using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Bitmate.Models;
using Bitmate.Services.Crypto.Exceptions;
using Bitmate.Services.Crypto.Mempool.Models;
using Bitmate.Services.Crypto.Mempool.Responses;
using Bitmate.Services.Crypto.Models;
using Bitmate.Utilities;
using Bitmate.Utilities.Json;

namespace Bitmate.Services.Crypto.Mempool
{
    public class MempoolCryptoApi : CryptoApi, IDisposable
    {
        public override string[] MainBlockchains { get; } = { "main" };

        public override string[] TestBlockchains { get; } = { "test" };

        public override string FormatBlockchainName(string name) => "BTC";

        private Events Events => _ws.Events;

        private TrackedTransaction _trackedTransaction;
        private long _height;

        private readonly CircularList<Proxy> _proxies;
        private MempoolWebSocket _ws;

        public MempoolCryptoApi(List<Proxy> proxies = null) : base(proxies)
        {
            _proxies = proxies == null ? null : new CircularList<Proxy>(proxies);
        }

        private static bool IsTestBlockchain(string blockchain) => blockchain == "test";

        private static Uri GetBaseAddress(string blockchain)
        {
            string baseAddress = "https://mempool.space/";

            if (IsTestBlockchain(blockchain))
                baseAddress += "testnet/";

            baseAddress += "api/";

            return new Uri(baseAddress);
        }

        private async Task StartWebSocketAsync(string blockchain)
        {
            if (_ws == null)
            {
                _ws = new MempoolWebSocket(_proxies, IsTestBlockchain(blockchain));
                await _ws.StartAsync();
            }
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
            await StartWebSocketAsync(blockchain);

            if (_trackedTransaction is not { Found: true })
            {
                _trackedTransaction = new();

                Transaction transaction;

                try
                {
                    HttpRequestMessage RequestMessage() => new(HttpMethod.Get, GetBaseAddress(blockchain) + $"tx/{txid}");

                    transaction = await GetResponseAsync<Transaction>(RequestMessage);
                }
                catch (EntityNotFoundException)
                {
                    return _trackedTransaction;
                }

                _trackedTransaction.Found = true;

                if (transaction.Status.Confirmed)
                {
                    long height = await GetBlockchainHeightAsync(blockchain);

                    _trackedTransaction.Confirmations = height - transaction.Status.BlockHeight + 1;
                }

                await _ws.TrackTxAsync(txid);

                if (_trackedTransaction.Confirmations == 0)
                {
                    Events.TxConfirmed += _ =>
                    {
                        _trackedTransaction.Confirmations++;

                        if (_height > 0)
                        {
                            _height++;
                        }
                    };
                }

                Events.BlockMined += _ =>
                {
                    if (_trackedTransaction.Confirmations > 0)
                    {
                        _trackedTransaction.Confirmations++;
                    }

                    if (_height > 0)
                    {
                        _height++;
                    }
                };

                Events.TxReplaced += newTx =>
                {
                    _trackedTransaction.DoubleSpent = true;
                    _trackedTransaction.DoubleSpentTxId = newTx;
                };
            }

            // Must create a copy because the original object could be modified at any time due to events
            return new TrackedTransaction()
            {
                Found = _trackedTransaction.Found,
                Confirmations = _trackedTransaction.Confirmations,
                DoubleSpent = _trackedTransaction.DoubleSpent,
                DoubleSpentTxId = _trackedTransaction.DoubleSpentTxId
            };
        }

        public override async Task<long> GetBlockchainHeightAsync(string blockchain)
        {
            if (_height == 0)
            {
                HttpRequestMessage RequestMessage() => new(HttpMethod.Get, GetBaseAddress(blockchain) + "blocks");

                var blocks = await GetResponseAsync<List<Block>>(RequestMessage);

                _height = blocks.First().Height;
            }

            return _height;
        }

        public override void Dispose()
        {
            base.Dispose();
            _ws.Dispose();
        }
    }
}
