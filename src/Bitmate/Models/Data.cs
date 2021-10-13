using System;
using System.Collections.Generic;
using Bitmate.Services.Cache;
using Bitmate.Services.Crypto;
using Telegram.Bot;

namespace Bitmate.Models
{
    public class Data
    {
        public ITelegramBotClient Bot { get; set; }

        public string BotUsername { get; set; }

        public Settings Settings { get; set; }

        public CacheService Cache { get; set; }

        public Func<CryptoApi> FuncApi { get; set; }

        public List<Proxy> Proxies { get; set; }
    }
}
