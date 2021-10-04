namespace BitconfirmBot.Services.SoChain.Responses
{
    public class ResponseBase<T>
    {
        public string Status { get; set; }

        public T Data { get; set; }

        public bool IsSuccessful() => Status == "success";
    }
}
