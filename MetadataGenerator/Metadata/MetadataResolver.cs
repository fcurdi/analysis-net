using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using MetadataGenerator.Generators.Fields;
using MetadataGenerator.Generators.Methods;
using Model;
using Model.Types;
using static System.Linq.Enumerable;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Metadata
{
    internal class MetadataResolver
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

            foreach (var assemblyReference in assembly.References)
            {
                // FIXME parameters
                assemblyReferences.Add(assemblyReference.Name, metadataContainer.metadataBuilder.AddAssemblyReference(
                    name: metadataContainer.metadataBuilder.GetOrAddString(assemblyReference.Name),
                    version: new Version(4, 0, 0, 0),
                    culture: metadataContainer.metadataBuilder.GetOrAddString("neutral"),
                    publicKeyOrToken: metadataContainer.metadataBuilder.GetOrAddBlob(
                        ImmutableArray.Create<byte>(0xB7, 0x7A, 0x5C, 0x56, 0x19, 0x34, 0xE0, 0x89)),
                    flags: default,
                    hashValue: default)
                );
            }

            fieldSignatureGenerator = new FieldSignatureGenerator(metadataContainer);
            methodSignatureGenerator = new MethodSignatureGenerator(metadataContainer);
        }

        public SRM.EntityHandle ReferenceHandleOf(IMetadataReference metadataReference)
        {
            switch (metadataReference)
            {
                case IFieldReference field:
                {
                    var signature = fieldSignatureGenerator.GenerateSignatureOf(field);
                    return ReferenceHandleOf(field, signature);
                }
                case IMethodReference method:
                {
                    var signature = methodSignatureGenerator.GenerateSignatureOf(method);
                    return ReferenceHandleOf(method, signature);
                }
                case FunctionPointerType functionPointer:
                {
                    var signature = methodSignatureGenerator.GenerateSignatureOf(functionPointer);
                    return metadataContainer.metadataBuilder.AddStandaloneSignature(metadataContainer.metadataBuilder.GetOrAddBlob(signature));
                }
                case IType type:
                    return ReferenceHandleOf(type);
                default:
                    throw new Exception($"Metadata reference not supported");
            }
        }

        private SRM.EntityHandle ReferenceHandleOf(IType type)
        {
            switch (type)
            {
                case IBasicType basicType: return ReferenceHandleOf(basicType);
                case IType iType when iType is ArrayType || iType is PointerType || iType is IGenericParameterReference:
                {
                    var signature = new SRM.BlobBuilder();
                    var encoder = new ECMA335.BlobEncoder(signature).TypeSpecificationSignature();
                    Encode(iType, encoder);
                    // FIXME should be stored? or added every time?
                    return metadataContainer.metadataBuilder.AddTypeSpecification(metadataContainer.metadataBuilder.GetOrAddBlob(signature));
                }
                default:
                    throw new Exception($"type ${type} not yet supported");
            }
        }

        /*
         * Returns a TypeReference for type. It stores references because metadata does not have a getOrAddTypeReference.
         */
        private SRM.EntityHandle ReferenceHandleOf(IBasicType type)
        {
            // TODO rewrite this method better
            // FIXME should be stored? or added every time?
            if (type.IsGenericInstantiation())
            {
                var signature = new SRM.BlobBuilder();
                var encoder = new ECMA335.BlobEncoder(signature).TypeSpecificationSignature();
                Encode(type, encoder);
                return metadataContainer.metadataBuilder.AddTypeSpecification(metadataContainer.metadataBuilder.GetOrAddBlob(signature));
            }

            var typeName = type.Name;

            /**
             * CLS-compliant generic type names are encoded using the format “name[`arity]”, where […] indicates that the grave accent character “`” and
             * arity together are optional. The encoded name shall follow these rules:
             *     - name shall be an ID that does not contain the “`” character.
             *     - arity is specified as an unsigned decimal number without leading zeros or spaces.
             *     - For a normal generic type, arity is the number of type parameters declared on the type.
             *     - For a nested generic type, arity is the number of newly introduced type parameters.
             */

            // FIXME partial logic. See TypeGenerator.TypeNameOf. Needs to be unified with that
            if (type.IsGenericType())
            {
                typeName = $"{type.Name}`{type.GenericParameterCount}";
            }

            var key =
                $"{type.ContainingAssembly.Name}.{type.ContainingNamespace}.{(type.ContainingType != null ? (type.ContainingType.Name + ".") : "")}{typeName}";
            if (!typeReferences.TryGetValue(key, out var typeReference))
            {
                SRM.EntityHandle resolutionScope;
                if (type.ContainingType == null) // if defined in the namespace then search there
                {
                    resolutionScope = type.ContainingAssembly.Name.Equals(assembly.Name)
                        ? default
                        : assemblyReferences[type.ContainingAssembly.Name];
                }
                else
                {
                    // if not, recursively get a reference for the containing type and use that as the resolution scope
                    resolutionScope = ReferenceHandleOf(type.ContainingType);
                }

                typeReference = metadataContainer.metadataBuilder.AddTypeReference(
                    resolutionScope: resolutionScope,
                    @namespace: metadataContainer.metadataBuilder.GetOrAddString(type.ContainingNamespace),
                    name: metadataContainer.metadataBuilder.GetOrAddString(typeName));
                typeReferences.Add(key, typeReference);

                return typeReference;
            }

            // if not add the new type reference to metadata and store it
            return typeReference;
        }

        private SRM.EntityHandle ReferenceHandleOf(IMethodReference method, SRM.BlobBuilder signature)
        {
            if (method.IsGenericInstantiation())
            {
                // FIXME should be stored and not add a new one each time (like the else branch).
                // FIXME To do this, the key should have info related to the instantiation that unequivocally identifies that particular instantiation
                var methodSpecificationHandle = metadataContainer.metadataBuilder.AddMethodSpecification(
                    ReferenceHandleOf(method.GenericMethod, methodSignatureGenerator.GenerateSignatureOf(method.GenericMethod)),
                    metadataContainer.metadataBuilder.GetOrAddBlob(signature)
                );
                return methodSpecificationHandle;
            }
            else
            {
                var key =
                    $"{method.ContainingType.ContainingAssembly.Name}.{method.ContainingType.ContainingNamespace}.{method.ContainingType}.{method.Name}";
                if (!memberReferences.TryGetValue(key, out var methodReferenceHandle))
                {
                    methodReferenceHandle = metadataContainer.metadataBuilder.AddMemberReference(
                        parent: ReferenceHandleOf(method.ContainingType),
                        name: metadataContainer.metadataBuilder.GetOrAddString(method.Name),
                        signature: metadataContainer.metadataBuilder.GetOrAddBlob(signature));
                    memberReferences.Add(key, methodReferenceHandle);
                }

                return methodReferenceHandle;
            }
        }

        private SRM.MemberReferenceHandle ReferenceHandleOf(IFieldReference field, SRM.BlobBuilder signature)
        {
            var key =
                $"{field.ContainingType.ContainingAssembly.Name}.{field.ContainingType.ContainingNamespace}.{field.ContainingType.Name}.{field.Name}";
            if (!memberReferences.TryGetValue(key, out var memberReferenceHandle))
            {
                memberReferenceHandle = metadataContainer.metadataBuilder.AddMemberReference(
                    parent: ReferenceHandleOf(field.ContainingType),
                    name: metadataContainer.metadataBuilder.GetOrAddString(field.Name),
                    signature: metadataContainer.metadataBuilder.GetOrAddBlob(signature));
                memberReferences.Add(key, memberReferenceHandle);

                return memberReferenceHandle;
            }

            return memberReferenceHandle;
        }

        // SignatureTypeEncoder is a struct but it is not necessary to pass it by reference since 
        // it operates on its Builder (BlobBuilder) which is a class (tha means the builder reference is always the same)
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
                switch (type)
                {
                    case IBasicType basicType:
                    {
                        if (basicType.IsGenericInstantiation())
                        {
                            var genericInstantiation = encoder.GenericInstantiation(
                                ReferenceHandleOf(basicType.GenericType),
                                basicType.GenericParameterCount,
                                type.TypeKind == TypeKind.ValueType);
                            foreach (var genericArg in basicType.GenericArguments)
                            {
                                Encode(genericArg, genericInstantiation.AddArgument());
                            }
                        }
                        else
                        {
                            encoder.Type(ReferenceHandleOf(basicType), type.TypeKind == TypeKind.ValueType);
                        }

                        break;
                    }
                    case ArrayType arrayType:
                        encoder.Array(
                            elementTypeEncoder => Encode(arrayType.ElementsType, elementTypeEncoder),
                            arrayShapeEncoder =>
                            {
                                var lowerBounds = arrayType.Rank > 1
                                    ? Repeat(0, (int) arrayType.Rank).ToImmutableArray() // FIXME 0 because ArrayType does not know bounds
                                    : ImmutableArray<int>.Empty;
                                arrayShapeEncoder.Shape(
                                    rank: (int) arrayType.Rank,
                                    sizes: ImmutableArray<int>.Empty,
                                    lowerBounds: lowerBounds);
                            });
                        break;
                    case PointerType pointerType:
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

                        break;
                    }
                    case IGenericParameterReference genericParameter:
                        switch (genericParameter.Kind)
                        {
                            case GenericParameterKind.Type:
                                encoder.GenericTypeParameter(genericParameter.Index);
                                break;
                            case GenericParameterKind.Method:
                                encoder.GenericMethodTypeParameter(genericParameter.Index);
                                break;
                        }

                        break;
                    default:
                        throw new Exception($"Type {type} not supported");
                }
            }
        }
    }
}