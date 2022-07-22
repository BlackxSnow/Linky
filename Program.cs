using System.Runtime.Serialization;
using System.Text.Json;
using Discord;
using Discord.WebSocket;
using linkybot;

public class JSONConfig
{
    public string? Token { get; set; }
}

public class Program
{
    public static DiscordSocketClient? Client { get; private set; }
    
    public static Task Main(string[] args) => MainAsync();

    private static async Task MainAsync()
    {
        await Log(new LogMessage(LogSeverity.Info, "Main", "Connecting to Google Drive..."));
        Scraper.InitialiseGoogle();
        
        Client = new DiscordSocketClient();
        
        Client.Log += Log;
        Client.Ready += OnReady;
        Client.JoinedGuild += OnJoin;

        var configStream = File.OpenRead("config.json");
        var jsonConfig = (JSONConfig?)(await JsonSerializer.DeserializeAsync(configStream, typeof(JSONConfig)));
        configStream.Close();
        if (jsonConfig == null) throw new SerializationException("Unable to read config.json");
        await Client.LoginAsync(TokenType.Bot, jsonConfig.Token);
        await Client.StartAsync();

        
        
        await Task.Delay(-1);
    }

    private static async Task OverwriteCommands(SocketGuild guild, bool assign)
    {
        await guild.BulkOverwriteApplicationCommandAsync(Commands.GetCommands(Client));
    }

    private static async Task InitialiseGuild(SocketGuild guild)
    {
        await OverwriteCommands(guild, true);
        
    }
    
    private static async Task OnReady()
    {
        await Log(new LogMessage(LogSeverity.Info, "OnReady", "Initialising commands..."));
       
        foreach (var guild in Client.Guilds)
        {
            await InitialiseGuild(guild);
        }
        await Log(new LogMessage(LogSeverity.Info, "OnReady", $"Finished initialising commands."));
    }

    private static async Task OnJoin(SocketGuild guild)
    {
        await InitialiseGuild(guild);
    }
    
    public static Task Log(LogMessage message)
    {
        Console.WriteLine(message.ToString());
        return Task.CompletedTask;
    }
}