using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using MetadataGenerator.Generators.Fields;
using MetadataGenerator.Generators.Methods;
using Model;
using Model.Types;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator
{
    class MetadataResolver
    {
        private readonly Assembly assembly;
        private readonly MetadataContainer metadataContainer;
        private readonly IDictionary<string, SRM.AssemblyReferenceHandle> assemblyReferences = new Dictionary<string, SRM.AssemblyReferenceHandle>();
        private readonly IDictionary<string, SRM.TypeReferenceHandle> typeReferences = new Dictionary<string, SRM.TypeReferenceHandle>();
        private readonly IDictionary<string, SRM.MemberReferenceHandle> memberReferences = new Dictionary<string, SRM.MemberReferenceHandle>();
        private readonly FieldSignatureGenerator fieldSignatureGenerator;
        private readonly MethodSignatureGenerator methodSignatureGenerator;

        public MetadataResolver(MetadataContainer metadataContainer, Assembly assembly)
        {
            this.metadataContainer = metadataContainer;
            this.assembly = assembly;

            // FIXME: assemblyName => assemblyRef could result in false positive?
            foreach (var assemblyReference in assembly.References)
            {
                // FIXME parameters
                assemblyReferences.Add(assemblyReference.Name, metadataContainer.metadataBuilder.AddAssemblyReference(
                        name: metadataContainer.metadataBuilder.GetOrAddString(assemblyReference.Name),
                        version: new Version(4, 0, 0, 0),
                        culture: metadataContainer.metadataBuilder.GetOrAddString("neutral"),
                        publicKeyOrToken: metadataContainer.metadataBuilder.GetOrAddBlob(ImmutableArray.Create<byte>(0xB7, 0x7A, 0x5C, 0x56, 0x19, 0x34, 0xE0, 0x89)),
                        flags: default,
                        hashValue: default)
                );
            }
            fieldSignatureGenerator = new FieldSignatureGenerator(metadataContainer);
            methodSignatureGenerator = new MethodSignatureGenerator(metadataContainer);
        }

        //FIXME name? more generic? only needed for method?
        public SRM.StandaloneSignatureHandle ResolveStandaloneSignatureFor(FunctionPointerType method)
        {
            var signature = methodSignatureGenerator.GenerateSignatureOf(method);
            return metadataContainer.metadataBuilder.AddStandaloneSignature(metadataContainer.metadataBuilder.GetOrAddBlob(signature));
        }

        public SRM.EntityHandle ReferenceHandleOf(IMetadataReference metadataReference)
        {
            if (metadataReference is IFieldReference field)
            {
                var signature = fieldSignatureGenerator.GenerateSignatureOf(field);
                return ReferenceHandleOf(field, signature);
            }
            else if (metadataReference is IMethodReference method)
            {
                var signature = methodSignatureGenerator.GenerateSignatureOf(method);
                return ReferenceHandleOf(method, signature);
            }
            else if (metadataReference is IType type)
            {
                return ReferenceHandleOf(type);
            }
            else
            {
                throw new Exception(); // FIXME
            }
        }

        private SRM.EntityHandle ReferenceHandleOf(IType type)
        {
            if (type is IBasicType basicType)
            {
                return ReferenceHandleOf(basicType, basicType.Name);
            }
            // FIXME rompe porque queda desordenada la tabla de genericParam. 
            else if (type is IGenericParameterReference genericParameterReference)
            {
                return ReferenceHandleOf(genericParameterReference.GenericContainer.ContainingType, genericParameterReference.Name);
            }
            else if (type is ArrayType arrayType)
            {
                var signature = new SRM.BlobBuilder();
                var encoder = new ECMA335.BlobEncoder(signature).TypeSpecificationSignature();
                Encode(arrayType, encoder);
                return metadataContainer.metadataBuilder.AddTypeSpecification(metadataContainer.metadataBuilder.GetOrAddBlob(signature));
            }
            else if (type is PointerType pointerType)
            {
                var signature = new SRM.BlobBuilder();
                var encoder = new ECMA335.BlobEncoder(signature).TypeSpecificationSignature();
                Encode(pointerType, encoder);
                return metadataContainer.metadataBuilder.AddTypeSpecification(metadataContainer.metadataBuilder.GetOrAddBlob(signature));
            }
            else
            {
                throw new Exception("not yet supported");
            }
        }

        /*
         * Returns a TypeReference for type. It stores references because metadata does not have a getOrAddTypeReference.
         */
        private SRM.TypeReferenceHandle ReferenceHandleOf(IBasicType type, string typeName)
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
                typeReference = metadataContainer.metadataBuilder.AddTypeReference(
                    resolutionScope: resolutionScope,
                    @namespace: metadataContainer.metadataBuilder.GetOrAddString(type.ContainingNamespace),
                    name: metadataContainer.metadataBuilder.GetOrAddString(typeName));
                typeReferences.Add(typeReferenceKey, typeReference);
            }
            return typeReference;
        }

        private SRM.MemberReferenceHandle ReferenceHandleOf(IMethodReference method, SRM.BlobBuilder signature)
        {
            var key = $"{method.ContainingType.ContainingAssembly.Name}.{method.ContainingType.ContainingNamespace}.{method.ContainingType.Name}.{method.Name}";
            if (!memberReferences.TryGetValue(key, out SRM.MemberReferenceHandle memberReferenceHandle))
            {
                memberReferenceHandle = metadataContainer.metadataBuilder.AddMemberReference(
                        parent: ReferenceHandleOf(method.ContainingType),
                        name: metadataContainer.metadataBuilder.GetOrAddString(method.Name),
                        signature: metadataContainer.metadataBuilder.GetOrAddBlob(signature));
                memberReferences.Add(key, memberReferenceHandle);
            }
            return memberReferenceHandle;
        }

        // FIXME extract method for both method and field? identical
        private SRM.MemberReferenceHandle ReferenceHandleOf(IFieldReference field, SRM.BlobBuilder signature)
        {
            var key = $"{field.ContainingType.ContainingAssembly.Name}.{field.ContainingType.ContainingNamespace}.{field.ContainingType.Name}.{field.Name}";
            if (!memberReferences.TryGetValue(key, out SRM.MemberReferenceHandle memberReferenceHandle))
            {
                memberReferenceHandle = metadataContainer.metadataBuilder.AddMemberReference(
                        parent: ReferenceHandleOf(field.ContainingType),
                        name: metadataContainer.metadataBuilder.GetOrAddString(field.Name),
                        signature: metadataContainer.metadataBuilder.GetOrAddBlob(signature));
                memberReferences.Add(key, memberReferenceHandle);
            }
            return memberReferenceHandle;
        }

        // SignatureTypeEncoder is a struct but it is not necessary to pass it by reference since 
        // it operates on its Builder (BlobBuilber) which is a class (tha means the builder refernece is always the same)
        public void Encode(IType type, ECMA335.SignatureTypeEncoder encoder)
        {
            if (type.Equals(PlatformTypes.Boolean)) encoder.Boolean();
            else if (type.Equals(PlatformTypes.Byte)) encoder.Byte();
            else if (type.Equals(PlatformTypes.SByte)) encoder.SByte();
            else if (type.Equals(PlatformTypes.Char)) encoder.Char();
            else if (type.Equals(PlatformTypes.Double)) encoder.Double();
            else if (type.Equals(PlatformTypes.Int16)) encoder.Int16();
            else if (type.Equals(PlatformTypes.UInt16)) encoder.UInt16();
            else if (type.Equals(PlatformTypes.Int32)) encoder.Int32();
            else if (type.Equals(PlatformTypes.UInt32)) encoder.UInt32();
            else if (type.Equals(PlatformTypes.Int64)) encoder.Int64();
            else if (type.Equals(PlatformTypes.UInt64)) encoder.UInt64();
            else if (type.Equals(PlatformTypes.String)) encoder.String();
            else if (type.Equals(PlatformTypes.Single)) encoder.Single();
            else if (type.Equals(PlatformTypes.Object)) encoder.Object();
            else
            {
                if (type is IBasicType basicType)
                {
                    if (basicType.GenericType != null)
                    {
                        var genericInstantiation = encoder.GenericInstantiation(
                             ReferenceHandleOf(basicType),
                             basicType.GenericParameterCount,
                             type.TypeKind == TypeKind.ValueType
                         );
                        foreach (var genericArg in basicType.GenericArguments)
                        {
                            Encode(genericArg, genericInstantiation.AddArgument());
                        }
                    }
                    else
                    {
                        encoder.Type(ReferenceHandleOf(basicType), type.TypeKind == TypeKind.ValueType);
                    }
                }
                else if (type is ArrayType arrayType)
                {
                    encoder.Array(
                        elementTypeEncoder => Encode(arrayType.ElementsType, elementTypeEncoder),
                        arrayShapeEncoder =>
                        {
                            // FIXME real values for sizes and lowerBounds
                            // size cannot be known (example: int[])
                            arrayShapeEncoder.Shape(
                                rank: (int)arrayType.Rank,
                                sizes: ImmutableArray<int>.Empty,
                                lowerBounds: ImmutableArray<int>.Empty);
                        });

                }
                else if (type is PointerType pointerType)
                {
                    // TODO there's also signatureTypeEncode.FunctionPointer()/IntPtr()/UIntPtr
                    var targetType = pointerType.TargetType;
                    if (targetType.Equals(PlatformTypes.Void))
                    {
                        encoder.VoidPointer();
                    }
                    else
                    {
                        Encode(targetType, encoder.Pointer());
                    }
                }
                else if (type is GenericParameter genericParameter)
                {
                    switch (genericParameter.Kind)
                    {
                        case GenericParameterKind.Type:
                            encoder.GenericTypeParameter(genericParameter.Index);
                            break;
                        case GenericParameterKind.Method:
                            encoder.GenericMethodTypeParameter(genericParameter.Index);
                            break;
                    }
                }
                else throw new Exception($"Type {type} not supported");
            }
        }
    }
}