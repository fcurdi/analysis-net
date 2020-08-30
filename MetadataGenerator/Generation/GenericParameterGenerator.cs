using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Model;
using Model.Types;
using static MetadataGenerator.AttributesProvider;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generation
{
    internal static class GenericParameterGenerator
    {
        // GenericParam Table (0x2A)
        public static void GenerateGenericParameters(MetadataContainer metadataContainer) =>
            metadataContainer
                .GenericParameterEntries
                .Select(entry =>
                {
                    var genericParameter = entry.GenericParameter;
                    var constraints = genericParameter.Constraints
                        .Select(type => metadataContainer.MetadataResolver.HandleOf(type))
                        .ToSet();
                    return new GenericParamRow(
                        entry.Parent,
                        AttributesFor(genericParameter),
                        metadataContainer.MetadataBuilder.GetOrAddString(genericParameter.Name),
                        genericParameter.Index,
                        constraints
                    );
                })
                .OrderBy(row => ECMA335.CodedIndex.TypeOrMethodDef(row.Parent))
                .ThenBy(row => row.Index)
                .ToList()
                .ForEach(row =>
                {
                    var genericParameterHandle = metadataContainer.MetadataBuilder.AddGenericParameter(
                        row.Parent,
                        row.Attributes,
                        row.Name,
                        row.Index
                    );

                    foreach (var constraint in row.Constraints)
                    {
                        metadataContainer.MetadataBuilder.AddGenericParameterConstraint(genericParameterHandle, constraint);
                    }
                });

        private class GenericParamRow
        {
            public readonly SRM.EntityHandle Parent;
            public readonly GenericParameterAttributes Attributes;
            public readonly SRM.StringHandle Name;
            public readonly ushort Index;
            public readonly ISet<SRM.EntityHandle> Constraints;

            public GenericParamRow(
                SRM.EntityHandle parent,
                GenericParameterAttributes attributes,
                SRM.StringHandle name,
                ushort index,
                ISet<SRM.EntityHandle> constraints)
            {
                Parent = parent;
                Attributes = attributes;
                Name = name;
                Index = index;
                Constraints = constraints;
            }
        }

        public class GenericParameterEntry
        {
            public readonly SRM.EntityHandle Parent;
            public readonly GenericParameter GenericParameter;

            public GenericParameterEntry(SRM.EntityHandle parent, GenericParameter genericParameter)
            {
                Parent = parent;
                GenericParameter = genericParameter;
            }
        }
    }
}