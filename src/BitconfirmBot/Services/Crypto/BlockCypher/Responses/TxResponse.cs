namespace BitconfirmBot.Services.Crypto.BlockCypher.Responses
{
    /// <summary>
    /// Not complete<br/>
    /// <see href="https://www.blockcypher.com/dev/bitcoin/#tx"/>
    /// </summary>
    public class TxResponse
    {
        public long Confirmations { get; set; }

        public bool DoubleSpend { get; set; }

        public string DoubleSpendTx { get; set; }
    }
}
