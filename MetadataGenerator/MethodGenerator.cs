using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Model;

namespace MetadataGenerator
{
    public class MethodGenerator
    {
        private readonly MetadataBuilder metadata;
        private readonly MethodBodyStreamEncoder methodBodyStream;

        public MethodGenerator(MetadataBuilder metadata, ref MethodBodyStreamEncoder methodBodyStream)
        {
            this.metadata = metadata;
            this.methodBodyStream = methodBodyStream;
        }

        public MethodDefinitionHandle Generate(Model.Types.MethodDefinition method)
        {
            var methodSignature = new BlobBuilder();
            new BlobEncoder(methodSignature)
                .MethodSignature(isInstanceMethod: !method.IsStatic) //FIXME ?
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
                            TypeEncoder.Encode(method.ReturnType, returnType.Type());
                        }

                    },
                    parameters =>
                    {
                        foreach (var parameter in method.Parameters)
                        {
                            TypeEncoder.Encode(parameter.Type, parameters.AddParameter().Type());
                        }
                    });

            var instructions = new InstructionEncoder(new BlobBuilder());

            instructions.OpCode(ILOpCode.Nop);
            instructions.OpCode(ILOpCode.Ret);

            var methodAttributes =
                (method.IsAbstract ? MethodAttributes.Abstract : 0) |
                (method.IsStatic ? MethodAttributes.Static : 0) |
                (method.IsVirtual ? MethodAttributes.Virtual : 0) |
                (method.ContainingType.Kind.Equals(Model.Types.TypeDefinitionKind.Interface) ? MethodAttributes.NewSlot : 0) | // FIXME not entirely correct
                (method.IsConstructor ? MethodAttributes.SpecialName | MethodAttributes.RTSpecialName : 0) | //FIXME should do the same for class constructor (cctor)
                (method.Name.StartsWith("get_") || method.Name.StartsWith("set_") ? MethodAttributes.SpecialName : 0) | //FIXME
                MethodAttributes.HideBySig; //FIXME when?

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


    }
}
