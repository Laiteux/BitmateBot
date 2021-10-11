using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Bitmate.Extensions;
using Bitmate.Models;
using Bitmate.Services.Crypto.Models;
using Bitmate.Services.Crypto.SoChain.Responses;
using Bitmate.Utilities.Json;

namespace Bitmate.Services.Crypto.SoChain
{
    public class SoChainCryptoApi : CryptoApi
    {
        /// <summary>
        /// <see href="https://chain.so/api/#networks-supported"/>
        /// </summary>
        public override string[] MainBlockchains { get; } =
        {
            "BTC", "LTC", "DOGE", "DASH", "ZEC"
        };

        /// <summary>
        /// <inheritdoc cref="MainBlockchains"/>
        /// </summary>
        public override string[] TestBlockchains { get; } =
        {
            "BTCTEST", "LTCTEST", "DOGETEST", "DASHTEST", "ZECTEST"
        };

        /// <summary>
        /// <see href="https://chain.so/api/#rate-limits"/>
        /// </summary>
        public override int MaxRequestsPerHour { get; } = 300;

        protected override Uri BaseAddress { get; } = new("https://chain.so/api/v2/");

        public SoChainCryptoApi(HttpClient httpClient = null) : base(httpClient) { }

        public SoChainCryptoApi(List<Proxy> proxies) : base(proxies) { }

        public override string FormatBlockchainName(string name) => name.TrimEnd("TEST");

        private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
        {
            PropertyNamingPolicy = new SnakeCaseJsonNamingPolicy()
        };

        private async Task<ResponseBase<T>> GetResponseAsync<T>(Func<HttpRequestMessage> requestMessage)
        {
            using var responseMessage = await HttpClient.SendAsync(requestMessage.Invoke());

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

        public override async Task<Transaction> GetTransactionAsync(string blockchain, string txid)
        {
            HttpRequestMessage RequestMessage() => new(HttpMethod.Get, $"is_tx_confirmed/{blockchain}/{txid}");

            var txConfirmationInfo = await GetResponseAsync<TxConfirmationInfoResponse>(RequestMessage);

            return new Transaction()
            {
                Found = txConfirmationInfo.IsSuccessful(),
                Confirmations = txConfirmationInfo.Data?.Confirmations ?? default
            };
        }

        public override async Task<long> GetBlockchainHeightAsync(string blockchain)
        {
            HttpRequestMessage RequestMessage() => new(HttpMethod.Get, $"get_info/{blockchain}");

            var blockchainInfo = await GetResponseAsync<NetworkInfoResponse>(RequestMessage);

            return blockchainInfo.Data.Blocks;
        }
    }
}
