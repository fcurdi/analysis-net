﻿using MetadataGenerator.Generation.CustomAttributes;
using MetadataGenerator.Generation.Types;
using Model;
using SR = System.Reflection;

namespace MetadataGenerator.Generation
{
    internal static class AssemblyGenerator
    {
        public static MetadataContainer Generate(Assembly assembly)
        {
            var metadataContainer = new MetadataContainer(assembly);
            var metadataBuilder = metadataContainer.MetadataBuilder;
            var customAttributeGenerator = new CustomAttributeGenerator(metadataContainer);

            // Assembly Table (0x20) 
            var assemblyDefinitionHandle = metadataBuilder.AddAssembly(
                name: metadataBuilder.GetOrAddString(assembly.Name),
                version: assembly.Version,
                culture: metadataBuilder.GetOrAddString(assembly.Culture),
                publicKey: metadataBuilder.GetOrAddBlob(assembly.PublicKey),
                flags: SR.AssemblyFlags.PublicKey,
                hashAlgorithm: SR.AssemblyHashAlgorithm.Sha1
            );

            foreach (var customAttribute in assembly.Attributes)
            {
                customAttributeGenerator.Generate(assemblyDefinitionHandle, customAttribute);
            }

            new ModuleGenerator(metadataContainer).Generate(assembly);
            new NamespaceGenerator(metadataContainer, customAttributeGenerator).Generate(assembly.RootNamespace);

            // Some tables must be sorted by one or more of their columns. Since the dll's methods and types don't follow a
            // particular order, the info needed to load this tables is stored during type/method generation but not added to the
            // MetadataBuilder until now where they can be previously sorted.
            new InterfaceImplementationGenerator(metadataContainer).Generate();
            new GenericParameterGenerator(metadataContainer).Generate();
            new NestedTypeGenerator(metadataContainer).Generate();

            return metadataContainer;
        }
    }
}