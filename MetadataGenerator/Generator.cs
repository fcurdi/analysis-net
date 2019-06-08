using System;
using System.Collections.Immutable;
using System.IO;
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
                    version: new Version(4, 0, 0, 0),
                    culture: default(StringHandle),
                    publicKeyOrToken: metadata.GetOrAddBlob(ImmutableArray.Create<byte>(0xB7, 0x7A, 0x5C, 0x56, 0x19, 0x34, 0xE0, 0x89)),
                    flags: default(AssemblyFlags),
                    hashValue: default(BlobHandle));

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
                foreach (var namezpace in assembly.RootNamespace.Namespaces)
                {
                    foreach (var typeDefinition in namezpace.Types)
                    {
                        TypeAttributes typeAttributes;
                        MethodDefinitionHandle? firstMethodHandle = null;
                        EntityHandle baseType;
                        if (typeDefinition.Kind.Equals(Model.Types.TypeDefinitionKind.Class))
                        {

                            foreach (var method in typeDefinition.Methods)
                            {
                                var methodSignature = new BlobBuilder();
                                new BlobEncoder(methodSignature).
                                    MethodSignature().
                                    Parameters(
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
                                    // (method.IsExternal ? MethodAttributes.) | // FIXME
                                    (method.IsConstructor ? MethodAttributes.SpecialName | MethodAttributes.RTSpecialName : 0) |
                                    MethodAttributes.Public | // FIXME: how to know?
                                    MethodAttributes.HideBySig; // FIXME: ?

                                var methodDefinitionHandle = metadata.AddMethodDefinition(
                                    attributes: methodAttributes,
                                    implAttributes: MethodImplAttributes.IL | MethodImplAttributes.Managed, //FIXME
                                    name: metadata.GetOrAddString(method.Name),
                                    signature: metadata.GetOrAddBlob(methodSignature),
                                    bodyOffset: methodBodyStream.AddMethodBody(instructions),
                                    parameterList: default(ParameterHandle)); //FIXME

                                if (!firstMethodHandle.HasValue)
                                {
                                    firstMethodHandle = methodDefinitionHandle;
                                }

                            }

                            typeAttributes = //FIXME
                                  TypeAttributes.Class |
                                  TypeAttributes.Public |
                                  TypeAttributes.BeforeFieldInit;
                            baseType = systemObjectTypeRef;
                        }
                        else if (typeDefinition.Kind.Equals(Model.Types.TypeDefinitionKind.Enum))
                        {
                            typeAttributes = TypeAttributes.BeforeFieldInit | TypeAttributes.Public;
                            baseType = systemEnumTypeRef;
                        }
                        else
                        {
                            throw new Exception();
                        }

                        metadata.AddTypeDefinition(
                            attributes: typeAttributes,
                            @namespace: metadata.GetOrAddString(namezpace.Name),
                            name: metadata.GetOrAddString(typeDefinition.Name),
                            baseType: baseType,
                            fieldList: MetadataTokens.FieldDefinitionHandle(1), //FIXME
                            methodList: firstMethodHandle ?? MetadataTokens.MethodDefinitionHandle(1));
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

        //FIXME: names, type of parameters
        private void EncodeType(Model.Types.IType returnType, SignatureTypeEncoder signatureTypeEncoder)
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
            if (returnType.Equals(Model.Types.PlatformTypes.Boolean))
            {
                signatureTypeEncoder.Boolean();
            }
            else if (returnType.Equals(Model.Types.PlatformTypes.Byte))
            {
                signatureTypeEncoder.Byte();
            }
            else if (returnType.Equals(Model.Types.PlatformTypes.Char))
            {
                signatureTypeEncoder.Char();
            }
            else if (returnType.Equals(Model.Types.PlatformTypes.Double))
            {
                signatureTypeEncoder.Double();
            }
            else if (returnType.Equals(Model.Types.PlatformTypes.Int16))
            {
                signatureTypeEncoder.Int16();
            }
            else if (returnType.Equals(Model.Types.PlatformTypes.Int32))
            {
                signatureTypeEncoder.Int32();
            }
            else if (returnType.Equals(Model.Types.PlatformTypes.Int64))
            {
                signatureTypeEncoder.Int64();
            }

            else if (returnType.Equals(Model.Types.PlatformTypes.String))
            {
                signatureTypeEncoder.String();
            }
            else
            {
                throw signatureTypeEncoder.ToUnknownValueException();
            }

        }

    }
}