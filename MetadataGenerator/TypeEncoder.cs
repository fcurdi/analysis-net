using System.Linq;
using System.Reflection.Metadata.Ecma335;
using Model.Types;

namespace MetadataGenerator
{
    public class TypeEncoder
    {

        private readonly TypeReferences typeReferences;

        public TypeEncoder(TypeReferences typeReferences)
        {
            this.typeReferences = typeReferences;
        }

        //FIXME: names, type of parameters
        public void Encode(Model.Types.IType type, SignatureTypeEncoder signatureTypeEncoder)
        {
            //FIXME incomplete: missing some built in types
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
            else if (type.Equals(Model.Types.PlatformTypes.Object))
            {
                signatureTypeEncoder.Object();
            }
            else
            {
                //FIXME: GetOrAddTypeReference needs IBasicType and type is IType. 
                //FIXME does this conversion always work? Equals works? breaks encapsulation
                //FIXME: nonetheless it is a hack
                // var convertedType = type as IBasicType; FIXME casting also breaks encapsulation and could fail
                var convertedType = TypeHelper.GetClassHierarchy(type).First(t => t.Equals(type));
                signatureTypeEncoder.Type(typeReferences.TypeReferenceOf(convertedType), false);
            }

        }

    }
}
