using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bitmate.Models;
using Bitmate.Utilities.Json;

namespace Bitmate.Services.Cache
{
    public class CacheService
    {
        private readonly string _cacheFilePath;
        private readonly JsonSerializerOptions _jsonSerializerOptions;
        private readonly object _locker = new();

        public CacheService(string cacheFilePath, JsonSerializerOptions jsonSerializerOptions = null)
        {
            _cacheFilePath = cacheFilePath;

            _jsonSerializerOptions = jsonSerializerOptions ?? new();
            _jsonSerializerOptions?.Converters.Add(new InlineKeyboardMarkupJsonConverter());
        }

        private void Write(List<CachedTransaction> cache)
        {
            lock (_locker)
            {
                string cacheJson = JsonSerializer.Serialize(cache, _jsonSerializerOptions);

                File.WriteAllText(_cacheFilePath, cacheJson);
            }
        }

        public List<CachedTransaction> Read()
        {
            lock (_locker)
            {
                string cacheJson = File.ReadAllText(_cacheFilePath);

                return JsonSerializer.Deserialize<List<CachedTransaction>>(cacheJson, _jsonSerializerOptions);
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

                cache.Remove(transaction);

                Write(cache);
            }
        }
        public void Update(CachedTransaction transaction)
        {
            lock (_locker)
            {
                var cache = Read();

                int index = cache.IndexOf(transaction);
                cache[index] = transaction;

                Write(cache);
            }
        }

        public bool TryGet(CachedTransaction transaction, out CachedTransaction found)
        {
            lock (_locker)
            {
                var cache = Read();

                found = cache.SingleOrDefault(t => t.Equals(transaction));

                return found != null;
            }
        }

        public bool TryGetByHashCode(int hashCode, out CachedTransaction found)
        {
            lock (_locker)
            {
                var cache = Read();

                found = cache.SingleOrDefault(t => t.GetHashCode() == hashCode);

                return found != null;
            }
        }
    }
}
