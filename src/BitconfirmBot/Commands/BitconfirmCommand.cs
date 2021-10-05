using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitconfirmBot.Extensions;
using BitconfirmBot.Services.SoChain;
using BitconfirmBot.Services.SoChain.Responses;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace BitconfirmBot.Commands
{
    public class BitconfirmCommand : Command
    {
        public BitconfirmCommand() : base("bitconfirm")
        {
        }

        protected override Dictionary<string, bool> Args { get; } = new()
        {
            { "NETWORK", true },
            { "TXID", true },
            { "confirmations", false }
        };

        /// <summary>
        /// <see href="https://chain.so/api/#networks-supported"/>
        /// </summary>
        public static readonly string[] SupportedNetworks = { "BTC", "LTC", "DOGE", "DASH", "ZEC" };

        public const string AutoNetwork = "-AUTO-";

        private static readonly SoChainService _soChain = new();

        private static int _currentlyMonitoredTransactions;

        protected override async Task ExecuteAsync(ITelegramBotClient bot, Message message, string[] args)
        {
            string network = args[0].ToUpper();
            string txid = args[1];
            int confirmations = args.Length < 3 ? 1 : int.Parse(args[2]);

            ResponseBase<TxConfirmationInfoResponse> txConfirmationInfo = null;

            if (network == AutoNetwork)
            {
                foreach (string supportedNetwork in SupportedNetworks)
                {
                    txConfirmationInfo = await _soChain.GetTxConfirmationInfoAsync(supportedNetwork, txid);

                    if (txConfirmationInfo.IsSuccessful())
                    {
                        network = supportedNetwork;

                        break;
                    }
                }

                if (network == AutoNetwork)
                {
                    await bot.SendTextMessageAsync(message.Chat, new StringBuilder()
                        .AppendLine("😓 I was unable to auto-detect what network this transaction belongs to.")
                        .AppendLine()
                        .AppendLine($"👉 Please use the /{Name} command and specify it yourself.")
                        .ToString());

                    await SendUsageAsync(bot, message.Chat);

                    return;
                }
            }
            else if (!SupportedNetworks.Contains(network.TrimEnd("TEST")))
            {
                await bot.SendTextMessageAsync(message.Chat, new StringBuilder()
                    .AppendLine("💡 List of supported networks:")
                    .AppendLine()
                    .AppendJoin("\n", SupportedNetworks.Select(n => $"- {n} / {n}TEST"))
                    .ToString());

                return;
            }

            const int maxConfirmations = 100;

            if (confirmations is < 1 or > maxConfirmations)
            {
                await bot.SendTextMessageAsync(message.Chat, $"❌ Confirmation count can not exceed {maxConfirmations}.");

                return;
            }

            txConfirmationInfo ??= await _soChain.GetTxConfirmationInfoAsync(network, txid);

            bool confirmed = txConfirmationInfo.Data.Confirmations > 0;

            if (txConfirmationInfo.IsSuccessful())
            {
                if (txConfirmationInfo.Data.Confirmations >= confirmations)
                {
                    await bot.SendTextMessageAsync(message.Chat, $"⚠️ Your transaction has already hit {txConfirmationInfo.Data.Confirmations} {"confirmation".Pluralize(txConfirmationInfo.Data.Confirmations)}.");

                    return;
                }

                await bot.SendTextMessageAsync(message.Chat, $"🔔 Ok, I will let you know you when your transaction hits {confirmations} {"confirmation".Pluralize(confirmations)}.");
            }
            else
            {
                await bot.SendTextMessageAsync(message.Chat, "❌ An error occurred when trying to fetch your transaction, please try again.");

                return;
            }

            var networkInfo = await _soChain.GetNetworkInfoAsync(network);

            long lastBlock = networkInfo.Data.Blocks;
            bool newBlock = false;

            _currentlyMonitoredTransactions++;

            while (true)
            {
                try
                {
                    txConfirmationInfo = await _soChain.GetTxConfirmationInfoAsync(network, txid);

                    if (txConfirmationInfo.Data.Confirmations >= confirmations)
                    {
                        await bot.SendTextMessageAsync(message.Chat,
                            $"✅ Your transaction just hit {txConfirmationInfo.Data.Confirmations} {"confirmation".Pluralize(txConfirmationInfo.Data.Confirmations)}!",
                            replyToMessageId: message.MessageId);

                        break;
                    }

                    if (!confirmed)
                    {
                        networkInfo = await _soChain.GetNetworkInfoAsync(network);

                        if (networkInfo.Data.Blocks > lastBlock)
                        {
                            if (newBlock)
                            {
                                await bot.SendTextMessageAsync(message.Chat,
                                    $"⛏ New block #{lastBlock} was mined but your transaction didn't make it through.",
                                    replyToMessageId: message.MessageId);

                                lastBlock = networkInfo.Data.Blocks;
                                newBlock = false;
                            }
                            else
                            {
                                newBlock = true;
                            }
                        }

                        if (txConfirmationInfo.Data.Confirmations > 0)
                        {
                            await bot.SendTextMessageAsync(message.Chat, new StringBuilder()
                                    .AppendLine($"⛏ New block #{networkInfo.Data.Blocks} was mined and your transaction just made it through.")
                                    .AppendLine()
                                    .AppendLine($"⏳ {confirmations - 1} more until it hits {confirmations} confirmations...")
                                    .ToString(),
                                replyToMessageId: message.MessageId);

                            confirmed = true;
                        }
                    }
                }
                catch
                {
                    // Ignore
                }
                finally
                {
                    // https://chain.so/api/#rate-limits
                    const double maxRequestsPerMinute = (double)(300 - 30) / 2; // -30 (-10%) for safety, /2 because we send up to 2 requests for each transaction

                    double delaySeconds = 60 / (maxRequestsPerMinute / _currentlyMonitoredTransactions);

                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, delaySeconds)));
                }
            }

            _currentlyMonitoredTransactions--;
        }
    }
}
