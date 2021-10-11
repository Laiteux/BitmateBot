namespace Bitmate.Services.Crypto.Mempool.Responses
{
    /// <summary>
    /// Not complete
    /// </summary>
    public class Transaction
    {
        public string Txid { get; set; }

        public TransactionStatus Status { get; set; }
    }

    public class TransactionStatus
    {
        public bool Confirmed { get; set; }

        public long BlockHeight { get; set; }
    }
}
