using System;
using System.Collections.Generic;
using System.Linq;

namespace BitconfirmBot.Helpers
{
    public static class TypeHelper
    {
        public static IEnumerable<Type> GetSubclasses<T>()
        {
            return typeof(T).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(T)) && t.IsClass && !t.IsAbstract);
        }
    }
}
