namespace BitconfirmBot.Models
{
    public class Settings
    {
        public string Token { get; set; }

        public string Api { get; set; }

        public ProxiesSettings Proxies { get; set; }
    }

    public class ProxiesSettings
    {
        public bool Use { get; set; }

        public int TimeoutSeconds { get; set; }
    }
}
