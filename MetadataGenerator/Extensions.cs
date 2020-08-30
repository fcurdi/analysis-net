using System.Collections.Generic;
using System.Linq;
using Model.Types;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator
{
    public static class Extensions
    {
        public static T FirstOr<T>(this IEnumerable<T> enumerable, T defaultValue)
        {
            var first = enumerable.FirstOrDefault();
            return first.Equals(default(T)) ? defaultValue : first;
        }

        // The next available slot in the corresponding table. If nothing is defined in the module then use row number 1 for the corresponding table
        public static int NextRowFor(this ECMA335.MetadataBuilder metadata, ECMA335.TableIndex tableIndex) => metadata.GetRowCount(tableIndex) + 1;

        public static bool IsGenericInstantiation(this IBasicType type) => type.GenericType != null;

        public static bool IsGenericInstantiation(this IMethodReference method) => method.GenericMethod != null;
    }
}