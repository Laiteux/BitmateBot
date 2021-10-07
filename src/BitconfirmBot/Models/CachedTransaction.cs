using System;
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

        public string Api { get; }

        public string Blockchain { get; }

        public string TxId { get; }

        public long Confirmations { get; }

        public Message Message { get; }

        public override bool Equals(object obj)
        {
            if (obj is not CachedTransaction transaction)
                return false;

            return
                Api == transaction.Api &&
                Blockchain == transaction.Blockchain &&
                TxId == transaction.TxId &&
                Confirmations == transaction.Confirmations &&
                Message.Chat.Id == transaction.Message.Chat.Id &&
                Message.From.Id == transaction.Message.From.Id;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Api, Blockchain, TxId, Confirmations, Message);
        }
    }
}
