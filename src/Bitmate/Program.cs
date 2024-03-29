﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Bitmate.Commands;
using Bitmate.Extensions;
using Bitmate.Helpers;
using Bitmate.Models;
using Bitmate.Services.Cache;
using Bitmate.Services.Crypto;
using Telegram.Bot;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using File = System.IO.File;

namespace Bitmate
{
    public static class Program
    {
        public static Data Data { get; } = new();

        private static List<Type> _cryptoApiTypes;

        private static List<Command> _commands;

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

            _cryptoApiTypes = TypeHelper.GetSubclasses<CryptoApi>().ToList();

            var cryptoApi = _cryptoApiTypes.SingleOrDefault(c => c.Name.TrimEnd(nameof(CryptoApi)).Equals(Data.Settings.Api, StringComparison.OrdinalIgnoreCase));

            if (cryptoApi == null)
            {
                throw new Exception("No API found with this name.");
            }

            Data.FuncApi = () => (CryptoApi)Activator.CreateInstance(cryptoApi, Data.Settings.Proxies.Use ? Data.Proxies : new HttpClient());

            _commands = TypeHelper.GetSubclasses<Command>()
                .Select(t => (Command)Activator.CreateInstance(t))
                .ToList();
        }

        private static async Task LoadCache()
        {
            var trackCommand = (TrackCommand)_commands.Single(c => c is TrackCommand);

            foreach (var transaction in Data.Cache.Read())
            {
                if (transaction.Api.Equals(Data.Settings.Api, StringComparison.OrdinalIgnoreCase))
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await trackCommand.StartMonitoringTransactionAsync(Data.Bot, transaction);
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
                        "⚠️ Bot was just restarted and API was changed, please send me your transaction again.",
                        replyToMessageId: transaction.Message.MessageId,
                        allowSendingWithoutReply: true);

                    if (transaction.LastBlockMinedMessage != null)
                    {
                        // ReSharper disable once RedundantArgumentDefaultValue
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

                        if (commandName.Contains('@'))
                        {
                            if (!commandName.EndsWith("@" + Data.BotUsername, StringComparison.OrdinalIgnoreCase))
                            {
                                return;
                            }

                            commandName = commandName.Split('@').First();
                        }

                        command = _commands.SingleOrDefault(c => c.Name.Contains(commandName, StringComparer.OrdinalIgnoreCase));
                        commandArgs = message.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();
                    }
                    else
                    {
                        string[] splittedMessage = message.Text.Split(' ');
                        string transaction = splittedMessage[0];

                        var match = Regex.Match(transaction, @"^(?:.*[\/#=])?((0x)?[a-fA-F0-9]{64})(?:\/.*)?$");

                        if (!match.Success)
                            return;

                        string txid = match.Groups[1].Value;
                        int confirmations = splittedMessage.Length < 2 ? 1 : int.Parse(splittedMessage[1]);

                        command = _commands.Single(c => c is TrackCommand);
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

                            return;
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
                            if (Data.BotUsername == "BitconfirmBot")
                            {
                                await bot.SendTextMessageAsync(message.Chat, "ℹ️ Bitconfirm is now Bitmate! Find me here: @BitmateBot");

                                return;
                            }

                            command = (Command)Activator.CreateInstance(command.GetType());

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
