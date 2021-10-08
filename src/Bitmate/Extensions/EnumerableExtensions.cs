using System;
using System.Collections.Generic;

namespace Bitmate.Extensions
{
    public static class EnumerableExtensions
    {
        public static void ForEach<TSource>(this IEnumerable<TSource> source, Action<TSource> action)
        {
            foreach (TSource item in source)
            {
                action(item);
            }
        }
    }
}
