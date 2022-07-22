using System.Text;
using Discord;
using Discord.WebSocket;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using File = Google.Apis.Drive.v3.Data.File;

namespace linkybot;

public static class Scraper
{
    private static readonly string[] _Scopes = { DriveService.Scope.DriveReadonly };
    private const string _ApplicationName = "LinkyDiscord";
    private const string _CredentialPath = "token.json";

    private static DriveService? _DriveService;

    private const string _DriveRootID = "1LkNGhmicF684l6ggVcJdmgbzy8CmnDB9";
    private const string _DriveRootLink = $"https://drive.google.com/drive/u/0/folders/{_DriveRootID}";
    private const string _DriveRootName = "Devil's Cookbook Stuff";

    private class Folder
    {
        public File? DriveFolder;
        public List<File> Files = new();
    }
    
    public static void InitialiseGoogle()
    {
        UserCredential credentials;
        using (var stream = new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
        {
            var secrets = GoogleClientSecrets.FromStream(stream).Secrets;
            var dataStore = new FileDataStore(_CredentialPath, true);
            credentials = GoogleWebAuthorizationBroker.AuthorizeAsync(
                secrets,
                _Scopes,
                "user",
                CancellationToken.None,
                dataStore).Result;
        }
        Console.WriteLine("Starting service...");
        _DriveService = new DriveService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credentials,
            ApplicationName = _ApplicationName
        });
    }

    private static async Task ExploreDriveDirectory(Dictionary<string, Folder> filesByFolder, File parentDir)
    {
        var request = _DriveService.Files.List();
        request.Q = $"'{parentDir.Id}' in parents";
        request.Fields = "files(name,webViewLink,mimeType,id)";
        var listing = await request.ExecuteAsync();

        var files = new List<File>(listing.Files.Count);
        var tasks = new List<Task>();
        foreach (var file in listing.Files)
        {
            if (file.MimeType == "application/vnd.google-apps.folder")
            {
                tasks.Add(ExploreDriveDirectory(filesByFolder, file));
            }
            else files.Add(file);
        }
        filesByFolder.Add(parentDir.Name, new Folder(){DriveFolder = parentDir, Files = files});
        await Task.WhenAll(tasks);
    }

    private static void AddCustomLinks(Dictionary<string, Folder> folders, ServerData config)
    {
        foreach (var link in config.Links)
        {
            var linkFile = new File()
            {
                Name = link.Name,
                WebViewLink = link.URL
            };
            if (folders.TryGetValue(link.FolderName, out var folder))
            {
                folder.Files.Add(linkFile);
                continue;
            }
            folders.Add(link.FolderName, new Folder() { Files = { linkFile } });
        }
    }
    
    private static async Task UpdateLinkPost(SocketGuild guild, bool isManual, SocketSlashCommand? command = null)
    {
        var config = ServerData.Get(guild.Id);
        if (config.TargetChannel == null)
        {
            if (command != null) await command.RespondAsync("No target channel is set, use /config to set one.", ephemeral: true);
            return;
        }
        if (guild.GetChannel((ulong)config.TargetChannel) is not IMessageChannel channel)
        {
            if (command != null) await command.RespondAsync("Warning: updatechannel is not valid text channel.");
            return;
        }
        
        var root = new File()
        {
            Name = _DriveRootName,
            WebViewLink = _DriveRootLink,
            Id = _DriveRootID
        };
        var filesByFolder = new Dictionary<string, Folder>();
        await ExploreDriveDirectory(filesByFolder, root);
        AddCustomLinks(filesByFolder, config);
        
        var embeds = new Embed[filesByFolder.Count];

        var i = 0;
        var description = new StringBuilder(128);
        foreach (var (folderName, data) in filesByFolder)
        {
            description.Clear();
            foreach (var file in data.Files)
            {
                description.AppendLine($"[{file.Name}]({file.WebViewLink})");
            }
            embeds[i] = new EmbedBuilder()
            {
                Title = folderName,
                Color = Color.Purple,
                Description = description.ToString(),
                Url = data.DriveFolder?.WebViewLink,
                Footer = new EmbedFooterBuilder() {Text = $"Last updated: {DateTime.Now} ({(isManual ? "Manual" : "Automatic")})"}
            }.Build();
            i++;
        }
        
        if (config.LinkMessageID != null)
        {
            var message = await channel.GetMessageAsync((ulong)config.LinkMessageID);
            if (message is IUserMessage userMessage)
            {
                await userMessage.ModifyAsync(msg => msg.Embeds = embeds);
                if (command!=null) await command.RespondAsync("Successfully fetched and updated existing post.", ephemeral: true);
                return;
            }
            if (command != null) await command.RespondAsync("Warning: stored message ID cannot be found or is invalid type.");
        }
        
        config.LinkMessageID = (await channel.SendMessageAsync(embeds: embeds)).Id;
        await ServerData.Save(guild.Id);
        if (command != null) await command.RespondAsync("Successfully fetched and created new post.", ephemeral: true);
    }
    
    public static async Task Update(SocketSlashCommand command)
    {
        await UpdateLinkPost((command.Channel as SocketGuildChannel)!.Guild, true, command);
    }
    
    
}