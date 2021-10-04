using System;

namespace BitconfirmBot.Services.SoChain.Responses
{
    public class NetworkInfoResponse
    {
        public string Name { get; set; }

        public string Acronym { get; set; }

        public string Network { get; set; }

        public string SymbolHtmlcode { get; set; }

        public Uri Url { get; set; }

        public string MiningDifficulty { get; set; }

        public long UnconfirmedTxs { get; set; }

        public long Blocks { get; set; }

        public string Price { get; set; }

        public string PriceBase { get; set; }

        public long PriceUpdateTime { get; set; }

        public string Hashrate { get; set; }
    }
}
