using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Tasks;
using Bitmate.Models;
using Bitmate.Services.Crypto.Mempool.Models;
using Bitmate.Services.Crypto.Mempool.Responses;
using Bitmate.Utilities;
using Websocket.Client;

namespace Bitmate.Services.Crypto.Mempool
{
    public class MempoolWebSocket : IDisposable
    {
        public Events Events { get; } = new();

        private static readonly Uri Mainnet = new("wss://mempool.space/api/v1/ws");
        private static readonly Uri Testnet = new("wss://mempool.space/testnet/api/v1/ws");

        private readonly WebsocketClient _ws;
        private bool _disposed;

        public MempoolWebSocket(bool testnet = false) : this(null, testnet) { }

        public MempoolWebSocket(CircularList<Proxy> proxies, bool testnet = false)
        {
            _ws = new WebsocketClient(testnet ? Testnet : Mainnet, () => new ClientWebSocket()
            {
                Options =
                {
                    Proxy = proxies?.Next().ToWebProxy()
                }
            })
            {
                ErrorReconnectTimeout = TimeSpan.Zero
            };
        }

        private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public async Task StartAsync()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GetType));

            await _ws.Start();

            _ws.MessageReceived.Subscribe(MessageReceived);

            // Keepalive
            _ = Task.Run(async () =>
            {
                while (!_disposed)
                {
                    await Task.Delay(TimeSpan.FromSeconds(30));

                    _ = PingAsync();
                }
            });
        }

        private void MessageReceived(ResponseMessage message)
        {
            var json = JsonSerializer.Deserialize<JsonElement>(message.Text, _jsonSerializerOptions);

            if (json.TryGetProperty("block", out var jsonBlock))
            {
                var block = JsonSerializer.Deserialize<Block>(jsonBlock.GetRawText(), _jsonSerializerOptions);

                if (json.TryGetProperty("txConfirmed", out _))
                {
                    Events.TxConfirmed?.Invoke(block);
                }
                else // On purpose, to avoid 2 events for the same block
                {
                    Events.BlockMined?.Invoke(block);
                }
            }
            else if (json.TryGetProperty("address-transactions", out var transactions))
            {
                foreach (var transaction in transactions.EnumerateArray())
                {
                    Events.TxReceived?.Invoke(JsonSerializer.Deserialize<AddressTransaction>(transaction.GetRawText(), _jsonSerializerOptions));
                }
            }
            else if (json.TryGetProperty("rbfTransaction", out var rbfTransaction))
            {
                string txid = rbfTransaction.GetProperty("txid").GetString();

                Events.TxReplaced?.Invoke(txid);
            }
        }

        private void ThrowIfNotStarted()
        {
            if (!_ws.IsStarted) throw new InvalidOperationException($"Please call {nameof(StartAsync)} first.");
        }

        private async Task PingAsync()
        {
            ThrowIfNotStarted();

            await _ws.SendInstant(JsonSerializer.Serialize(new
            {
                action = "ping"
            }));
        }

        public async Task TrackBlocksAsync()
        {
            ThrowIfNotStarted();

            await _ws.SendInstant(JsonSerializer.Serialize(new
            {
                action = "want",
                data = new[] { "blocks" }
            }));
        }

        public async Task TrackTxAsync(string txid)
        {
            ThrowIfNotStarted();
            await TrackBlocksAsync(); // Required

            await _ws.SendInstant(JsonSerializer.Serialize(new Dictionary<string, string>()
            {
                { "track-tx", txid }
            }));
        }

        public async Task TrackAddressAsync(string address)
        {
            ThrowIfNotStarted();

            await _ws.SendInstant(JsonSerializer.Serialize(new Dictionary<string, string>()
            {
                { "track-address", address }
            }));
        }

        public void Dispose()
        {
            _ws.Dispose();
            _disposed = true;
        }
    }
}
