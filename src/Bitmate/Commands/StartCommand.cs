using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        protected override async Task ExecuteAsync(ITelegramBotClient bot, Message message, string[] args)
        {
            bool groupAdd = message.Type == MessageType.ChatMembersAdded;

            // That's just something I did so users with a first name that doesn't contain a letter or digit
            // get called "mate" instead, because I think it would look weird for it to be like "👋 Hey ^$#!",
            // just a personal preference and doesn't really make any difference if we're being honest but eh
            string userName = groupAdd || !message.From.FirstName.Any(char.IsLetterOrDigit)
                ? groupAdd ? "mates" : "mate"
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
                .AppendLine("🔗 Send me a transaction hash or URL to get started!")
                .AppendLine()
                .AppendLine("💡 Pro tip: You can also append a custom amount of confirmations.")
                .ToString());

            if (message.Chat.Type == ChatType.Private)
            {
                await bot.SendTextMessageAsync(message.Chat, "👥 Psst, I also work in groups! Add me in the middle of a deal and I'll be happy to help with tracking a transaction.");
            }
        }
    }
}
