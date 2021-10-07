using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BitconfirmBot.Commands;
using BitconfirmBot.Extensions;
using BitconfirmBot.Helpers;
using BitconfirmBot.Models;
using BitconfirmBot.Services.Cache;
using BitconfirmBot.Services.Crypto;
using Telegram.Bot;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using File = System.IO.File;

namespace BitconfirmBot
{
    public static class Program
    {
        public static Data Data { get; } = new();

        private static readonly List<Type> _cryptoApiTypes = TypeHelper.GetSubclasses<CryptoApi>().ToList();

        private static readonly List<Command> _commands = TypeHelper.GetSubclasses<Command>()
            .Select(t => (Command)Activator.CreateInstance(t))
            .ToList();

        public static async Task Main()
        {
            Init();

            Data.Bot = new TelegramBotClient(Data.Settings.Token);
            Data.BotUsername = (await Data.Bot.GetMeAsync()).Username;

            Data.Bot.StartReceiving(HandleUpdate, HandleError, new ReceiverOptions()
            {
                AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery, UpdateType.ChatMember }
            });

            Console.WriteLine($"[+] @{Data.BotUsername} started!");

            await LoadCache();

            await Task.Delay(-1);
        }

        private static void Init()
        {
            Data.Settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path.Combine("Files", "Settings.json")));

            Data.Cache = new CacheService(Path.Combine("Files", "Cache.json"), new JsonSerializerOptions()
            {
                WriteIndented = true
            });

            Data.Proxies = File.ReadAllLines(Path.Combine("Files", "Proxies.txt"))
                .Select(p => new Proxy(p, Data.Settings.Proxies))
                .ToList();

            var cryptoApi = _cryptoApiTypes.SingleOrDefault(c => c.Name.TrimEnd("Service").Equals(Data.Settings.Api, StringComparison.OrdinalIgnoreCase));

            if (cryptoApi == null)
            {
                throw new Exception("No API found with this name.");
            }

            Data.Api = (CryptoApi)Activator.CreateInstance(cryptoApi, Data.Settings.Proxies.Use ? Data.Proxies : new HttpClient());
        }

        private static async Task LoadCache()
        {
            foreach (var transaction in Data.Cache.Read())
            {
                if (transaction.Api == Data.Settings.Api)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await BitconfirmCommand.StartMonitoringTransactionAsync(Data.Bot, transaction);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed monitoring cached transaction ({transaction.Blockchain} {transaction.TxId}): {ex.Message}");
                        }
                    });
                }
                else
                {
                    await Data.Bot.SendTextMessageAsync(transaction.Message.Chat,
                        "⚠️ Bot just restarted and API was changed, please resend your transaction.",
                        replyToMessageId: transaction.Message.MessageId);

                    if (transaction.LastBlockMinedMessage != null)
                    {
                        await Data.Bot.EditMessageReplyMarkupAsync(transaction.LastBlockMinedMessage.Chat, transaction.LastBlockMinedMessage.MessageId, null);
                    }

                    Data.Cache.Remove(transaction);
                }
            }

            Console.WriteLine("[+] Cache loaded!");
        }

        [SuppressMessage("ReSharper", "MethodSupportsCancellation")]
        private static async Task HandleUpdate(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
        {
            try
            {
                Message message = update.Message;
                Command command = null;
                string[] commandArgs = null;

                if (message is { Type: MessageType.Text })
                {
                    if (message.Text.StartsWith('/'))
                    {
                        string commandName = message.Text.TrimStart('/').Split(' ').First();

                        if (commandName.Contains('@') && !commandName.EndsWith("@" + Data.BotUsername, StringComparison.OrdinalIgnoreCase))
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

                        if (match.Groups[3].Success && Data.Api.EthereumBlockchains == null)
                        {
                            await bot.SendTextMessageAsync(message.Chat, new StringBuilder()
                                .AppendLine("😔 Sorry, Ethereum tokens aren't supported as of right now.")
                                .AppendLine()
                                .AppendLine("Here's the list of currently supported blockchains:")
                                .AppendLine()
                                .AppendJoin(" / ", Data.Api.GetFormattedSupportedBlockchains())
                                .ToString());

                            return;
                        }

                        string txid = match.Groups[2].Value;
                        int confirmations = splittedMessage.Length < 2 ? 1 : int.Parse(splittedMessage[1]);

                        command = _commands.Single(c => c is BitconfirmCommand);
                        commandArgs = new[] { txid, confirmations.ToString() };
                    }
                }
                else if (update.CallbackQuery is { } callbackQuery)
                {
                    string[] parts = callbackQuery.Data.Split(':');
                    string action = parts[0];

                    if (action == "toggleBlockAlerts")
                    {
                        int hashCode = int.Parse(parts[1]);

                        if (!Data.Cache.TryGetByHashCode(hashCode, out var transaction) || transaction.Message.From.Id != callbackQuery.From.Id)
                        {
                            await Data.Bot.AnswerCallbackQueryAsync(callbackQuery.Id);
                        }

                        transaction.BlockAlertsMuted = !transaction.BlockAlertsMuted;

                        Data.Cache.Update(transaction);

                        await Data.Bot.AnswerCallbackQueryAsync(callbackQuery.Id, transaction.BlockAlertsMuted
                            ? "🔕 Block alerts muted"
                            : "🔔 Block alerts unmuted");

                        await Data.Bot.EditMessageReplyMarkupAsync(transaction.LastBlockMinedMessage.Chat, transaction.LastBlockMinedMessage.MessageId,
                            new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData(transaction.BlockAlertsMuted
                                ? "🔔 Unmute block alerts"
                                : "🔕 Mute block alerts",
                                $"toggleBlockAlerts:{transaction.GetHashCode()}")));
                    }
                }
                else if (message?.NewChatMembers?.Any(u => u.Username == Data.BotUsername) ?? false)
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
