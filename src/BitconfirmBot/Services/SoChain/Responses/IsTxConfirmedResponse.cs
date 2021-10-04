namespace BitconfirmBot.Services.SoChain.Responses
{
    public class IsTxConfirmedResponse
    {
        public long Confirmations { get; set; }

        public bool IsConfirmed { get; set; }
    }
}
