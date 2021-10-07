using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BitconfirmBot.Models;

namespace BitconfirmBot.Services.Cache
{
    public class CacheService
    {
        private readonly string _cacheFilePath;
        private readonly JsonSerializerOptions _jsonSerializerOptions;
        private readonly object _locker = new();

        public CacheService(string cacheFilePath, JsonSerializerOptions jsonSerializerOptions = null)
        {
            _cacheFilePath = cacheFilePath;
            _jsonSerializerOptions = jsonSerializerOptions;
        }

        public List<CachedTransaction> Read()
        {
            lock (_locker)
            {
                string cacheJson = File.ReadAllText(_cacheFilePath);

                return JsonSerializer.Deserialize<List<CachedTransaction>>(cacheJson, _jsonSerializerOptions);
            }
        }

        public void Write(List<CachedTransaction> cache)
        {
            lock (_locker)
            {
                string cacheJson = JsonSerializer.Serialize(cache, _jsonSerializerOptions);

                File.WriteAllText(_cacheFilePath, cacheJson);
            }
        }

        public void Add(CachedTransaction transaction)
        {
            lock (_locker)
            {
                var cache = Read();

                cache.Add(transaction);

                Write(cache);
            }
        }

        public void Remove(CachedTransaction transaction)
        {
            lock (_locker)
            {
                var cache = Read();

                // Can't use cache.Remove(transaction) since ReadCache() creates a different object and reference isn't equal
                cache.RemoveAll(t => t.Message.MessageId == transaction.Message.MessageId);

                Write(cache);
            }
        }
    }
}
