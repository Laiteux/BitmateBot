using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace BitconfirmBot.Commands
{
    public class StartCommand : Command
    {
        public StartCommand() : base("start")
        {
        }

        protected override async Task ExecuteAsync(ITelegramBotClient bot, Message message, string[] args)
        {
            bool groupAdd = message.Type == MessageType.ChatMembersAdded;

            string userName = groupAdd || (message.From.FirstName.Length == 1 && !char.IsLetterOrDigit(message.From.FirstName[0]))
                ? (groupAdd ? "mates" : "mate")
                : message.From.FirstName;

            await bot.SendTextMessageAsync(message.Chat, $"👋 Hey {userName}! I'm Bitmate and I will be your #1 crypto companion from now on.");

            await bot.SendTextMessageAsync(message.Chat, new StringBuilder()
                .AppendLine("*Here are a few things I can do for you:*")
                .AppendLine()
                .AppendLine("✅ Track your transaction confirmations")
                .AppendLine("⛏ Notify you about mined blocks")
                .AppendLine("🔄 Let you know on double-spend attempts")
                .AppendLine("➕ And more!")
                .AppendLine()
                .AppendLine("*Coming soon:*")
                .AppendLine()
                .AppendLine("💱 Currency conversion")
                .AppendLine("💰 Cryptocurrency prices")
                .AppendLine("📊 Live recommended fees")
                .AppendLine("🔎 Smart inline mode")
                .ToString(), ParseMode.Markdown);

            await bot.SendTextMessageAsync(message.Chat, "🔗 Send me a transaction hash or URL to get started.");

            if (!groupAdd)
            {
                await bot.SendTextMessageAsync(message.Chat, "👥 Psst, I also work in groups! Add me in the middle of a deal and I'll be happy to help with tracking a transaction.");
            }
        }
    }
}
