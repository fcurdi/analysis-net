using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Model.Types;

namespace MetadataGenerator
{
    public static class Extensions
    {
        public static T FirstOr<T>(this IEnumerable<T> enumerable, T defaultValue)
        {
            T first = enumerable.FirstOrDefault();
            return first.Equals(default(T)) ? defaultValue : first;
        }

        public static bool IsOneOf(this MethodParameterKind kind, params MethodParameterKind[] kinds) => ImmutableList.Create(kinds).Contains(kind);
    }
}
