using System.Text;
using System.Text.Json;
using Discord;
using Discord.WebSocket;

namespace linkybot;

public class ServerData
{
    public ulong? TargetChannel { get; set; }
    public ulong? LinkMessageID { get; set; }
    public List<string> CommandChannels { get; set; }
    public List<CustomLink> Links { get; set; }

    private static Dictionary<ulong, ServerData> _LoadedGuilds = new Dictionary<ulong, ServerData>();
    
    public ServerData()
    {
        CommandChannels = new List<string>();
        Links = new List<CustomLink>();
    }

    private static async Task ListConfig(SocketSlashCommand command, SocketSlashCommandDataOption subcommand)
    {
        var config = Get((ulong)command.GuildId!);

        var guild = (command.Channel as SocketGuildChannel).Guild;
        
        var values = new StringBuilder();
        values.AppendLine($"updatechannel: {(config.TargetChannel != null ? guild.GetChannel((ulong)config.TargetChannel) : "Unset")}");
        
        var embed = new EmbedBuilder()
        {
            Title = "Config",
            Color = Color.Purple,
            Description = values.ToString()
        }.Build();

        await command.RespondAsync(embed: embed);
    }

    private static async Task SetConfig(SocketSlashCommand command, SocketSlashCommandDataOption subcommand)
    {
        var guildID = (ulong)command.GuildId!;
        var config = Get(guildID);

        var option = subcommand.Options.First();
        var value = option.Options.First();
        var succeeded = true;
        switch (option.Name)
        {
            case "updatechannel":
                config.TargetChannel = (value.Value as SocketChannel).Id;
                break;
            default:
                succeeded = false;
                break;
        }

        if (succeeded)
        {
            await Save(guildID);
            await command.RespondAsync("Successfully set config value.", ephemeral: true);
        }
        else await command.RespondAsync("Failed to set config value.", ephemeral: true);
    }
    
    public static async Task Configure(SocketSlashCommand command)
    {
        var subcommand = command.Data.Options.First();
        switch (subcommand.Name)
        {
            case "list":
                await ListConfig(command, subcommand);
                break;
            case "set":
                await SetConfig(command, subcommand);
                break;
        }
    }
    
    public static ServerData Get(ulong guildID)
    {
        if (_LoadedGuilds.TryGetValue(guildID, out var data)) return data;
        var dataFile = $"./data/{guildID}.json";
        if (File.Exists(dataFile))
        {
            using (var stream = File.OpenRead(dataFile))
            {
                data = JsonSerializer.Deserialize<ServerData>(stream)!;
            }
            _LoadedGuilds.Add(guildID, data);
            return data;
        }

        var newData = new ServerData();
        _LoadedGuilds.Add(guildID, newData);
        return newData;
    }

    public static async Task Save(ulong guildID)
    {
        if (!_LoadedGuilds.TryGetValue(guildID, out var data))
        {
            Program.Log(new LogMessage(LogSeverity.Error, "ServerData", $"Attempted to save id {guildID} with no data in memory."));
            return;
        }
        var dataFile = $"./data/{guildID}.json";
        Directory.CreateDirectory("data");
        await using var stream = File.OpenWrite(dataFile);
        await JsonSerializer.SerializeAsync(stream, data);
    }
}