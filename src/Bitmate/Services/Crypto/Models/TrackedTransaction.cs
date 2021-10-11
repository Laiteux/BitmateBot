namespace Bitmate.Services.Crypto.Models
{
    public class TrackedTransaction
    {
        public bool Found { get; set; }

        public long Confirmations { get; set; }

        public bool DoubleSpent { get; set; }

        public string DoubleSpentTxId { get; set; }
    }
}
