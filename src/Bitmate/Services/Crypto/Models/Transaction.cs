namespace BitconfirmBot.Services.Crypto.Models
{
    public class Transaction
    {
        public bool Found { get; set; }

        public long Confirmations { get; set; }

        public bool DoubleSpent { get; set; }

        public string DoubleSpentTxId { get; set; }
    }
}
