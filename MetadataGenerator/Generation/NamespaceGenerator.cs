﻿using System.Linq;
using MetadataGenerator.Generation.CustomAttributes;
using MetadataGenerator.Generation.Types;
using Model;
using Model.Types;
using static MetadataGenerator.Generation.Types.NestedTypeGenerator;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generation
{
    internal class NamespaceGenerator
    {
        private readonly MetadataContainer metadataContainer;
        private readonly TypeGenerator typeGenerator;

        public NamespaceGenerator(MetadataContainer metadataContainer, CustomAttributeGenerator customAttributeGenerator)
        {
            this.metadataContainer = metadataContainer;
            typeGenerator = new TypeGenerator(metadataContainer, customAttributeGenerator);
        }

        public void Generate(Namespace namezpace)
        {
            foreach (var type in namezpace.Types)
            {
                GenerateTypes(type);
            }

            foreach (var nestedNamespace in namezpace.Namespaces)
            {
                Generate(nestedNamespace);
            }
        }

        private SRM.TypeDefinitionHandle GenerateTypes(TypeDefinition type)
        {
            var nestedTypes = type.Types.Select(GenerateTypes).ToList();
            var typeDefinitionHandle = typeGenerator.Generate(type);
            foreach (var nestedType in nestedTypes)
            {
                metadataContainer
                    .DelayedEntries
                    .NestedTypeEntries
                    .Add(new NestedTypeEntry(nestedType, typeDefinitionHandle));
            }

            return typeDefinitionHandle;
        }
    }
}