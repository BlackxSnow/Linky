using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Discord;
using Discord.WebSocket;

namespace linkybot;

public struct IgnorePattern
{
    // public string SerializedPattern { get; set; }
    // [NonSerialized] public Regex
    public Regex Pattern { get; private set; }

    public class JsonConverter : JsonConverter<IgnorePattern>
    {
        public override IgnorePattern Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return new IgnorePattern() { Pattern = new Regex(reader.GetString()!) };
        }

        public override void Write(Utf8JsonWriter writer, IgnorePattern value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.Pattern.ToString());
        }
    }

    private static async Task AddPattern(SocketSlashCommand command, SocketSlashCommandDataOption subcommand)
    {
        IGuild? guild = (command.Channel as IGuildChannel)!.Guild;
        ServerData config = ServerData.Get(guild.Id);

        var pattern = new IgnorePattern()
        {
            Pattern = new Regex(subcommand.Options.ElementAt(0).Value as string)
        };

        config.IgnorePatterns ??= new List<IgnorePattern>();
        config.IgnorePatterns.Add(pattern);
        await ServerData.Save(guild.Id);
        await command.RespondAsync("Successfully added pattern.", ephemeral: true);
    }

    private static async Task RemovePattern(SocketSlashCommand command, SocketSlashCommandDataOption subcommand)
    {
        IGuild? guild = (command.Channel as IGuildChannel)!.Guild;
        ServerData config = ServerData.Get(guild.Id);
        
        var toRemove = subcommand.Options.ElementAt(0).Value as string;

        try
        {
            int match = config.IgnorePatterns.FindIndex(p => p.Pattern.ToString() == toRemove);
            config.Links.RemoveAt(match);
        }
        catch (ArgumentNullException)
        {
            await command.RespondAsync("Failed: Pattern was not found.", ephemeral: true);
        }

        await ServerData.Save(guild.Id);
        await command.RespondAsync("Successfully removed pattern.", ephemeral: true);
    }
    
    public static async Task ModifyPattern(SocketSlashCommand command)
    {
        SocketSlashCommandDataOption? subcommand = command.Data.Options.First();
        switch (subcommand.Name)
        {
            case "add":
                await AddPattern(command, subcommand);
                break;
            case "remove":
                await RemovePattern(command, subcommand);
                break;
        }
    }
}