using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Bitmate.Models;
using Bitmate.Services.Crypto.Exceptions;
using Bitmate.Services.Crypto.Mempool.Responses;
using Bitmate.Services.Crypto.Models;
using Bitmate.Utilities.Json;

namespace Bitmate.Services.Crypto.Mempool
{
    public class MempoolCryptoApi : CryptoApi
    {
        public override string[] MainBlockchains { get; } = { "main" };

        public override string[] TestBlockchains { get; } = { "test" };

        public override string FormatBlockchainName(string name) => "BTC";

        private static bool IsTestBlockchain(string blockchain) => blockchain == "test";

        /// <summary>
        /// <see href="https://github.com/mempool/mempool/blob/07ba2f6ecc103aadef806b3f367c3edd6e235b09/nginx.conf#L68"/>
        /// </summary>
        public override int MaxRequestsPerHour { get; } = 200;

        public MempoolCryptoApi(HttpClient httpClient = null) : base(httpClient) { }

        public MempoolCryptoApi(List<Proxy> proxies) : base(proxies) { }

        private static Uri GetBaseAddress(string blockchain)
        {
            string baseAddress = "https://mempool.space/";

            if (IsTestBlockchain(blockchain))
                baseAddress += "testnet/";

            baseAddress += "api/";

            return new Uri(baseAddress);
        }

        private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
        {
            PropertyNamingPolicy = new SnakeCaseJsonNamingPolicy()
        };

        private async Task<T> GetResponseAsync<T>(Func<HttpRequestMessage> requestMessage)
        {
            using var responseMessage = await HttpClient.SendAsync(requestMessage.Invoke());

            if (responseMessage.StatusCode == HttpStatusCode.NotFound)
            {
                throw new EntityNotFoundException();
            }

            var responseString = await responseMessage.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<T>(responseString, _jsonSerializerOptions);
        }

        public override async Task<TrackedTransaction> GetTransactionAsync(string blockchain, string txid)
        {
            try
            {
                HttpRequestMessage RequestMessage() => new(HttpMethod.Get, GetBaseAddress(blockchain) + $"tx/{txid}");

                var transaction = await GetResponseAsync<Transaction>(RequestMessage);

                long height = await GetBlockchainHeightAsync(blockchain);

                return new TrackedTransaction()
                {
                    Found = true,
                    Confirmations = height - transaction.Status.BlockHeight + 1
                };
            }
            catch (EntityNotFoundException)
            {
                return new TrackedTransaction()
                {
                    Found = false
                };
            }
        }

        public override async Task<long> GetBlockchainHeightAsync(string blockchain)
        {
            HttpRequestMessage RequestMessage() => new(HttpMethod.Get, GetBaseAddress(blockchain) + "blocks");

            var blocks = await GetResponseAsync<List<Block>>(RequestMessage);

            return blocks.First().Height;
        }
    }
}
