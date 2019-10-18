using System;
using MetadataGenerator.Generators;
using Model;
using SR = System.Reflection;

namespace MetadataGenerator
{
    static class AssemblyGenerator
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

            return metadataContainer;
        }
    }
}


