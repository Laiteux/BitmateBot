using Telegram.Bot.Types;

namespace BitconfirmBot.Models
{
    public class Transaction
    {
        public Transaction(string network, string txid, int confirmations, Message message)
        {
            Network = network;
            TxId = txid;
            Confirmations = confirmations;
            Message = message;
        }

        public string Network { get; set; }

        public string TxId { get; set; }

        public int Confirmations { get; set; }

        public Message Message { get; set; }
    }
}
