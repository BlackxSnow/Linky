using Discord;
using Discord.WebSocket;

namespace linkybot;

public struct CustomLink
{
    public string FolderName { get; set; }
    public string Name { get; set; }
    public string URL { get; set; }

    private static async Task AddLink(SocketSlashCommand command, SocketSlashCommandDataOption subcommand)
    {
        var guild = (command.Channel as IGuildChannel)!.Guild;
        var config = ServerData.Get(guild.Id);

        var link = new CustomLink()
        {
            FolderName = subcommand.Options.ElementAt(0).Value as string,
            Name = subcommand.Options.ElementAt(1).Value as string,
            URL = subcommand.Options.ElementAt(2).Value as string
        };

        config.Links.Add(link);
        await ServerData.Save(guild.Id);
        await command.RespondAsync("Successfully added link.", ephemeral: true);
    }

    private static async Task RemoveLink(SocketSlashCommand command, SocketSlashCommandDataOption subcommand)
    {
        var guild = (command.Channel as IGuildChannel)!.Guild;
        var config = ServerData.Get(guild.Id);
        
        var name = subcommand.Options.ElementAt(0).Value as string;

        try
        {
            var match = config.Links.FindIndex(l => l.Name == name);
            config.Links.RemoveAt(match);
        }
        catch (ArgumentNullException)
        {
            await command.RespondAsync("Failed: Link was not found.", ephemeral: true);
        }

        await ServerData.Save(guild.Id);
        await command.RespondAsync("Successfully removed link.", ephemeral: true);
    }
    
    public static async Task Link(SocketSlashCommand command)
    {
        var subcommand = command.Data.Options.First();
        switch (subcommand.Name)
        {
            case "add":
                await AddLink(command, subcommand);
                break;
            case "remove":
                await RemoveLink(command, subcommand);
                break;
        }
    }
}