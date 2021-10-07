using System;
using System.Net;
using System.Net.Http;

namespace BitconfirmBot.Models
{
    public class Proxy
    {
        public Proxy(string proxy, ProxiesSettings settings)
        {
            string[] split = proxy.Split(':');

            Host = split[0];
            Port = int.Parse(split[1]);

            if (split.Length == 4)
            {
                Credentials = new NetworkCredential(split[2], split[3]);
            }

            Settings = settings;
        }

        public ProxiesSettings Settings { get; set; }

        public string Host { get; }

        public int Port { get; }

        public NetworkCredential Credentials { get; }

        public HttpClient GetHttpClient() => new(new HttpClientHandler()
        {
            Proxy = new WebProxy(Host, Port)
            {
                Credentials = Credentials
            }
        })
        {
            Timeout = TimeSpan.FromSeconds(Settings.TimeoutSeconds)
        };
    }
}
