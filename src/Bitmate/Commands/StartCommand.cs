using System.Text;
using System.Threading.Tasks;
using Bitmate.Services.Crypto;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Bitmate.Commands
{
    public class StartCommand : Command
    {
        public StartCommand() : base("start")
        {
        }

        protected override async Task ExecuteAsync(ITelegramBotClient bot, CryptoApi api, Message message, string[] args)
        {
            bool groupAdd = message.Type == MessageType.ChatMembersAdded;

            string userName = groupAdd || (message.From.FirstName.Length == 1 && !char.IsLetterOrDigit(message.From.FirstName[0]))
                ? (groupAdd ? "mates" : "mate")
                : message.From.FirstName;

            await bot.SendTextMessageAsync(message.Chat, $"👋 Hey {userName}! I'm Bitmate and I will be your #1 crypto companion from now on.");

            await bot.SendTextMessageAsync(message.Chat, new StringBuilder()
                .AppendLine("*Here are a few of my features:*")
                .AppendLine()
                .AppendLine("✅ Confirmations tracking")
                .AppendLine("⛏ Mined blocks tracking")
                .AppendLine("🔄 Double-spend detection")
                .AppendLine("➕ And more!")
                .AppendLine()
                .AppendLine("*Coming soon:*")
                .AppendLine()
                .AppendLine("💰 Cryptocurrency prices")
                .AppendLine("💱 Currency conversion")
                .AppendLine("📊 Live recommended fees")
                .AppendLine("🔎 Smart inline mode")
                .ToString(), ParseMode.Markdown);

            await bot.SendTextMessageAsync(message.Chat, new StringBuilder()
                .AppendLine("🔗 Send me a transaction hash or URL to get started.")
                .AppendLine()
                .AppendLine("💡 Pro tip: You can also append a custom amount of confirmations.")
                .ToString());

            if (!groupAdd && message.Chat.Type == ChatType.Private)
            {
                await bot.SendTextMessageAsync(message.Chat, "👥 Psst, I also work in groups! Add me in the middle of a deal and I'll be happy to help with tracking a transaction.");
            }
        }
    }
}
