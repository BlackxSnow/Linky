using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Timers;
using Discord;
using Discord.WebSocket;
using Timer = System.Timers.Timer;

namespace linkybot;

public class ServerData
{
    public ulong GuildID { get; set; }
    public ulong? TargetChannel { get; set; }
    public ulong? LinkMessageID { get; set; }
    public List<ulong> LinkMessageIDs { get; set; }
    public List<string> CommandChannels { get; set; }
    public List<CustomLink>? Links { get; set; }
    public List<IgnorePattern>? IgnorePatterns { get; set; }
    public DateTime LastUpdate { get; set; }
    
    [NonSerialized] private Timer _UpdateTimer;
    private static readonly TimeSpan _OneHour = new TimeSpan(0, 1, 0, 0);
    
    private static readonly Dictionary<ulong, ServerData> _LoadedGuilds = new Dictionary<ulong, ServerData>();

    private void CompleteTimer(object? sender, ElapsedEventArgs e)
    {
        Scraper.Update(GuildID).Wait();
        SetupTimer();
    }

    private void SetupTimer()
    {
        var sinceLastUpdate = DateTime.Now - LastUpdate;
        var timeToNext = Math.Min((_OneHour - sinceLastUpdate).TotalMilliseconds, _OneHour.TotalMilliseconds);
        _UpdateTimer.Interval = timeToNext;
        _UpdateTimer.Start();
    }
    
    private async Task Initialise(ulong guildID)
    {
        GuildID = guildID;
        var sinceLastUpdate = DateTime.Now - LastUpdate;
        if (sinceLastUpdate.TotalHours > 1)
        {
            await Scraper.Update(guildID);
        }
        SetupTimer();
    }

    public void ResetTimer()
    {
        _UpdateTimer.Stop();
        SetupTimer();
    }
    
    public ServerData()
    {
        CommandChannels = new List<string>();
        Links = new List<CustomLink>();
        _UpdateTimer = new Timer();
        _UpdateTimer.Elapsed += CompleteTimer;
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

    public static async Task LoadAll()
    {
        var idPattern = new Regex("/([1-9]*).json");
        await Program.Log(new LogMessage(LogSeverity.Info, "ServerData.LoadAll", "Loading all server data files..."));
        foreach (var file in Directory.GetFiles("./data"))
        {
            var id = ulong.Parse(idPattern.Match(file).Groups[1].Value);
            if (_LoadedGuilds.ContainsKey(id)) continue;

            ServerData data;
            await using (var stream = File.OpenRead(file))
            {
                data = JsonSerializer.Deserialize<ServerData>(stream, new JsonSerializerOptions() {Converters = { new IgnorePattern.JsonConverter() }})!;
                _LoadedGuilds.Add(id, data);
            }
            await data.Initialise(id);
            await Program.Log(new LogMessage(LogSeverity.Info, "ServerData.LoadAll", $"Loaded guild {id}."));
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
                data = JsonSerializer.Deserialize<ServerData>(stream, new JsonSerializerOptions() {Converters = { new IgnorePattern.JsonConverter() }})!;
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
            await Program.Log(new LogMessage(LogSeverity.Error, "ServerData", $"Attempted to save id {guildID} with no data in memory."));
            return;
        }
        var dataFile = $"./data/{guildID}.json";
        Directory.CreateDirectory("data");
        await using var stream = File.Open(dataFile, FileMode.Create, FileAccess.Write);
        await JsonSerializer.SerializeAsync(stream, data, new JsonSerializerOptions() {Converters = { new IgnorePattern.JsonConverter() }});
    }
}