using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace BitconfirmBot.Commands
{
    public abstract class Command
    {
        public string Name { get; }

        protected virtual Dictionary<string, bool> Args { get; } = null;

        protected Command(string name)
        {
            Name = name;
        }

        public async Task HandleAsync(ITelegramBotClient bot, Message message, string[] args)
        {
            if (Args != null && args.Length < Args.Count(arg => arg.Value))
            {
                await SendUsageAsync(bot, message.Chat);

                return;
            }

            await ExecuteAsync(bot, message, args);
        }

        private async Task SendUsageAsync(ITelegramBotClient bot, ChatId chatId)
        {
            await bot.SendTextMessageAsync(chatId, $"Usage: `/{Name} {string.Join(' ', Args.Select(arg => (arg.Value ? "<" : "[") + arg.Key + (arg.Value ? ">" : "]")))}`", ParseMode.Markdown);
        }

        protected abstract Task ExecuteAsync(ITelegramBotClient bot, Message message, string[] args);
    }
}
