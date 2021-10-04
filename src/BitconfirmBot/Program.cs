using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
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
                AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery }
            };

            await bot.ReceiveAsync(HandleUpdateAsync, HandleError, receiverOptions);
        }

        [SuppressMessage("ReSharper", "MethodSupportsCancellation")]
        private static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
        {
            try
            {
                if (update.Message is { Type: MessageType.Text } message)
                {
                    if (!message.Text.StartsWith('/'))
                        return;

                    string commandName = message.Text.TrimStart('/').Split(' ').First();

                    if (commandName.Contains('@') && !commandName.EndsWith("@" + _botUsername, StringComparison.OrdinalIgnoreCase))
                        return;

                    commandName = commandName.Split('@').First().ToLower();

                    string[] commandArgs = message.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();

                    var command = _commands.SingleOrDefault(c => c.Name == commandName);

                    if (command == null)
                        return;

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
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private static Task HandleError(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
