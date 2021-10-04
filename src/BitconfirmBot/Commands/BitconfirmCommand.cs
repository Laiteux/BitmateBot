using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitconfirmBot.Extensions;
using BitconfirmBot.Services.SoChain;
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

        private static readonly SoChainService _soChain = new();

        /// <summary>
        /// <see href="https://chain.so/api/#networks-supported"/>
        /// </summary>
        private static readonly string[] _supportedNetworks = { "BTC", "DOGE", "LTC", "DASH", "ZEC" };

        protected override async Task ExecuteAsync(ITelegramBotClient bot, Message message, string[] args)
        {
            string network = args[0].ToUpper();
            string txid = args[1];
            int confirmations = args.Length < 3 ? 1 : int.Parse(args[2]);

            if (!_supportedNetworks.Contains(network.TrimEnd("TEST")))
            {
                await bot.SendTextMessageAsync(message.Chat, new StringBuilder()
                    .AppendLine("List of supported networks:")
                    .AppendLine()
                    .AppendJoin("\n", _supportedNetworks.Select(n => $"- {n} / {n}TEST"))
                    .ToString());

                return;
            }

            const int maxConfirmations = 100;

            if (confirmations is < 1 or > maxConfirmations)
            {
                await bot.SendTextMessageAsync(message.Chat, $"❌ Confirmation count can not exceed {maxConfirmations}.");

                return;
            }

            var isTxConfirmed = await _soChain.IsTxConfirmedAsync(network, txid);
            bool confirmed = isTxConfirmed.Data.Confirmations > 0;

            if (isTxConfirmed.IsSuccessful())
            {
                if (isTxConfirmed.Data.Confirmations >= confirmations)
                {
                    await bot.SendTextMessageAsync(message.Chat, $"⚠️ Your transaction has already reached {confirmations} {"confirmation".Pluralize(confirmations)}.");

                    return;
                }

                await bot.SendTextMessageAsync(message.Chat, $"🔔 Ok, I will notify you when your transaction hits {confirmations} {"confirmation".Pluralize(confirmations)}.");
            }
            else
            {
                await bot.SendTextMessageAsync(message.Chat, "❌ An error occurred when trying to fetch your transaction, please try again.");

                return;
            }

            var networkInfo = await _soChain.GetNetworkInfoAsync(network);

            long lastBlock = networkInfo.Data.Blocks;
            bool newBlock = false;

            while (true)
            {
                try
                {
                    isTxConfirmed = await _soChain.IsTxConfirmedAsync(network, txid);

                    if (isTxConfirmed.Data.Confirmations >= confirmations)
                    {
                        await bot.SendTextMessageAsync(message.Chat,
                            $"✅ Your transaction just hit {isTxConfirmed.Data.Confirmations} {"confirmation".Pluralize(isTxConfirmed.Data.Confirmations)}!",
                            replyToMessageId: message.MessageId);

                        break;
                    }

                    if (confirmations == 1)
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
                    }

                    if (isTxConfirmed.Data.Confirmations > 0 && !confirmed)
                    {
                        await bot.SendTextMessageAsync(message.Chat, new StringBuilder()
                                .AppendLine($"⛏ New block #{networkInfo.Data.Blocks} was mined and your transaction just made it through.")
                                .AppendLine()
                                .AppendLine($"⏳ {confirmations - 1} more until it hits {confirmations} confirmations!")
                                .ToString(),
                            replyToMessageId: message.MessageId);

                        confirmed = true;
                    }
                }
                catch
                {
                    // ignored
                }
                finally
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
        }
    }
}
