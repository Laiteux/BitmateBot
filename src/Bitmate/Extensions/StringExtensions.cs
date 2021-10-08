using System;

namespace Bitmate.Extensions
{
    public static class StringExtensions
    {
        public static string Pluralize(this string str, long count, string append = "s")
        {
            return count > 1 ? str + append : str;
        }

        public static string TrimEnd(this string source, string suffixToRemove, StringComparison comparison = StringComparison.Ordinal)
        {
            return source.EndsWith(suffixToRemove) ? source.Remove(source.LastIndexOf(suffixToRemove, comparison)) : source;
        }
    }
}
