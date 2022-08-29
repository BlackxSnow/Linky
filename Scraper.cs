using System.Text;
using Discord;
using Discord.WebSocket;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util;
using Google.Apis.Util.Store;
using File = Google.Apis.Drive.v3.Data.File;

namespace linkybot;

public static class Scraper
{
    private static readonly string[] _Scopes = { DriveService.Scope.DriveReadonly };
    private const string _ApplicationName = "LinkyDiscord";
    private const string _CredentialPath = "./token";

    private static DriveService? _DriveService;
    private static UserCredential? _Credentials;

    private const string _DriveRootID = "1LkNGhmicF684l6ggVcJdmgbzy8CmnDB9";
    private const string _DriveRootLink = $"https://drive.google.com/drive/u/0/folders/{_DriveRootID}";
    private const string _DriveRootName = "Devil's Cookbook Stuff";

    private class Folder
    {
        public DriveFileEntry? DriveFolder;
        public List<File> Files = new();
    }
    
    public static void InitialiseGoogle()
    {
        using (var stream = new FileStream("./credentials.json", FileMode.Open, FileAccess.Read))
        {
            var secrets = GoogleClientSecrets.FromStream(stream).Secrets;
            var dataStore = new FileDataStore(_CredentialPath, true);
            _Credentials = GoogleWebAuthorizationBroker.AuthorizeAsync(
                secrets,
                _Scopes,
                "user",
                CancellationToken.None,
                dataStore).Result;
        }
        
        Console.WriteLine("Starting service...");
        
        _DriveService = new DriveService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = _Credentials,
            ApplicationName = _ApplicationName
        });
    }

    private static async Task ExploreDriveDirectory(Dictionary<string, Folder> filesByFolder, DriveFileEntry parentDir, ServerData config)
    {
        
        FilesResource.ListRequest? request = _DriveService.Files.List();
        request.Q = $"'{parentDir.DriveFile.Id}' in parents";
        request.Fields = "files(name,webViewLink,mimeType,id)";
        FileList? listing = await request.ExecuteAsync();

        var files = new List<File>(listing.Files.Count);
        var tasks = new List<Task>();
        foreach (File? file in listing.Files)
        {
            string path = Path.Combine(parentDir.Path, file.Name);
            if (config.IgnorePatterns != null && config.IgnorePatterns.Any(p => p.Pattern.IsMatch(path))) continue;
            var entry = new DriveFileEntry(path, file);
            if (file.MimeType == "application/vnd.google-apps.folder")
            {
                tasks.Add(ExploreDriveDirectory(filesByFolder, entry, config));
            }
            else files.Add(file);
        }
        filesByFolder.Add(parentDir.Path, new Folder(){DriveFolder = parentDir, Files = files});
        await Task.WhenAll(tasks);
    }

    private static void AddCustomLinks(Dictionary<string, Folder> folders, ServerData config)
    {
        foreach (CustomLink link in config.Links)
        {
            var linkFile = new File()
            {
                Name = link.Name,
                WebViewLink = link.URL
            };
            if (folders.TryGetValue(link.FolderName, out Folder? folder))
            {
                folder.Files.Add(linkFile);
                continue;
            }
            folders.Add(link.FolderName, new Folder() { Files = { linkFile } });
        }
    }

    const int _MaxEmbedsPerPost = 10;
    private static List<Embed>[] BuildEmbeds(Dictionary<string, Folder> filesByFolder, bool isManual, ServerData config, out int neededPostCount)
    {
        neededPostCount = (int)Math.Ceiling(filesByFolder.Count / (double)_MaxEmbedsPerPost);
        var embeds = new List<Embed>[neededPostCount];

        var description = new StringBuilder(128);
        
        Dictionary<string, Folder>.Enumerator filePairs = filesByFolder.GetEnumerator();
        for (var p = 0; p < neededPostCount; p++)
        {
            int currentOffset = p * _MaxEmbedsPerPost;
            int remainingEmbeds = filesByFolder.Count - currentOffset;
            int postEmbedCount = Math.Min(remainingEmbeds, _MaxEmbedsPerPost);
            embeds[p] = new List<Embed>(postEmbedCount);

            
            
            for (var e = 0; e < postEmbedCount; e++)
            {
                filePairs.MoveNext();
                description.Clear();

                foreach (File file in filePairs.Current.Value.Files)
                {
                    description.AppendLine($"[{file.Name}]({file.WebViewLink})");
                }
                
                embeds[p].Add(new EmbedBuilder()
                {
                    Title = Path.GetFileName(filePairs.Current.Key),
                    Color = Color.Purple,
                    Description = description.ToString(),
                    Url = filePairs.Current.Value.DriveFolder?.DriveFile.WebViewLink,
                    Author = new EmbedAuthorBuilder() { Name = filePairs.Current.Key },
                    Timestamp = new DateTimeOffset(DateTime.Now),
                    Footer = new EmbedFooterBuilder() {Text = $"Last Update: ({(isManual ? "Manual" : "Automatic")})"}
                }.Build());
            }
        }
        
        return embeds;
    }
    
    private static async Task UpdateLinkPosts(SocketGuild guild, bool isManual, SocketSlashCommand? command = null)
    {
        ServerData config = ServerData.Get(guild.Id);
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
        
        var root = new DriveFileEntry("/" ,new File()
        {
            Name = _DriveRootName,
            WebViewLink = _DriveRootLink,
            Id = _DriveRootID
        });
        var filesByFolder = new Dictionary<string, Folder>();
        await ExploreDriveDirectory(filesByFolder, root, config);
        AddCustomLinks(filesByFolder, config);

        List<Embed>[] embeds = BuildEmbeds(filesByFolder, isManual, config, out int neededPostCount);

        for (var i = 0; i < neededPostCount; i++)
        {
            if (config.LinkMessageIDs.Count > i)
            {
                IMessage message = await channel.GetMessageAsync(config.LinkMessageIDs[i]);
                if (message != null)
                {
                    await ((message as IUserMessage)!).ModifyAsync(msg => msg.Embeds = embeds[i].ToArray());
                }
                else
                {
                    config.LinkMessageIDs[i] = (await channel.SendMessageAsync(embeds: embeds[i].ToArray())).Id;
                }
            }
            else
            {
                config.LinkMessageIDs.Add((await channel.SendMessageAsync(embeds: embeds[i].ToArray())).Id);
            }
        }

        if (neededPostCount < config.LinkMessageIDs.Count)
        {
            config.LinkMessageIDs.RemoveRange(neededPostCount, config.LinkMessageIDs.Count - neededPostCount);
        }

        for (int i = config.LinkMessageIDs.Count; i > neededPostCount; i--)
        {
            IMessage toRemove = await channel.GetMessageAsync(config.LinkMessageIDs[i - 1]);
            if (toRemove != null) await toRemove.DeleteAsync();
            config.LinkMessageIDs.RemoveAt(i-1);
        }
        
        config.LastUpdate = DateTime.Now;
        await ServerData.Save(guild.Id);
        if (command!=null) await command.RespondAsync("Successfully updated posts.", ephemeral: true);
    }
    
    public static async Task Update(SocketSlashCommand command)
    {
        var guild = (command.Channel as SocketGuildChannel)!.Guild;
        await UpdateLinkPosts(guild, true, command);
        var data = ServerData.Get(guild.Id);
        data.ResetTimer();
    }

    public static async Task Update(ulong guildID)
    {
        await UpdateLinkPosts(Program.Client!.GetGuild(guildID), false);
    }
    
    
}