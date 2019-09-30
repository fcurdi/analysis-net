using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Model.Types;

// TODO separate in namespaces (Collection, Metadata, etc)? is there a convention for extensions?
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

        // The next available slot in the corresponding table. If nothing is defined in the module then use row number 1 for the corresponding table
        public static int NextRowFor(this MetadataBuilder metadata, TableIndex tableIndex) => metadata.GetRowCount(tableIndex) + 1;

        public static string CurrentLabelString(this InstructionEncoder instructionEncoder) => string.Format("L_{0:x4}", instructionEncoder.Offset);
    }
}
