using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Model;
using Model.Types;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator
{
    class ReferenceHandleResolver
    {
        private readonly Assembly assembly;
        private readonly ECMA335.MetadataBuilder metadata;
        private readonly IDictionary<string, SRM.AssemblyReferenceHandle> assemblyReferences = new Dictionary<string, SRM.AssemblyReferenceHandle>();
        private readonly IDictionary<string, SRM.TypeReferenceHandle> typeReferences = new Dictionary<string, SRM.TypeReferenceHandle>();
        private readonly IDictionary<string, SRM.MemberReferenceHandle> methodReferences = new Dictionary<string, SRM.MemberReferenceHandle>();

        public ReferenceHandleResolver(ECMA335.MetadataBuilder metadata, Assembly assembly)
        {
            this.metadata = metadata;
            this.assembly = assembly;

            // FIXME: assemblyName => assemblyRef could result in false positive?
            foreach (var assemblyReference in assembly.References)
            {
                // FIXME parameters
                assemblyReferences.Add(assemblyReference.Name, metadata.AddAssemblyReference(
                        name: metadata.GetOrAddString(assemblyReference.Name),
                        version: new Version(4, 0, 0, 0),
                        culture: metadata.GetOrAddString("neutral"),
                        publicKeyOrToken: metadata.GetOrAddBlob(ImmutableArray.Create<byte>(0xB7, 0x7A, 0x5C, 0x56, 0x19, 0x34, 0xE0, 0x89)),
                        flags: default,
                        hashValue: default)
                );
            }
        }

        /*
         * Returns a TypeReference for type. It stores references because metadata does not have a getOrAddTypeReference.
         */
        public SRM.TypeReferenceHandle ReferenceHandleOf(IBasicType type)
        {
            var typeReferenceKey = $"{type.ContainingAssembly.Name}.{type.ContainingNamespace}.{(type.ContainingType != null ? (type.ContainingType.Name + ".") : "")}{type.Name}";
            if (!typeReferences.TryGetValue(typeReferenceKey, out SRM.TypeReferenceHandle typeReference)) // If stored then return that
            { // if not add the new type reference to metadata and store it
                SRM.EntityHandle resolutionScope;
                if (type.ContainingType == null) // if defined in the namespace then search there
                {
                    resolutionScope = type.ContainingAssembly.Name.Equals(assembly.Name)
                        ? default
                        : assemblyReferences[type.ContainingAssembly.Name];
                }
                else
                { // if not, recursively get a reference for the containing type and use that as the resolution scopeø
                    resolutionScope = ReferenceHandleOf(type.ContainingType);
                }
                typeReference = metadata.AddTypeReference(
                    resolutionScope: resolutionScope,
                    @namespace: metadata.GetOrAddString(type.ContainingNamespace),
                    name: metadata.GetOrAddString(type.Name));
                typeReferences.Add(typeReferenceKey, typeReference);
            }
            return typeReference;
        }

        public SRM.MemberReferenceHandle ReferenceHandleOf(IMethodReference method, SRM.BlobBuilder signature)
        {
            var key = $"{method.ContainingType.ContainingAssembly.Name}.{method.ContainingType.ContainingNamespace}.{method.ContainingType.Name}.{method.Name}";
            if (!methodReferences.TryGetValue(key, out SRM.MemberReferenceHandle memberReferenceHandle))
            {
                memberReferenceHandle = metadata.AddMemberReference(
                        parent: ReferenceHandleOf(method.ContainingType),
                        name: metadata.GetOrAddString(method.Name),
                        signature: metadata.GetOrAddBlob(signature));
                methodReferences.Add(key, memberReferenceHandle);
            }
            return memberReferenceHandle;
        }

        // FIXME extract method for both method and field? identical
        public SRM.MemberReferenceHandle ReferenceHandleOf(IFieldReference field, SRM.BlobBuilder signature)
        {
            var key = $"{field.ContainingType.ContainingAssembly.Name}.{field.ContainingType.ContainingNamespace}.{field.ContainingType.Name}.{field.Name}";
            if (!methodReferences.TryGetValue(key, out SRM.MemberReferenceHandle memberReferenceHandle))
            {
                memberReferenceHandle = metadata.AddMemberReference(
                        parent: ReferenceHandleOf(field.ContainingType),
                        name: metadata.GetOrAddString(field.Name),
                        signature: metadata.GetOrAddBlob(signature));
                methodReferences.Add(key, memberReferenceHandle);
            }
            return memberReferenceHandle;
        }
    }
}