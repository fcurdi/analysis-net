using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace MetadataGenerator
{
    public class TypeReferences
    {
        private readonly Model.Assembly assembly;
        private readonly MetadataBuilder metadata;
        private readonly IDictionary<string, AssemblyReferenceHandle> assemblyReferences = new Dictionary<string, AssemblyReferenceHandle>();
        // FIXME better name. It's not all the type references but just the ones needed for Base type and interfaces
        private readonly IDictionary<string, TypeReferenceHandle> typeReferences = new Dictionary<string, TypeReferenceHandle>();

        public TypeReferences(MetadataBuilder metadata, Model.Assembly assembly)
        {
            this.metadata = metadata;
            this.assembly = assembly;

            //FIXME see references in IlSpy generated vs original
            //FIXME: assemblyName => assemblyRef could result in false positive?
            foreach (var assemblyReference in assembly.References)
            {
                // FIXME parameters depend of assembly info that is not in the model
                assemblyReferences.Add(assemblyReference.Name, metadata.AddAssemblyReference(
                        name: metadata.GetOrAddString(assemblyReference.Name),
                        version: new Version(4, 0, 0, 0),
                        culture: metadata.GetOrAddString("neutral"),
                        publicKeyOrToken: metadata.GetOrAddBlob(ImmutableArray.Create<byte>(0xB7, 0x7A, 0x5C, 0x56, 0x19, 0x34, 0xE0, 0x89)),
                        flags: default(AssemblyFlags),
                        hashValue: default(BlobHandle))
                );
            }
        }

        public EntityHandle TypeReferenceOf(Model.Types.IBasicType type)
        {
            TypeReferenceHandle typeReference;
            var typeReferenceKey = $"{type.ContainingAssembly.Name}.{type.ContainingNamespace}.{type.Name}";
            if (typeReferences.TryGetValue(typeReferenceKey, out var value))
            {
                typeReference = value;
            }
            else
            {
                var resolutionScope = type.ContainingAssembly.Name.Equals(assembly.Name)
                    ? default(AssemblyReferenceHandle)
                    : assemblyReferences[type.ContainingAssembly.Name];

                // FIXME: comparing to the name of the current assembly could result in a false positive?
                typeReference = metadata.AddTypeReference(
                    resolutionScope: resolutionScope,
                    @namespace: metadata.GetOrAddString(type.ContainingNamespace),
                    name: metadata.GetOrAddString(type.Name));
                typeReferences.Add(typeReferenceKey, typeReference);
            }
            return typeReference;
        }
    }
}
