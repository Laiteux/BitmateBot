using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Bitmate.Models;
using Bitmate.Services.Crypto.Models;
using Bitmate.Utilities;

namespace Bitmate.Services.Crypto
{
    public abstract class CryptoApi
    {
        /// <summary>
        /// By descending priority order to try and make auto detection as fast as possible
        /// </summary>
        public abstract string[] MainBlockchains { get; }

        /// <summary>
        /// <inheritdoc cref="MainBlockchains"/>
        /// </summary>
        public virtual string[] TestBlockchains { get; }

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

        public abstract Task<TrackedTransaction> GetTransactionAsync(string blockchain, string txid);

        public virtual Task<long> GetBlockchainHeightAsync(string blockchain)
            => throw new NotSupportedException();

        public virtual string FormatBlockchainName(string name) => name;

        public IEnumerable<string> GetFormattedBlockchains(bool test = false)
            => (test ? TestBlockchains : MainBlockchains).Select(FormatBlockchainName);

        public string BuildSupportedBlockchainsMessage(string error = null)
        {
            var message = new StringBuilder();

            if (error != null)
            {
                message
                    .AppendLine(error)
                    .AppendLine();
            }

            message
                .AppendLine($"*🌐 Main blockchains:* {string.Join(" / ", GetFormattedBlockchains())}");

            if (TestBlockchains != null)
            {
                message
                    .AppendLine()
                    .AppendLine($"*🧪 Test blockchains:* {string.Join(" / ", GetFormattedBlockchains(true))}");
            }

            return message.ToString();
        }
    }
}
