using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BitconfirmBot.Commands;
using BitconfirmBot.Models;
using BitconfirmBot.Services.Cache;
using Telegram.Bot;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using File = System.IO.File;

namespace BitconfirmBot
{
    public static class Program
    {
        public static Settings Settings { get; private set; }

        public static CacheService Cache { get; private set; }

        public static string BotUsername { get; private set; }

        private static readonly List<Command> _commands = typeof(Command).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Command)) && t.IsClass && !t.IsAbstract)
            .Select(t => (Command)Activator.CreateInstance(t))
            .ToList();

        [SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
        public static async Task Main()
        {
            Settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path.Combine("Files", "Settings.json")));

            Cache = new CacheService(Path.Combine("Files", "Cache.json"), new JsonSerializerOptions()
            {
                WriteIndented = true
            });

            var bot = new TelegramBotClient(Settings.Token);
            BotUsername = (await bot.GetMeAsync()).Username;

            var receiverOptions = new ReceiverOptions()
            {
                AllowedUpdates = new[] { UpdateType.Message, UpdateType.ChatMember }
            };

            bot.StartReceiving(HandleUpdate, HandleError, receiverOptions);

            Console.WriteLine($"@{BotUsername} started!");

            foreach (var transaction in Cache.Read())
            {
                try
                {
                    await BitconfirmCommand.StartMonitoringTransactionAsync(bot, transaction);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to start monitoring cached transaction ({transaction.Network} {transaction.TxId}): {ex.Message}");
                }
            }

            Console.WriteLine("Cache loaded!");

            await Task.Delay(-1);
        }

        [SuppressMessage("ReSharper", "MethodSupportsCancellation")]
        private static async Task HandleUpdate(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
        {
            try
            {
                Message message = update.Message;
                Command command = null;
                string[] commandArgs = null;

                if (update.Message is { Type: MessageType.Text })
                {
                    if (message.Text.StartsWith('/'))
                    {
                        string commandName = message.Text.TrimStart('/').Split(' ').First();

                        if (commandName.Contains('@') && !commandName.EndsWith("@" + BotUsername, StringComparison.OrdinalIgnoreCase))
                            return;

                        commandName = commandName.Split('@').First().ToLower();

                        command = _commands.SingleOrDefault(c => c.Name == commandName);
                        commandArgs = message.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();
                    }
                    else
                    {
                        string[] splittedMessage = message.Text.Split(' ');
                        string transaction = splittedMessage[0];

                        var match = Regex.Match(transaction, @"^(.*[\/#=])?((0x)?[a-fA-F0-9]{64})(\/.*)?$");

                        if (!match.Success)
                            return;

                        if (match.Groups[3].Success)
                        {
                            await bot.SendTextMessageAsync(message.Chat, new StringBuilder()
                                .AppendLine("😔 Sorry, Ethereum tokens aren't supported as of right now.")
                                .AppendLine()
                                .AppendLine("Here's the list of currently supported networks:")
                                .AppendLine()
                                .AppendJoin(" / ", BitconfirmCommand.SupportedNetworks)
                                .ToString());

                            return;
                        }

                        string txid = match.Groups[2].Value;
                        int confirmations = splittedMessage.Length < 2 ? 1 : int.Parse(splittedMessage[1]);

                        command = _commands.Single(c => c is BitconfirmCommand);
                        commandArgs = new[] { txid, confirmations.ToString() };
                    }
                }
                else if (update.Message?.NewChatMembers?.Any(u => u.Username == BotUsername) ?? false)
                {
                    command = _commands.Single(c => c is StartCommand);
                }

                if (command != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await command.HandleAsync(bot, message, commandArgs);
                        }
                        catch (Exception ex)
                        {
                            try
                            {
                                await bot.SendTextMessageAsync(message.Chat, ex.Message);
                            }
                            catch
                            {
                                Console.WriteLine(ex + Environment.NewLine);
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex + Environment.NewLine);
            }
        }

        private static Task HandleError(ITelegramBotClient bot, Exception ex, CancellationToken cancellationToken)
        {
            Console.WriteLine(ex + Environment.NewLine);

            return Task.CompletedTask;
        }
    }
}
