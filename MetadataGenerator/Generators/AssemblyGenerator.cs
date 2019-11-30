using System;
using MetadataGenerator.Metadata;
using Model;
using SR = System.Reflection;

namespace MetadataGenerator.Generators
{
    internal static class AssemblyGenerator
    {
        public static MetadataContainer Generate(Assembly assembly)
        {
            var metadataContainer = new MetadataContainer(assembly);
            var namespaceGenerator = new NamespaceGenerator(metadataContainer);
            var metadataBuilder = metadataContainer.metadataBuilder;

            foreach (var namezpace in assembly.RootNamespace.Namespaces)
            {
                namespaceGenerator.Generate(namezpace);
            }

            // FIXME args
            metadataBuilder.AddAssembly(
                name: metadataBuilder.GetOrAddString(assembly.Name),
                version: new Version(1, 0, 0, 0),
                culture: default,
                publicKey: default,
                flags: SR.AssemblyFlags.PublicKey,
                hashAlgorithm: SR.AssemblyHashAlgorithm.Sha1);

            metadataBuilder.AddModule(
                generation: 0,
                moduleName: metadataBuilder.GetOrAddString($"{assembly.Name}.{(metadataContainer.Executable ? "exe" : "dll")}"),
                mvid: metadataBuilder.GetOrAddGuid(Guid.NewGuid()),
                encId: metadataBuilder.GetOrAddGuid(Guid.Empty),
                encBaseId: metadataBuilder.GetOrAddGuid(Guid.Empty));
            /*
             * Generic parameters table must be sorted by owner (TypeOrMethodDef that owns the generic parameter). Since the dll's methods and types don't follow a
             * particular order, the info needed to generate this parameters is stored during type/method generation but not added to the MetadataBuilder until now
            */
            metadataContainer.GenerateGenericParameters();

            // nested types table also needs to be sorted
            metadataContainer.GenerateNestedTypes();

            return metadataContainer;
        }
    }
}