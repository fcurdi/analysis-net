using System;
using System.Reflection.Metadata.Ecma335;

namespace MetadataGenerator
{
    public static class TypeEncoder
    {
        //FIXME: names, type of parameters
        public static void Encode(Model.Types.IType type, SignatureTypeEncoder signatureTypeEncoder)
        {
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
