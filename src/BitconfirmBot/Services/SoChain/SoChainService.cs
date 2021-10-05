using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using BitconfirmBot.Services.SoChain.Responses;
using BitconfirmBot.Utilities;

namespace BitconfirmBot.Services.SoChain
{
    public class SoChainService
    {
        private readonly HttpClient _httpClient;

        public SoChainService(HttpClient httpClient = null)
        {
            _httpClient = httpClient ?? new();
            _httpClient.BaseAddress = new("https://chain.so/api/v2/");
        }

        private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
        {
            PropertyNamingPolicy = new SnakeCaseJsonNamingPolicy()
        };

        private async Task<ResponseBase<T>> GetResponseAsync<T>(Func<HttpRequestMessage> requestMessage)
        {
            using var responseMessage = await _httpClient.SendAsync(requestMessage.Invoke());

            if (responseMessage.StatusCode == HttpStatusCode.InternalServerError)
            {
                return new ResponseBase<T>()
                {
                    Status = responseMessage.StatusCode.ToString()
                };
            }

            var responseString = await responseMessage.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<ResponseBase<T>>(responseString, _jsonSerializerOptions);
        }

        public async Task<ResponseBase<TxConfirmationInfoResponse>> GetTxConfirmationInfoAsync(string network, string txid)
        {
            HttpRequestMessage RequestMessage() => new(HttpMethod.Get, $"is_tx_confirmed/{network}/{txid}");

            return await GetResponseAsync<TxConfirmationInfoResponse>(RequestMessage);
        }

        public async Task<ResponseBase<NetworkInfoResponse>> GetNetworkInfoAsync(string network)
        {
            HttpRequestMessage RequestMessage() => new(HttpMethod.Get, $"get_info/{network}");

            return await GetResponseAsync<NetworkInfoResponse>(RequestMessage);
        }
    }
}
