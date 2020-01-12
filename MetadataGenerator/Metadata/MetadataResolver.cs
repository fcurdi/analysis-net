using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using MetadataGenerator.Generators.Fields;
using MetadataGenerator.Generators.Methods;
using Model.Types;
using static System.Linq.Enumerable;
using Assembly = Model.Assembly;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Metadata
{
    // FIXME is there a way to unify all this dictionaries and logic of GetOrAdd?
    internal class MetadataResolver
    {
        private readonly Assembly assembly;
        private readonly MetadataContainer metadataContainer;
        private readonly IDictionary<string, SRM.AssemblyReferenceHandle> assemblyReferences = new Dictionary<string, SRM.AssemblyReferenceHandle>();
        private readonly IDictionary<string, SRM.TypeReferenceHandle> typeReferences = new Dictionary<string, SRM.TypeReferenceHandle>();

        private readonly IDictionary<KeyValuePair<string, SRM.BlobHandle>, SRM.MemberReferenceHandle> memberReferences =
            new Dictionary<KeyValuePair<string, SRM.BlobHandle>, SRM.MemberReferenceHandle>();

        private readonly IDictionary<SRM.BlobHandle, SRM.TypeSpecificationHandle> typeSpecificationReferences =
            new Dictionary<SRM.BlobHandle, SRM.TypeSpecificationHandle>();

        private readonly IDictionary<SRM.BlobHandle, SRM.MethodSpecificationHandle> methodSpecificationReferences =
            new Dictionary<SRM.BlobHandle, SRM.MethodSpecificationHandle>();

        private readonly IDictionary<SRM.BlobHandle, SRM.StandaloneSignatureHandle> standaloneSignatureReferences =
            new Dictionary<SRM.BlobHandle, SRM.StandaloneSignatureHandle>();

        private readonly FieldSignatureGenerator fieldSignatureGenerator;
        private readonly MethodSignatureGenerator methodSignatureGenerator;

        public MetadataResolver(MetadataContainer metadataContainer, Assembly assembly)
        {
            this.metadataContainer = metadataContainer;
            this.assembly = assembly;

            foreach (var assemblyReference in assembly.References)
            {
                // TODO version,culture and others should be in the assemblyReference. Submit PR with this
                assemblyReferences.Add(assemblyReference.Name, metadataContainer.metadataBuilder.AddAssemblyReference(
                    name: metadataContainer.metadataBuilder.GetOrAddString(assemblyReference.Name),
                    version: new Version(4, 0, 0, 0),
                    culture: default,
                    publicKeyOrToken: default,
                    flags: AssemblyFlags.PublicKey,
                    hashValue: default)
                );
            }

            fieldSignatureGenerator = new FieldSignatureGenerator(metadataContainer);
            methodSignatureGenerator = new MethodSignatureGenerator(metadataContainer);
        }

        public SRM.EntityHandle HandleOf(IMetadataReference metadataReference)
        {
            switch (metadataReference)
            {
                case IFieldReference field:
                {
                    var signature = fieldSignatureGenerator.GenerateSignatureOf(field);
                    return GetOrAddFieldReference(field, signature);
                }
                case IMethodReference method:
                {
                    var signature = methodSignatureGenerator.GenerateSignatureOf(method);
                    return GetOrAddMethodReference(method, signature);
                }
                case FunctionPointerType functionPointer:
                {
                    var signature = methodSignatureGenerator.GenerateSignatureOf(functionPointer);
                    return GetOrAddStandaloneSignature(signature);
                }
                case IType type:
                    switch (type)
                    {
                        case IBasicType basicType:
                            return GetOrAddTypeReference(basicType);
                        case IType iType when iType is ArrayType || iType is PointerType || iType is IGenericParameterReference:
                            return GetOrAddTypeSpecificationFor(iType);
                        default:
                            throw new Exception($"type {type} not yet supported");
                    }
                default:
                    throw new Exception($"Metadata {metadataReference} reference not supported");
            }
        }

        private SRM.EntityHandle GetOrAddTypeReference(IBasicType type)
        {
            if (type.IsGenericInstantiation()) return GetOrAddTypeSpecificationFor(type);

            var typeName = type.Name;

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
                    resolutionScope = GetOrAddTypeReference(type.ContainingType);
                }

                typeReference = metadataContainer.metadataBuilder.AddTypeReference(
                    resolutionScope: resolutionScope,
                    @namespace: metadataContainer.metadataBuilder.GetOrAddString(type.ContainingNamespace),
                    name: metadataContainer.metadataBuilder.GetOrAddString(typeName));
                typeReferences.Add(key, typeReference);
            }

            return typeReference;
        }

        private SRM.TypeSpecificationHandle GetOrAddTypeSpecificationFor(IType type)
        {
            var signature = new SRM.BlobBuilder();
            var encoder = new ECMA335.BlobEncoder(signature).TypeSpecificationSignature();
            Encode(type, encoder);
            var blobHandle = metadataContainer.metadataBuilder.GetOrAddBlob(signature);
            if (!typeSpecificationReferences.TryGetValue(blobHandle, out var typeSpecification))
            {
                typeSpecification = metadataContainer.metadataBuilder.AddTypeSpecification(blobHandle);
                typeSpecificationReferences.Add(blobHandle, typeSpecification);
            }

            return typeSpecification;
        }

        private SRM.MethodSpecificationHandle GetOrAddMethodSpecificationFor(IMethodReference method, SRM.BlobBuilder signature)
        {
            var blobHandle = metadataContainer.metadataBuilder.GetOrAddBlob(signature);
            if (!methodSpecificationReferences.TryGetValue(blobHandle, out var methodSpecification))
            {
                methodSpecification = metadataContainer.metadataBuilder.AddMethodSpecification(
                    GetOrAddMethodReference(method.GenericMethod, methodSignatureGenerator.GenerateSignatureOf(method.GenericMethod)),
                    blobHandle
                );
                methodSpecificationReferences.Add(blobHandle, methodSpecification);
            }

            return methodSpecification;
        }

        public SRM.StandaloneSignatureHandle GetOrAddStandaloneSignature(SRM.BlobBuilder signature)
        {
            var blobHandle = metadataContainer.metadataBuilder.GetOrAddBlob(signature);
            if (!standaloneSignatureReferences.TryGetValue(blobHandle, out var standaloneSignature))
            {
                standaloneSignature = metadataContainer.metadataBuilder.AddStandaloneSignature(blobHandle);
                standaloneSignatureReferences.Add(blobHandle, standaloneSignature);
            }

            return standaloneSignature;
        }

        private SRM.EntityHandle GetOrAddMethodReference(IMethodReference method, SRM.BlobBuilder signature)
        {
            if (method.IsGenericInstantiation())
            {
                return GetOrAddMethodSpecificationFor(method, signature);
            }
            else
            {
                var blobHandle = metadataContainer.metadataBuilder.GetOrAddBlob(signature);
                var key = new KeyValuePair<string, SRM.BlobHandle>(
                    $"{method.ContainingType.ContainingAssembly.Name}.{method.ContainingType.ContainingNamespace}.{method.ContainingType}.{method.Name}",
                    blobHandle
                );
                if (!memberReferences.TryGetValue(key, out var methodReferenceHandle))
                {
                    methodReferenceHandle = metadataContainer.metadataBuilder.AddMemberReference(
                        parent: GetOrAddTypeReference(method.ContainingType),
                        name: metadataContainer.metadataBuilder.GetOrAddString(method.Name),
                        signature: blobHandle);
                    memberReferences.Add(key, methodReferenceHandle);
                }

                return methodReferenceHandle;
            }
        }

        private SRM.MemberReferenceHandle GetOrAddFieldReference(IFieldReference field, SRM.BlobBuilder signature)
        {
            var blobHandle = metadataContainer.metadataBuilder.GetOrAddBlob(signature);
            var key = new KeyValuePair<string, SRM.BlobHandle>(
                $"{field.ContainingType.ContainingAssembly.Name}.{field.ContainingType.ContainingNamespace}.{field.ContainingType.Name}.{field.Name}",
                blobHandle
            );
            if (!memberReferences.TryGetValue(key, out var memberReferenceHandle))
            {
                memberReferenceHandle = metadataContainer.metadataBuilder.AddMemberReference(
                    parent: GetOrAddTypeReference(field.ContainingType),
                    name: metadataContainer.metadataBuilder.GetOrAddString(field.Name),
                    signature: blobHandle);
                memberReferences.Add(key, memberReferenceHandle);

                return memberReferenceHandle;
            }

            return memberReferenceHandle;
        }

        // SignatureTypeEncoder is a struct but it is not necessary to pass it by reference since 
        // it operates on its Builder (BlobBuilder) which is a class (that means the builder reference is always the same)
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
                        var isValueType = type.TypeKind == TypeKind.ValueType;
                        if (basicType.IsGenericInstantiation())
                        {
                            var genericInstantiation = encoder.GenericInstantiation(
                                GetOrAddTypeReference(basicType.GenericType),
                                basicType.GenericParameterCount,
                                isValueType);
                            foreach (var genericArg in basicType.GenericArguments)
                            {
                                Encode(genericArg, genericInstantiation.AddArgument());
                            }
                        }
                        else
                        {
                            encoder.Type(GetOrAddTypeReference(basicType), isValueType);
                        }

                        break;
                    }
                    case ArrayType arrayType:
                        encoder.Array(
                            elementTypeEncoder => Encode(arrayType.ElementsType, elementTypeEncoder),
                            arrayShapeEncoder =>
                            {
                                var lowerBounds = arrayType.Rank > 1
                                    ? Repeat(0, (int) arrayType.Rank).ToImmutableArray() // 0 because ArrayType does not know bounds
                                    : ImmutableArray<int>.Empty;
                                arrayShapeEncoder.Shape(
                                    rank: (int) arrayType.Rank,
                                    sizes: ImmutableArray<int>.Empty,
                                    lowerBounds: lowerBounds);
                            });
                        break;
                    case PointerType pointerType:
                    {
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