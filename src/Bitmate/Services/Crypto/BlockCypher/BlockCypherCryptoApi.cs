﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Bitmate.Models;
using Bitmate.Services.Crypto.BlockCypher.Responses;
using Bitmate.Services.Crypto.Exceptions;
using Bitmate.Services.Crypto.Models;
using Bitmate.Utilities.Json;

namespace Bitmate.Services.Crypto.BlockCypher
{
    public class BlockCypherCryptoApi : CryptoApi
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

        public override string FormatBlockchainName(string name) => name.Split('/')[0].ToUpper();

        /// <summary>
        /// <see href="https://www.blockcypher.com/dev/bitcoin/#rate-limits-and-tokens"/><br/>
        /// <see href="https://www.blockcypher.com/dev/ethereum/#rate-limits-and-tokens"/>
        /// </summary>
        public override int MaxRequestsPerHour { get; } = 200;

        protected override Uri BaseAddress { get; } = new("https://api.blockcypher.com/v1/");

        private const string HugeUpdateDelaysAlert = "The BlockCypher API has HUGE update delays, it is not advised to use it";

        public BlockCypherCryptoApi(HttpClient httpClient = null) : base(httpClient)
        {
            Console.WriteLine($"[!] {HugeUpdateDelaysAlert}");
        }

        public BlockCypherCryptoApi(List<Proxy> proxies) : base(proxies)
        {
            Console.WriteLine($"[!] {HugeUpdateDelaysAlert}");
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
            HttpRequestMessage RequestMessage() => new(HttpMethod.Get, $"{blockchain}/txs/{txid}");

            try
            {
                var tx = await GetResponseAsync<TxResponse>(RequestMessage);

                return new TrackedTransaction()
                {
                    Found = true,
                    Confirmations = tx.Confirmations,
                    DoubleSpent = tx.DoubleSpend,
                    DoubleSpentTxId = tx.DoubleSpendTx
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
            HttpRequestMessage RequestMessage() => new(HttpMethod.Get, blockchain);

            var blockchainInfo = await GetResponseAsync<BlockchainResponse>(RequestMessage);

            return blockchainInfo.Height;
        }
    }
}
