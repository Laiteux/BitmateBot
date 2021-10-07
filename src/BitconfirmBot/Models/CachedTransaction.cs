using Telegram.Bot.Types;

namespace BitconfirmBot.Models
{
    public class CachedTransaction
    {
        public CachedTransaction(string api, string blockchain, string txid, long confirmations, Message message)
        {
            Api = api;
            Blockchain = blockchain;
            TxId = txid;
            Confirmations = confirmations;
            Message = message;
        }

        public string Api { get; set; }

        public string Blockchain { get; set; }

        public string TxId { get; set; }

        public long Confirmations { get; set; }

        public Message Message { get; set; }
    }
}
