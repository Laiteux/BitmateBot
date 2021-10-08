using System;
using System.Security.Cryptography;
using System.Text;
using Telegram.Bot.Types;

namespace Bitmate.Models
{
    public class CachedTransaction
    {
        public CachedTransaction(string api, string blockchain, string txid, long confirmations, Message message)
        {
            Api = api.ToLower();
            Blockchain = blockchain;
            TxId = txid;
            Confirmations = confirmations;
            Message = message;
        }

        public string Api { get; }

        public string Blockchain { get; }

        public string TxId { get; }

        public long Confirmations { get; }

        public long LastBlockMined { get; set; }

        public bool BlockAlertsMuted { get; set; }

        public Message Message { get; }

        public Message LastBlockMinedMessage { get; set; }

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

        private static readonly MD5 _md5 = MD5.Create();

        /// <summary>
        /// <see href="https://stackoverflow.com/a/26870764/7854126"/>
        /// </summary>
        public override int GetHashCode()
        {
            var plain = string.Concat(Api, Blockchain, TxId, Confirmations, Message.MessageId);

            byte[] hash = _md5.ComputeHash(Encoding.UTF8.GetBytes(plain));

            return BitConverter.ToInt32(hash);

            // Can't use this because we need a deterministic value that remains the same between different program runs
            // See https://andrewlock.net/why-is-string-gethashcode-different-each-time-i-run-my-program-in-net-core/
            // return HashCode.Combine(Api, Blockchain, TxId, Confirmations, Message.MessageId);
        }
    }
}
