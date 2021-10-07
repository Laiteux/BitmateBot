using System.Collections.Generic;
using BitconfirmBot.Services.Cache;
using BitconfirmBot.Services.Crypto;
using Telegram.Bot;

namespace BitconfirmBot.Models
{
    public class Data
    {
        public ITelegramBotClient Bot { get; set; }

        public string BotUsername { get; set; }

        public Settings Settings { get; set; }

        public CacheService Cache { get; set; }

        public CryptoApi Api { get; set; }

        public List<Proxy> Proxies { get; set; }
    }
}
