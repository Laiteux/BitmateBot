using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bitmate.Extensions;
using Bitmate.Services.Cache;
using Bitmate.Services.Cache.Models;
using Bitmate.Services.Crypto.Models;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Bitmate.Commands
{
    public class TrackCommand : Command
    {
        private static CacheService Cache => Program.Data.Cache;

        private static int _currentlyMonitoredTransactions;

        public TrackCommand() : base("track", "bitconfirm")
        {
        }

        protected override Dictionary<string, bool> Args { get; } = new()
        {
            { "txid", true },
            { "confirmations", false }
        };

        protected override async Task ExecuteAsync(ITelegramBotClient bot, Message message, string[] args)
        {
            string blockchain = null;
            string txid = args[0];
            long confirmations = args.Length < 2 ? 1 : long.Parse(args[1]);

            if (txid.StartsWith("0x") && !CryptoApi.GetFormattedBlockchains().Contains("ETH"))
            {
                await bot.SendTextMessageAsync(message.Chat,
                    CryptoApi.BuildSupportedBlockchainsMessage("😔 Sorry, Ethereum tokens aren't supported as of right now."),
                    ParseMode.Markdown);

                return;
            }

            TrackedTransaction transaction = null;

            var locatingTransactionMessage = await bot.SendTextMessageAsync(message.Chat, "🔄 Locating transaction...");

            foreach (string supportedBlockchain in CryptoApi.MainBlockchains.Concat(CryptoApi.TestBlockchains ?? Array.Empty<string>()))
            {
                transaction = await CryptoApi.GetTransactionAsync(supportedBlockchain, txid);

                if (transaction.Found)
                {
                    blockchain = supportedBlockchain;

                    bool testnet = CryptoApi.TestBlockchains != null && CryptoApi.TestBlockchains.Contains(blockchain);

                    await bot.EditMessageTextAsync(locatingTransactionMessage.Chat, locatingTransactionMessage.MessageId,
                        $"🌐 Transaction found on the *{CryptoApi.FormatBlockchainName(blockchain)}{(testnet ? " test" : null)}* blockchain.",
                        ParseMode.Markdown);

                    break;
                }
            }

            if (blockchain == null)
            {
                await bot.EditMessageTextAsync(locatingTransactionMessage.Chat, locatingTransactionMessage.MessageId,
                    CryptoApi.BuildSupportedBlockchainsMessage("😓 Sorry, I was unable to locate this transaction on any blockchain."),
                    ParseMode.Markdown);

                return;
            }

            var cachedTransaction = new CachedTransaction(Program.Data.Settings.Api, blockchain, txid, confirmations, message);

            if (Cache.TryGet(cachedTransaction, out var found))
            {
                await bot.SendTextMessageAsync(message.Chat, "⬆️ You are already monitoring this transaction.",
                    replyToMessageId: found.Message.MessageId);

                return;
            }

            const int maxConfirmations = 50;

            if (confirmations is < 1 or > maxConfirmations)
            {
                await bot.SendTextMessageAsync(message.Chat, $"❌ Confirmation count can not exceed *{maxConfirmations}*.",
                    ParseMode.Markdown);

                return;
            }

            if (transaction.Found)
            {
                if (transaction.Confirmations >= confirmations)
                {
                    await bot.SendTextMessageAsync(message.Chat, $"❎ Your transaction has already reached *{transaction.Confirmations}* {"confirmation".Pluralize(transaction.Confirmations)}.",
                        ParseMode.Markdown);

                    return;
                }

                if (transaction.Confirmations > 0)
                {
                    await bot.SendTextMessageAsync(message.Chat, $"ℹ️ Your transaction currently has *{transaction.Confirmations}* {"confirmation".Pluralize(transaction.Confirmations)}.", ParseMode.Markdown);
                }

                await bot.SendTextMessageAsync(message.Chat, $"🔔 {(transaction.Confirmations > 0 ? null : "Ok, ")}I will let you know when {(transaction.Confirmations > 0 ? "it" : "your transaction")} hits *{confirmations}* {"confirmation".Pluralize(confirmations)}.",
                    ParseMode.Markdown);
            }
            else
            {
                await bot.SendTextMessageAsync(message.Chat, "❌ An error occurred when trying to fetch your transaction, please try again.");

                return;
            }

            Cache.Add(cachedTransaction);

            await StartMonitoringTransactionAsync(bot, cachedTransaction, transaction);
        }

        public async Task StartMonitoringTransactionAsync(ITelegramBotClient bot, CachedTransaction cachedTransaction, TrackedTransaction transaction = null)
        {
            string network = cachedTransaction.Blockchain;
            string txid = cachedTransaction.TxId;
            long confirmations = cachedTransaction.Confirmations;
            var message = cachedTransaction.Message;
            Message lastBlockMinedMessage;

            transaction ??= await CryptoApi.GetTransactionAsync(network, txid);
            bool oneConfirmation = transaction.Confirmations > 0;
            bool newBlock = false;

            _currentlyMonitoredTransactions++;

            while (true)
            {
                try
                {
                    // Refresh cachedTransaction in case the model got updated
                    Cache.TryGet(cachedTransaction, out cachedTransaction);
                    lastBlockMinedMessage = cachedTransaction.LastBlockMinedMessage;

                    transaction = await CryptoApi.GetTransactionAsync(network, txid);

                    if (transaction.Confirmations >= confirmations)
                    {
                        await bot.SendTextMessageAsync(message.Chat,
                            $"✅ Your transaction just hit *{transaction.Confirmations}* {"confirmation".Pluralize(transaction.Confirmations)}!",
                            ParseMode.Markdown,
                            replyToMessageId: message.MessageId);

                        break;
                    }

                    if (transaction.DoubleSpent)
                    {
                        var text = new StringBuilder()
                            .AppendLine("*⚠️ Your transaction has been double-spent!*")
                            .AppendLine()
                            .AppendLine("This could be either because the sender reversed it, or accelerated it by increasing the fee.");

                        if (transaction.DoubleSpentTxId != null)
                        {
                            text.AppendLine()
                                .AppendLine($"Here is the replacement transaction: [{transaction.DoubleSpentTxId}](https://live.blockcypher.com/btc/tx/{transaction.DoubleSpentTxId}/)");
                        }

                        text.AppendLine()
                            .AppendLine("*Be extremely careful when accepting this transaction!*");

                        await bot.SendTextMessageAsync(message.Chat, text.ToString(),
                            ParseMode.Markdown,
                            disableWebPagePreview: true,
                            replyToMessageId: message.MessageId);

                        break;
                    }

                    if (!oneConfirmation)
                    {
                        try
                        {
                            long height = await CryptoApi.GetBlockchainHeightAsync(network);

                            if (cachedTransaction.LastBlockMined == 0)
                            {
                                cachedTransaction.LastBlockMined = height;
                                Cache.Update(cachedTransaction);
                            }
                            else
                            {
                                if (height > cachedTransaction.LastBlockMined)
                                {
                                    if (newBlock)
                                    {
                                        if (!cachedTransaction.BlockAlertsMuted)
                                        {
                                            if (lastBlockMinedMessage != null)
                                            {
                                                // ReSharper disable once RedundantArgumentDefaultValue
                                                await bot.EditMessageReplyMarkupAsync(lastBlockMinedMessage.Chat, lastBlockMinedMessage.MessageId, null);
                                            }

                                            cachedTransaction.LastBlockMinedMessage = await bot.SendTextMessageAsync(message.Chat,
                                                $"⛏ New block `#{height}` was mined but your transaction didn't make it through, most likely because of the fees being too low.",
                                                ParseMode.Markdown,
                                                replyToMessageId: message.MessageId,
                                                replyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData(
                                                    "🔕 Mute block alerts", $"toggleBlockAlerts:{cachedTransaction.GetHashCode()}")));
                                        }

                                        cachedTransaction.LastBlockMined = height;
                                        newBlock = false;

                                        Cache.Update(cachedTransaction);
                                    }
                                    else
                                    {
                                        newBlock = true;
                                    }
                                }

                                if (transaction.Confirmations > 0)
                                {
                                    await bot.SendTextMessageAsync(message.Chat, new StringBuilder()
                                            .AppendLine($"⛏ New block `#{height}` was mined and your transaction just made it through.")
                                            .AppendLine()
                                            .AppendLine($"⏳ {confirmations - 1} more until it reaches {confirmations} confirmations...")
                                            .ToString(),
                                        ParseMode.Markdown,
                                        replyToMessageId: message.MessageId);

                                    oneConfirmation = true;
                                }
                            }
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }
                catch
                {
                    // ignored
                }
                finally
                {
                    double delaySeconds = 0;

                    if (CryptoApi.MaxRequestsPerHour != 0)
                    {
                        double maxRequestsPerMinute = CryptoApi.MaxRequestsPerHour - CryptoApi.MaxRequestsPerHour * 0.10; // -10% for safety
                        maxRequestsPerMinute /= 2; // /2 because we send up to 2 requests for each transaction (TODO: Make this dynamic)

                        delaySeconds = 60 / (maxRequestsPerMinute / _currentlyMonitoredTransactions);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, delaySeconds)));
                }
            }

            _currentlyMonitoredTransactions--;
            Cache.Remove(cachedTransaction);

            if (lastBlockMinedMessage != null)
            {
                // ReSharper disable once RedundantArgumentDefaultValue
                await bot.EditMessageReplyMarkupAsync(lastBlockMinedMessage.Chat, lastBlockMinedMessage.MessageId, null);
            }
        }
    }
}
