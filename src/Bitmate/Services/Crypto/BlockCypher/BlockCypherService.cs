using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Bitmate.Models;
using Bitmate.Services.Crypto.BlockCypher.Exceptions;
using Bitmate.Services.Crypto.BlockCypher.Responses;
using Bitmate.Services.Crypto.Models;
using Bitmate.Utilities.Json;

namespace Bitmate.Services.Crypto.BlockCypher
{
    public class BlockCypherService : CryptoApi
    {
        /// <summary>
        /// <see href="https://www.blockcypher.com/dev/bitcoin/#restful-resources"/>
        /// </summary>
        public override string[] MainBlockchains { get; } =
        {
            "btc/main", "eth/main", "ltc/main", "doge/main", "dash/main"
        };

        /// <summary>
        /// <inheritdoc cref="MainBlockchains"/>
        /// </summary>
        public override string[] TestBlockchains { get; } =
        {
            "btc/test3"
        };

        /// <summary>
        /// <see href="https://www.blockcypher.com/dev/bitcoin/#rate-limits-and-tokens"/><br/>
        /// <see href="https://www.blockcypher.com/dev/ethereum/#rate-limits-and-tokens"/>
        /// </summary>
        public override int MaxRequestsPerHour { get; } = 200;

        protected override Uri BaseAddress { get; } = new("https://api.blockcypher.com/v1/");

        public BlockCypherService(HttpClient httpClient = null) : base(httpClient)
        {
            Console.WriteLine("[!] It is strongly advised to use proxies with the BlockCypher API");
        }

        public BlockCypherService(List<Proxy> proxies) : base(proxies) { }

        public override string FormatBlockchainName(string name) => name.Split('/')[0].ToUpper();

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

        public override async Task<Transaction> GetTransactionAsync(string blockchain, string txid)
        {
            HttpRequestMessage RequestMessage() => new(HttpMethod.Get, $"{blockchain}/txs/{txid}");

            try
            {
                var tx = await GetResponseAsync<TxResponse>(RequestMessage);

                return new Transaction()
                {
                    Found = true,
                    Confirmations = tx.Confirmations,
                    DoubleSpent = tx.DoubleSpend,
                    DoubleSpentTxId = tx.DoubleSpendTx
                };
            }
            catch (EntityNotFoundException)
            {
                return new Transaction()
                {
                    Found = false
                };
            }
        }

        public override async Task<long> GetBlockchainHeightAsync(string blockchain)
        {
            HttpRequestMessage RequestMessage() => new(HttpMethod.Get, blockchain);

            var blockchainInfo = await GetResponseAsync<BlockchainResponse>(RequestMessage);

            return blockchainInfo.Height;
        }
    }
}
