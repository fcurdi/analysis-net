using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Model;
using Assembly = Model.Assembly;

namespace MetadataGenerator
{
    public class Generator : IGenerator
    {

        private static readonly Guid s_guid = new Guid("97F4DBD4-F6D1-4FAD-91B3-1001F92068E5"); //FIXME: ??
        private static readonly BlobContentId s_contentId = new BlobContentId(s_guid, 0x04030201); //FIXME: ??

        public void Generate(Assembly assembly)
        {

            using (var peStream = File.OpenWrite($"./{assembly.Name}.dll"))
            {
                var metadata = new MetadataBuilder();

                metadata.AddAssembly(
                    name: metadata.GetOrAddString(assembly.Name),
                    version: new Version(1, 0, 0, 0), // FIXME ??
                    culture: default(StringHandle), // FIXME ??
                    publicKey: default(BlobHandle), // FIXME ??
                    flags: AssemblyFlags.PublicKey, // FIXME ??
                    hashAlgorithm: AssemblyHashAlgorithm.Sha1); // FIXME ??

                metadata.AddModule(
                    generation: 0, // FIXME ??
                    moduleName: metadata.GetOrAddString($"{assembly.Name}.dll"),
                    mvid: metadata.GetOrAddGuid(s_guid), // FIXME ??
                    encId: default(GuidHandle), // FIXME ??
                    encBaseId: default(GuidHandle)); // FIXME ??

                var mscorlibAssemblyRef = metadata.AddAssemblyReference(
                    name: metadata.GetOrAddString("mscorlib"),
                    version: new Version(4, 0, 0, 0), //FIXME ??
                    culture: default(StringHandle), //FIXME ??
                     publicKeyOrToken: metadata.GetOrAddBlob(ImmutableArray.Create<byte>(0xB7, 0x7A, 0x5C, 0x56, 0x19, 0x34, 0xE0, 0x89)),//FIXME ??
                    flags: default(AssemblyFlags), //FIXME ??
                    hashValue: default(BlobHandle));//FIXME ??

                var systemObjectTypeRef = metadata.AddTypeReference(
                    resolutionScope: mscorlibAssemblyRef,
                    @namespace: metadata.GetOrAddString("System"),
                    name: metadata.GetOrAddString("Object"));

                var systemEnumTypeRef = metadata.AddTypeReference(
                    resolutionScope: mscorlibAssemblyRef,
                    @namespace: metadata.GetOrAddString("System"),
                    name: metadata.GetOrAddString("Enum"));

                var ilBuilder = new BlobBuilder();
                var methodBodyStream = new MethodBodyStreamEncoder(ilBuilder);
                var metadataTokensMethodsOffset = 1;
                var metadataTokensFieldsOffset = 1;
                foreach (var namezpace in assembly.RootNamespace.Namespaces)
                {
                    foreach (var typeDefinition in namezpace.Types)
                    {
                        MethodDefinitionHandle? firstMethodHandle = null;
                        FieldDefinitionHandle? firstFieldHandle = null;
                        if (typeDefinition.Kind.Equals(Model.Types.TypeDefinitionKind.Class))
                        {

                            foreach (var method in typeDefinition.Methods)
                            {
                                var methodHandle = GenerateMethod(metadata, ref methodBodyStream, method);
                                if (!firstMethodHandle.HasValue)
                                {
                                    firstMethodHandle = methodHandle;
                                }

                            }

                            metadataTokensMethodsOffset += typeDefinition.Methods.Count;

                            metadata.AddTypeDefinition(
                                attributes: ClassTypeAttributesFor(typeDefinition),
                                @namespace: metadata.GetOrAddString(namezpace.Name),
                                name: metadata.GetOrAddString(typeDefinition.Name),
                                baseType: systemObjectTypeRef, //FIXME  podria ser una subclase de otra cosa
                                fieldList: firstFieldHandle ?? MetadataTokens.FieldDefinitionHandle(metadataTokensFieldsOffset),
                                //FIXME ---> Por ahora funca pero MetadataTokens parece ser global. Si proceso mas de un assembly seguro accedo a los fields incorrectos
                                /// If the type declares fields the handle of the first one, otherwise the handle of the first field declared by the next type definition.
                                /// If no type defines any fields in the module, <see cref="MetadataTokens.FieldDefinitionHandle(int)"/>(1).
                                methodList: firstMethodHandle ?? MetadataTokens.MethodDefinitionHandle(metadataTokensMethodsOffset));
                            //FIXME ---> Por ahora funca pero MetadataTokens parece ser global. Si proceso mas de un assembly seguro accedo a los metodos incorrectos
                            /// If the type declares methods the handle of the first one, otherwise the handle of the first method declared by the next type definition.
                            /// If no type defines any methods in the module, <see cref="MetadataTokens.MethodDefinitionHandle(int)"/>(1).

                        }
                        else if (typeDefinition.Kind.Equals(Model.Types.TypeDefinitionKind.Enum))
                        {
                            var fieldSignatureBlobBuilder = new BlobBuilder();
                            EncodeType(
                                typeDefinition.Fields.First().Type, //FIXME first if empty
                                new BlobEncoder(fieldSignatureBlobBuilder)
                                .FieldSignature());

                            metadataTokensFieldsOffset++;

                            firstFieldHandle = metadata.AddFieldDefinition(
                                attributes: FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName,
                                name: metadata.GetOrAddString("value__"),
                                signature: metadata.GetOrAddBlob(fieldSignatureBlobBuilder));

                            var selfTypeDefinitionHandle = metadata.AddTypeDefinition(
                                attributes: EnumTypeAttributesFor(typeDefinition),
                                @namespace: metadata.GetOrAddString(namezpace.Name),
                                name: metadata.GetOrAddString(typeDefinition.Name),
                                baseType: systemEnumTypeRef,
                                fieldList: firstFieldHandle.Value,
                                //FIXME ---> Por ahora funca pero MetadataTokens parece ser global. Si proceso mas de un assembly seguro accedo a los fields incorrectos
                                /// If the type declares fields the handle of the first one, otherwise the handle of the first field declared by the next type definition.
                                /// If no type defines any fields in the module, <see cref="MetadataTokens.FieldDefinitionHandle(int)"/>(1).
                                methodList: MetadataTokens.MethodDefinitionHandle(metadataTokensMethodsOffset));
                            //FIXME ---> Por ahora funca pero MetadataTokens parece ser global. Si proceso mas de un assembly seguro accedo a los metodos incorrectos
                            /// If the type declares methods the handle of the first one, otherwise the handle of the first method declared by the next type definition.
                            /// If no type defines any methods in the module, <see cref="MetadataTokens.MethodDefinitionHandle(int)"/>(1).

                            typeDefinition.Fields
                                .Where(field => !field.Name.Equals("value__"))
                                .ToList()
                                .ForEach(field =>
                                {
                                    fieldSignatureBlobBuilder.Clear();
                                    new BlobEncoder(fieldSignatureBlobBuilder)
                                        .FieldSignature()
                                        .Type(selfTypeDefinitionHandle, true);

                                    metadata.AddConstant(
                                        metadata.AddFieldDefinition(
                                            attributes: FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal,
                                            name: metadata.GetOrAddString(field.Name),
                                            signature: metadata.GetOrAddBlob(fieldSignatureBlobBuilder)),
                                        field.Value.Value);

                                    metadataTokensFieldsOffset++;
                                });
                        }
                        else if (typeDefinition.Kind.Equals(Model.Types.TypeDefinitionKind.Interface))
                        {


                            foreach (var method in typeDefinition.Methods)
                            {
                                var methodHandle = GenerateMethod(metadata, ref methodBodyStream, method);
                                if (!firstMethodHandle.HasValue)
                                {

                                    firstMethodHandle = methodHandle;
                                }
                            }

                            metadataTokensMethodsOffset += typeDefinition.Methods.Count;

                            /* //FIXME properties
                            var propertySignatureBlogBuilder = new BlobBuilder();
                            new BlobEncoder(propertySignatureBlogBuilder)
                                .PropertySignature(isInstanceProperty: true) //FIXME when false?
                                .Parameters(
                                0,
                                returnType =>
                                {
                                    returnType.Type().Int32();
                                },
                                parameters =>
                                {
                                });

                            metadata.AddProperty(
                                attributes: PropertyAttributes.None,
                                name: metadata.GetOrAddString("get_Prop"),
                                signature: metadata.GetOrAddBlob(propertySignatureBlogBuilder));
                                */
                            metadata.AddTypeDefinition(
                                attributes: InterfaceTypeAttributesFor(typeDefinition),
                                @namespace: metadata.GetOrAddString(namezpace.Name),
                                name: metadata.GetOrAddString(typeDefinition.Name),
                                baseType: default(EntityHandle),
                                fieldList: default(FieldDefinitionHandle), //FIXME props
                                                                           //FIXME ---> Por ahora funca pero MetadataTokens parece ser global. Si proceso mas de un assembly seguro accedo a los fields incorrectos
                                                                           /// If the type declares fields the handle of the first one, otherwise the handle of the first field declared by the next type definition.
                                                                           /// If no type defines any fields in the module, <see cref="MetadataTokens.FieldDefinitionHandle(int)"/>(1).
                                methodList: firstMethodHandle ?? MetadataTokens.MethodDefinitionHandle(metadataTokensMethodsOffset));
                            //FIXME ---> Por ahora funca pero MetadataTokens parece ser global. Si proceso mas de un assembly seguro accedo a los metodos incorrectos
                            /// If the type declares methods the handle of the first one, otherwise the handle of the first method declared by the next type definition.
                            /// If no type defines any methods in the module, <see cref="MetadataTokens.MethodDefinitionHandle(int)"/>(1).
                        }

                    }
                }
                var peHeaderBuilder = new PEHeaderBuilder(imageCharacteristics: Characteristics.Dll);
                var peBuilder = new ManagedPEBuilder(
                               header: peHeaderBuilder,
                               metadataRootBuilder: new MetadataRootBuilder(metadata),
                               ilStream: ilBuilder,
                               entryPoint: default(MethodDefinitionHandle),
                               flags: CorFlags.ILOnly | CorFlags.StrongNameSigned,
                               deterministicIdProvider: content => s_contentId);
                var peBlob = new BlobBuilder();
                var contentId = peBuilder.Serialize(peBlob);
                peBlob.WriteContentTo(peStream);
            }

        }

        private static TypeAttributes EnumTypeAttributesFor(Model.Types.TypeDefinition typeDefinition)
        {
            return TypeAttributes.Class |
                (Model.Types.VisibilityKind.Public.Equals(typeDefinition.Visibility) ? TypeAttributes.Public : TypeAttributes.NotPublic) |
                TypeAttributes.Sealed;
        }

        private static TypeAttributes ClassTypeAttributesFor(Model.Types.TypeDefinition typeDefinition)
        {
            return TypeAttributes.Class |
                  TypeAttributes.BeforeFieldInit |
                  (Model.Types.VisibilityKind.Public.Equals(typeDefinition.Visibility) ? TypeAttributes.Public : TypeAttributes.NotPublic);
        }

        private static TypeAttributes InterfaceTypeAttributesFor(Model.Types.TypeDefinition typeDefinition)
        {
            return TypeAttributes.Interface | TypeAttributes.Public | TypeAttributes.Abstract;
        }

        private MethodDefinitionHandle GenerateMethod(MetadataBuilder metadata, ref MethodBodyStreamEncoder methodBodyStream, Model.Types.MethodDefinition method)
        {
            var methodSignature = new BlobBuilder();
            new BlobEncoder(methodSignature)
                .MethodSignature(isInstanceMethod: true) //FIXME when false?
                .Parameters(
                    method.Parameters.Count,
                    returnType =>
                    {
                        if (method.ReturnType.Equals(Model.Types.PlatformTypes.Void))
                        {
                            returnType.Void();
                        }
                        else
                        {
                            EncodeType(method.ReturnType, returnType.Type());
                        }

                    },
                    parameters =>
                    {
                        foreach (var parameter in method.Parameters)
                        {
                            EncodeType(parameter.Type, parameters.AddParameter().Type());
                        }

                    });

            var instructions = new InstructionEncoder(new BlobBuilder());

            instructions.OpCode(ILOpCode.Nop);
            instructions.OpCode(ILOpCode.Ret);

            var methodAttributes =
                (method.IsAbstract ? MethodAttributes.Abstract : 0) |
                (method.IsStatic ? MethodAttributes.Static : 0) |
                (method.IsVirtual ? MethodAttributes.Virtual : 0) |
                // MethodAttributes.NewSlot | FIXME how to know? needed for interface method at least
                (method.IsConstructor ? MethodAttributes.SpecialName | MethodAttributes.RTSpecialName : 0) |
                (method.Name.Equals("get_Prop") || method.Name.Equals("set_Prop") ? MethodAttributes.SpecialName : 0) | //FIXME
                MethodAttributes.HideBySig;

            switch (method.Visibility)
            {
                case Model.Types.VisibilityKind.Public:
                    methodAttributes |= MethodAttributes.Public;
                    break;
                case Model.Types.VisibilityKind.Private:
                    methodAttributes |= MethodAttributes.Private;
                    break;
                case Model.Types.VisibilityKind.Protected:
                    methodAttributes |= MethodAttributes.Family;
                    break;
                case Model.Types.VisibilityKind.Internal:
                    methodAttributes |= MethodAttributes.Assembly;
                    break;
                default:
                    throw method.Visibility.ToUnknownValueException();
            }

            return metadata.AddMethodDefinition(
                attributes: methodAttributes,
                implAttributes: MethodImplAttributes.IL | MethodImplAttributes.Managed, //FIXME
                name: metadata.GetOrAddString(method.Name),
                signature: metadata.GetOrAddBlob(methodSignature),
                bodyOffset: methodBodyStream.AddMethodBody(instructions),
                parameterList: default(ParameterHandle)); //FIXME
        }

        //FIXME: names, type of parameters
        private void EncodeType(Model.Types.IType type, SignatureTypeEncoder signatureTypeEncoder)
        {

            // reflection? FIXME
            //var type = Model.Types.PlatformTypes.Values().FirstOrDefault(t => method.ReturnType.Equals(t));
            //if (type == null)
            //{
            //    throw new Exception("Unknow return type");

            //}
            //else
            //{
            //    var returnTypeMethod = returnType.GetType().GetMethod(type.ToString());
            //    returnTypeMethod.Invoke(returnType, new Object[] { });
            //}

            //FIXME incomplete
            if (type.Equals(Model.Types.PlatformTypes.Boolean))
            {
                signatureTypeEncoder.Boolean();
            }
            else if (type.Equals(Model.Types.PlatformTypes.Byte))
            {
                signatureTypeEncoder.Byte();
            }
            else if (type.Equals(Model.Types.PlatformTypes.SByte))
            {
                signatureTypeEncoder.SByte();
            }
            else if (type.Equals(Model.Types.PlatformTypes.Char))
            {
                signatureTypeEncoder.Char();
            }
            else if (type.Equals(Model.Types.PlatformTypes.Double))
            {
                signatureTypeEncoder.Double();
            }
            else if (type.Equals(Model.Types.PlatformTypes.Int16))
            {
                signatureTypeEncoder.Int16();
            }
            else if (type.Equals(Model.Types.PlatformTypes.UInt16))
            {
                signatureTypeEncoder.UInt16();
            }
            else if (type.Equals(Model.Types.PlatformTypes.Int32))
            {
                signatureTypeEncoder.Int32();
            }
            else if (type.Equals(Model.Types.PlatformTypes.UInt32))
            {
                signatureTypeEncoder.UInt32();
            }
            else if (type.Equals(Model.Types.PlatformTypes.Int64))
            {
                signatureTypeEncoder.Int64();
            }
            else if (type.Equals(Model.Types.PlatformTypes.UInt64))
            {
                signatureTypeEncoder.UInt64();
            }
            else if (type.Equals(Model.Types.PlatformTypes.String))
            {
                signatureTypeEncoder.String();
            }
            else if (type.Equals(Model.Types.PlatformTypes.Single))
            {
                signatureTypeEncoder.Single();
            }
            else
            {
                throw new Exception("Unknown value:" + type.ToString());
            }

        }

    }
}