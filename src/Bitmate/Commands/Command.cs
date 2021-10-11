using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bitmate.Services.Crypto;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Bitmate.Commands
{
    public abstract class Command
    {
        public string[] Name { get; }

        protected virtual Dictionary<string, bool> Args { get; } = null;

        public virtual bool UseCryptoApi { get; } = false;

        protected Command(params string[] name)
        {
            Name = name;
        }

        public async Task HandleAsync(ITelegramBotClient bot, CryptoApi api, Message message, string[] args)
        {
            if (Args != null && args.Length < Args.Count(arg => arg.Value))
            {
                await SendUsageAsync(bot, message.Chat);

                return;
            }

            await ExecuteAsync(bot, api, message, args);
        }

        protected async Task SendUsageAsync(ITelegramBotClient bot, ChatId chatId)
        {
            await bot.SendTextMessageAsync(chatId, $"Usage: `/{Name.First()} {string.Join(' ', Args.Select(arg => (arg.Value ? "<" : "[") + arg.Key + (arg.Value ? ">" : "]")))}`", ParseMode.Markdown);
        }

        protected abstract Task ExecuteAsync(ITelegramBotClient bot, CryptoApi api, Message message, string[] args);
    }
}
