using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using MetadataGenerator.Generators.Fields;
using MetadataGenerator.Generators.Methods;
using MetadataGenerator.Generators.Methods.Body;
using Model;
using Model.Types;
using static System.Linq.Enumerable;
using static MetadataGenerator.Generators.TypeGenerator;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

// TODO revisar si esas keys que pongo en los getOrAdd (al ser mas precisas) estan funcionando o hacen que siempre se agregue uno nuevo
// TODO para esto comparar sin guardar ninguna cuanto da el conteo de las tablas y guardando despues.
// TODO ver si conviene usar blobhandle, byteArray de la signature  u otro. Evaluarlo tabla por tabla, quiza algunas andan bien y otras no
// TODO mas alla de la eficiencia, algunas tablas no admiten duplicados por lo que si no rompe por eso es que algunas esta usando.
namespace MetadataGenerator.Metadata
{
    internal class MetadataResolver
    {
        private readonly Assembly assembly;
        private readonly MetadataContainer metadataContainer;
        private readonly IDictionary<string, SRM.AssemblyReferenceHandle> assemblyReferences = new Dictionary<string, SRM.AssemblyReferenceHandle>();
        private readonly IDictionary<string, SRM.TypeReferenceHandle> typeReferences = new Dictionary<string, SRM.TypeReferenceHandle>();

        private readonly IDictionary<Tuple<object, byte[]>, SRM.MemberReferenceHandle> memberReferences =
            new Dictionary<Tuple<object, byte[]>, SRM.MemberReferenceHandle>();

        private readonly IDictionary<Tuple<string, SRM.BlobHandle>, SRM.TypeSpecificationHandle> typeSpecificationReferences =
            new Dictionary<Tuple<string, SRM.BlobHandle>, SRM.TypeSpecificationHandle>();

        private readonly IDictionary<Tuple<string, byte[]>, SRM.MethodSpecificationHandle> methodSpecificationReferences =
            new Dictionary<Tuple<string, byte[]>, SRM.MethodSpecificationHandle>();

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
                assemblyReferences.Add(assemblyReference.Name, metadataContainer.MetadataBuilder.AddAssemblyReference(
                    name: metadataContainer.MetadataBuilder.GetOrAddString(assemblyReference.Name),
                    version: assemblyReference.Version,
                    culture: metadataContainer.MetadataBuilder.GetOrAddString(assemblyReference.Culture),
                    publicKeyOrToken: metadataContainer.MetadataBuilder.GetOrAddBlob(assemblyReference.PublicKey),
                    flags: default,
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
                        case IType iType when iType is ArrayType || iType is PointerType  || iType is IGenericParameterReference:
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

            var key = type.GetFullName();
            if (!typeReferences.TryGetValue(key, out var typeReference))
            {
                SRM.EntityHandle resolutionScope;
                if (type.ContainingType == null) // if defined in the namespace then search there
                {
                    resolutionScope = type.ContainingAssembly.Name.Equals(assembly.Name)
                        ? metadataContainer.ModuleHandle
                        : (SRM.EntityHandle) assemblyReferences[type.ContainingAssembly.Name];
                }
                else
                {
                    // if not, recursively get a reference for the containing type and use that as the resolution scope
                    resolutionScope = GetOrAddTypeReference(type.ContainingType);
                }

                typeReference = metadataContainer.MetadataBuilder.AddTypeReference(
                    resolutionScope: resolutionScope,
                    @namespace: metadataContainer.MetadataBuilder.GetOrAddString(type.ContainingNamespace),
                    name: metadataContainer.MetadataBuilder.GetOrAddString(TypeNameOf(type)));
                typeReferences.Add(key, typeReference);
            }

            return typeReference;
        }

        private SRM.TypeSpecificationHandle GetOrAddTypeSpecificationFor(IType type)
        {
            var signature = new SRM.BlobBuilder();
            var encoder = new ECMA335.BlobEncoder(signature).TypeSpecificationSignature();
            Encode(type, encoder);
            var blobHandle = metadataContainer.MetadataBuilder.GetOrAddBlob(signature);
            var key = new Tuple<string, SRM.BlobHandle>(type.GetFullName(), blobHandle);
            if (!typeSpecificationReferences.TryGetValue(key, out var typeSpecification))
            {
                typeSpecification = metadataContainer.MetadataBuilder.AddTypeSpecification(blobHandle);
                typeSpecificationReferences.Add(key, typeSpecification);
            }

            return typeSpecification;
        }

        private SRM.MethodSpecificationHandle GetOrAddMethodSpecificationFor(IMethodReference method, SRM.BlobBuilder signature)
        {
            var genericMethodSignature = methodSignatureGenerator.GenerateSignatureOf(method.GenericMethod);
            var key = new Tuple<string, byte[]>(
                $"{method.GenericMethod.ContainingType.GetFullName()}.{method.GenericName}",
                genericMethodSignature.ToArray()
            );
            if (!methodSpecificationReferences.TryGetValue(key, out var methodSpecification))
            {
                methodSpecification = metadataContainer.MetadataBuilder.AddMethodSpecification(
                    GetOrAddMethodReference(method.GenericMethod, genericMethodSignature),
                    metadataContainer.MetadataBuilder.GetOrAddBlob(signature)
                );
                methodSpecificationReferences.Add(key, methodSpecification);
            }

            return methodSpecification;
        }

        public SRM.StandaloneSignatureHandle GetOrAddStandaloneSignature(SRM.BlobBuilder signature)
        {
            var blobHandle = metadataContainer.MetadataBuilder.GetOrAddBlob(signature);
            if (!standaloneSignatureReferences.TryGetValue(blobHandle, out var standaloneSignature))
            {
                standaloneSignature = metadataContainer.MetadataBuilder.AddStandaloneSignature(blobHandle);
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
            else if (method.ContainingType is ArrayTypeWrapper arrayTypeWrapper)
            {
                var parentHandle = GetOrAddTypeSpecificationFor(arrayTypeWrapper.Type);
                var blobHandle = metadataContainer.MetadataBuilder.GetOrAddBlob(signature);
                var key = new Tuple<object, byte[]>(parentHandle, signature.ToArray());
                if (!memberReferences.TryGetValue(key, out var methodReferenceHandle))
                {
                    methodReferenceHandle = metadataContainer.MetadataBuilder.AddMemberReference(
                        parent: parentHandle,
                        name: metadataContainer.MetadataBuilder.GetOrAddString(method.Name),
                        signature: blobHandle);
                    memberReferences.Add(key, methodReferenceHandle);
                }

                return methodReferenceHandle;
            }
            else
            {
                var blobHandle = metadataContainer.MetadataBuilder.GetOrAddBlob(signature);
                var key = new Tuple<object, byte[]>(
                    $"{method.ContainingType.GetFullName()}.{method.GenericName}",
                    signature.ToArray()
                );
                if (!memberReferences.TryGetValue(key, out var methodReferenceHandle))
                {
                    methodReferenceHandle = metadataContainer.MetadataBuilder.AddMemberReference(
                        parent: GetOrAddTypeReference(method.ContainingType),
                        name: metadataContainer.MetadataBuilder.GetOrAddString(method.Name),
                        signature: blobHandle);
                    memberReferences.Add(key, methodReferenceHandle);
                }

                return methodReferenceHandle;
            }
        }

        private SRM.MemberReferenceHandle GetOrAddFieldReference(IFieldReference field, SRM.BlobBuilder signature)
        {
            var blobHandle = metadataContainer.MetadataBuilder.GetOrAddBlob(signature);
            var key = new Tuple<object, byte[]>(
                $"{field.ContainingType.GetFullName()}.{field.Name}",
                signature.ToArray()
            );
            if (!memberReferences.TryGetValue(key, out var memberReferenceHandle))
            {
                memberReferenceHandle = metadataContainer.MetadataBuilder.AddMemberReference(
                    parent: GetOrAddTypeReference(field.ContainingType),
                    name: metadataContainer.MetadataBuilder.GetOrAddString(field.Name),
                    signature: blobHandle);
                memberReferences.Add(key, memberReferenceHandle);
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
            else if (type.Equals(PlatformTypes.IntPtr)) encoder.IntPtr();
            else if (type.Equals(PlatformTypes.UIntPtr)) encoder.UIntPtr();
            else
            {
                switch (type)
                {
                    case IBasicType iBasicType:
                    {
                        var isValueType = type.TypeKind == TypeKind.ValueType;
                        if (iBasicType.IsGenericInstantiation())
                        {
                            var genericInstantiation = encoder.GenericInstantiation(
                                GetOrAddTypeReference(iBasicType.GenericType),
                                iBasicType.GenericArguments.Count,
                                isValueType);
                            foreach (var genericArg in iBasicType.GenericArguments)
                            {
                                Encode(genericArg, genericInstantiation.AddArgument());
                            }
                        }
                        else
                        {
                            encoder.Type(GetOrAddTypeReference(iBasicType), isValueType);
                        }

                        break;
                    }
                    case ArrayType arrayType:
                    {
                        if (arrayType.IsVector)
                        {
                            Encode(arrayType.ElementsType, encoder.SZArray());
                        }
                        else
                        {
                            encoder.Array(
                                elementTypeEncoder => Encode(arrayType.ElementsType, elementTypeEncoder),
                                arrayShapeEncoder =>
                                {
                                    /**
                                     * This assumes that all dimensions have 0 as lower bound and none declare sizes.
                                     * Lower bounds and sizes are not modelled. 
                                     */
                                    var lowerBounds = Repeat(0, (int) arrayType.Rank).ToImmutableArray();
                                    var sizes = ImmutableArray<int>.Empty;
                                    arrayShapeEncoder.Shape((int) arrayType.Rank, sizes, lowerBounds);
                                });
                        }


                        break;
                    }
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
                    case FunctionPointerType _:
                    {
                        encoder.FunctionPointer();
                        break;
                    }
                    case IGenericParameterReference genericParameter:
                    {
                        switch (genericParameter.Kind)
                        {
                            case GenericParameterKind.Type:
                                encoder.GenericTypeParameter(genericParameter.Index);
                                break;
                            case GenericParameterKind.Method:
                                encoder.GenericMethodTypeParameter(genericParameter.Index);
                                break;
                            default:
                                throw new UnhandledCase();
                        }

                        break;
                    }
                    default:
                        throw new Exception($"Type {type} not supported");
                }
            }
        }
    }
}