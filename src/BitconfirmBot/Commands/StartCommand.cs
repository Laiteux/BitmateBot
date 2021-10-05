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

            var text = new StringBuilder()
                .AppendLine($"👋 Hi {(groupAdd ? "there" : message.From.FirstName)}, I'm @{Program.BotUsername} and I can help you keep track of your crypto transaction confirmations!")
                .AppendLine()
                .AppendLine("You can use the /bitconfirm command so I can start tracking your transaction and notify you when it confirms.")
                .AppendLine()
                .AppendLine("Or just send me your raw transaction hash and I will attempt to auto-detect which network it belongs to.")
                .AppendLine()
                .AppendLine("Tip: You can also append a number at the end of your message to specify after how many confirmations you want to be notified.");

            if (!groupAdd)
            {
                text.AppendLine()
                    .AppendLine("Psst, I work in groups too! Add me in the middle of a deal and I'll be happy to help with tracking a payment 😉");
            }

            await bot.SendTextMessageAsync(message.Chat, text.ToString());
        }
    }
}
