using System.Reflection;
using Discord;
using Discord.WebSocket;

namespace linkybot;

public static class Commands
{
    private class CommandBuilder : SlashCommandBuilder
    {
        public Func<SocketSlashCommand, Task>? OnExecute;
    }

    private static Dictionary<string, Func<SocketSlashCommand, Task>>? _CommandHandler;

    private static SlashCommandProperties[]? _Commands;

    public static SlashCommandProperties[] GetCommands(DiscordSocketClient client)
    {
        if (_Commands != null) return _Commands;
        
        ProcessCommands(client);
        return _Commands!;
    }
    
    private static async Task ExecuteCommand(SocketSlashCommand command)
    {
        await _CommandHandler![command.Data.Name](command);
    }
    
    private static void ProcessCommands(DiscordSocketClient client)
    {
        _CommandHandler = new Dictionary<string, Func<SocketSlashCommand, Task>>();

        var container = typeof(Commands);
        var fields = container.GetFields(BindingFlags.Static | BindingFlags.NonPublic);

        var commands = new List<SlashCommandProperties>(fields.Length);
        
        foreach (var field in fields)
        {
            var value = field.GetValue(null);
            if (value is not CommandBuilder command) continue;

            _CommandHandler.Add(command.Name, command.OnExecute!);
            commands.Add(command.Build());
        }

        _Commands = commands.ToArray();
        client.SlashCommandExecuted += ExecuteCommand;
    }

    private static CommandBuilder _Update = new CommandBuilder()
    {
        Name = "update",
        Description = "Poll endpoints for updates immediately.",
        IsDMEnabled = false,
        OnExecute = Scraper.Update
    };
    
    private static CommandBuilder _Configure = new CommandBuilder()
    {
        Name = "configure",
        Description = "Update or view server configuration values.",
        IsDMEnabled = false,
        OnExecute = ServerData.Configure,
        Options = new List<SlashCommandOptionBuilder>()
        {
            new SlashCommandOptionBuilder()
            {
                Name = "list",
                Description = "Show all configuration options and their values.",
                Type = ApplicationCommandOptionType.SubCommand,
            },
            new SlashCommandOptionBuilder()
            {
                Name = "set",
                Description = "Set a configuration value.",
                Type = ApplicationCommandOptionType.SubCommandGroup,
                Options = new List<SlashCommandOptionBuilder>()
                {
                    new SlashCommandOptionBuilder()
                    {
                        Name = "updatechannel",
                        Description = "Channel the bot will send and edit messages in.",
                        Type = ApplicationCommandOptionType.SubCommand,
                        Options = new List<SlashCommandOptionBuilder>()
                        {
                            new SlashCommandOptionBuilder()
                            {
                                Name = "channel",
                                Description = "The target channel.",
                                Type = ApplicationCommandOptionType.Channel,
                                IsRequired = true
                            }
                        }
                    }
                }
            }
        }
    };
    
    private static CommandBuilder _Link = new CommandBuilder()
    {
        Name = "link",
        Description = "Add or remove custom links.",
        IsDMEnabled = false,
        OnExecute = CustomLink.Link,
        Options = new List<SlashCommandOptionBuilder>()
        {
            new()
            {
                Name = "add",
                Description = "Show all configuration options and their values.",
                Type = ApplicationCommandOptionType.SubCommand,
                Options = new List<SlashCommandOptionBuilder>()
                {
                    new SlashCommandOptionBuilder()
                    {
                        Name = "foldername",
                        Description = "Folder name to put link under.",
                        Type = ApplicationCommandOptionType.String,
                        IsRequired = true
                    },
                    new SlashCommandOptionBuilder()
                    {
                        Name = "linkname",
                        Description = "Visible name of the link.",
                        Type = ApplicationCommandOptionType.String,
                        IsRequired = true
                    },
                    new SlashCommandOptionBuilder()
                    {
                        Name = "url",
                        Description = "URL of the custom link.",
                        Type = ApplicationCommandOptionType.String,
                        IsRequired = true
                    }
                }
            },
            new()
            {
                Name = "remove",
                Description = "Remove an existing custom link.",
                Type = ApplicationCommandOptionType.SubCommand,
                Options = new List<SlashCommandOptionBuilder>()
                {
                    new SlashCommandOptionBuilder()
                    {
                        Name = "name",
                        Description = "Name of the link to remove.",
                        Type = ApplicationCommandOptionType.String
                    }
                }
            }
        }
    };
}