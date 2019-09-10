using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Model.Types;

// TODO separate in namespaces? is there a convention for extensions?
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

        public static void CallVirtual(this InstructionEncoder encoder, EntityHandle methodReference)
        {
            encoder.OpCode(ILOpCode.Callvirt);
            encoder.Token(methodReference);
        }
    }
}
