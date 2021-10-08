using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using BitconfirmBot.Models;
using BitconfirmBot.Services.Crypto.Models;
using BitconfirmBot.Utilities;

namespace BitconfirmBot.Services.Crypto
{
    public abstract class CryptoApi
    {
        /// <summary>
        /// By descending priority order to try and make auto detection as fast as possible
        /// </summary>
        public abstract string[] SupportedBlockchains { get; }

        public bool IsEthBlockchainSupported => GetFormattedSupportedBlockchains().Contains("ETH");

        /// <summary>
        /// Set to <c>0</c> to disable
        /// </summary>
        public virtual int MaxRequestsPerHour { get; }

        protected virtual Uri BaseAddress { get; }

        private readonly CircularList<HttpClient> _httpClients;

        protected HttpClient HttpClient => _httpClients.Next();
        
        protected CryptoApi(HttpClient httpClient = null)
        {
            httpClient ??= new();

            // ReSharper disable once VirtualMemberCallInConstructor
            httpClient.BaseAddress = BaseAddress;

            _httpClients = new CircularList<HttpClient>(new[] { httpClient });
        }

        protected CryptoApi(IEnumerable<Proxy> proxies)
        {
            if (!proxies.Any())
            {
                throw new Exception("This API requires proxies, please load some.");
            }

            var httpClients = proxies.Select(p => p.GetHttpClient()).ToList();
            httpClients.ForEach(h => h.BaseAddress = BaseAddress);

            _httpClients = new CircularList<HttpClient>(httpClients);

            MaxRequestsPerHour = 0;
        }

        public abstract Task<Transaction> GetTransactionAsync(string blockchain, string txid);

        public virtual Task<long> GetBlockchainHeightAsync(string blockchain)
            => throw new NotSupportedException();

        public static string FormatBlockchainName(string name)
            => name.Split('/')[0].ToUpper();

        public IEnumerable<string> GetFormattedSupportedBlockchains()
            => SupportedBlockchains.Select(FormatBlockchainName);
    }
}
