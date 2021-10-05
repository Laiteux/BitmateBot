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
using Telegram.Bot;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using File = System.IO.File;

namespace BitconfirmBot
{
    public static class Program
    {
        private static readonly List<Command> _commands = typeof(Command).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Command)) && t.IsClass && !t.IsAbstract)
            .Select(t => (Command)Activator.CreateInstance(t))
            .ToList();

        private static string _botUsername;

        [SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
        [SuppressMessage("ReSharper", "MethodSupportsCancellation")]
        public static async Task Main()
        {
            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path.Combine("Files", "Settings.json")));

            var bot = new TelegramBotClient(settings.Token);
            _botUsername = (await bot.GetMeAsync()).Username;

            var receiverOptions = new ReceiverOptions()
            {
                AllowedUpdates = new[] { UpdateType.Message }
            };

            await bot.ReceiveAsync(HandleUpdate, HandleError, receiverOptions);
        }

        [SuppressMessage("ReSharper", "MethodSupportsCancellation")]
        private static async Task HandleUpdate(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
        {
            try
            {
                if (update.Message is { Type: MessageType.Text } message)
                {
                    Command command;
                    string[] commandArgs;

                    if (message.Text.StartsWith('/'))
                    {
                        string commandName = message.Text.TrimStart('/').Split(' ').First();

                        if (commandName.Contains('@') && !commandName.EndsWith("@" + _botUsername, StringComparison.OrdinalIgnoreCase))
                            return;

                        commandName = commandName.Split('@').First().ToLower();

                        command = _commands.SingleOrDefault(c => c.Name == commandName);
                        commandArgs = message.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();
                    }
                    else
                    {
                        string[] splittedMessage = message.Text.Split(' ');
                        string txid = splittedMessage[0];

                        if (Regex.IsMatch(txid, "^0x[a-fA-F0-9]{64}$"))
                        {
                            await bot.SendTextMessageAsync(message.Chat, new StringBuilder()
                                .AppendLine("😔 Sorry, Ethereum tokens aren't supported as of right now.")
                                .AppendLine()
                                .AppendLine("Here's the list of currently supported networks:")
                                .AppendLine()
                                .AppendJoin("\n", BitconfirmCommand.SupportedNetworks.Select(n => $"- {n} / {n}TEST"))
                                .ToString());

                            return;
                        }

                        if (!Regex.IsMatch(txid, "^[a-fA-F0-9]{64}$"))
                            return;

                        int confirmations = splittedMessage.Length < 2 ? 1 : int.Parse(splittedMessage[1]);

                        command = _commands.Single(c => c is BitconfirmCommand);
                        commandArgs = new[] { BitconfirmCommand.AutoNetwork, txid, confirmations.ToString() };
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
                                    Console.WriteLine(ex);
                                }
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private static async Task HandleError(ITelegramBotClient bot, Exception ex, CancellationToken cancellationToken)
        {
            Console.WriteLine(ex);
        }
    }
}
