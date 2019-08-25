using System.Collections.Generic;
using System.Linq;

namespace MetadataGenerator
{
    public static class Extensions
    {
        public static T FirstOr<T>(this IEnumerable<T> enumerable, T defaultValue)
        {
            T first = enumerable.FirstOrDefault();
            return first.Equals(default(T)) ? defaultValue : first;
        }

    }
}
