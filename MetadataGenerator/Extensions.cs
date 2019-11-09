using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Model.Types;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

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

        public static void CallVirtual(this ECMA335.InstructionEncoder encoder, SRM.EntityHandle methodReference)
        {
            encoder.OpCode(SRM.ILOpCode.Callvirt);
            encoder.Token(methodReference);
        }

        // The next available slot in the corresponding table. If nothing is defined in the module then use row number 1 for the corresponding table
        public static int NextRowFor(this ECMA335.MetadataBuilder metadata, ECMA335.TableIndex tableIndex) => metadata.GetRowCount(tableIndex) + 1;

        public static string CurrentLabelString(this ECMA335.InstructionEncoder instructionEncoder) => string.Format("L_{0:x4}", instructionEncoder.Offset).ToLower();

    }
}
