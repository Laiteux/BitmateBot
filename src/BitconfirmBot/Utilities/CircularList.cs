using System.Collections.Generic;

namespace BitconfirmBot.Utilities
{
    public class CircularList<T> : List<T>
    {
        private int _index;
        private readonly object _lock = new();

        public CircularList()
        {
        }

        public CircularList(IEnumerable<T> collection) : base(collection)
        {
        }

        public T Next()
        {
            lock (_lock)
            {
                if (_index >= Count)
                {
                    _index = 0;
                }

                return this[_index++];
            }
        }
    }
}
