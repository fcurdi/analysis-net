using System;
using System.Collections.Immutable;
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


        //FIXME signatureTypeEncoder should be by reference? or value?
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
                if (type is IBasicType)
                {
                    var basicType = type as IBasicType;
                    if (basicType.GenericArguments.Count > 0)
                    {
                        var genericInstantiation = signatureTypeEncoder.GenericInstantiation(
                             typeReferences.TypeReferenceOf(basicType),
                             basicType.GenericArguments.Count,
                             type.TypeKind == TypeKind.ValueType
                             );
                        foreach (var genericArg in basicType.GenericArguments)
                        {
                            Encode(genericArg, genericInstantiation.AddArgument());
                        }
                    }
                    else
                    {
                        signatureTypeEncoder.Type(typeReferences.TypeReferenceOf(basicType), type.TypeKind == TypeKind.ValueType);
                    }
                }
                else if (type is ArrayType)
                {
                    signatureTypeEncoder.Array(out var elementTypeEncoder, out var arrayShapeEncoder);
                    Encode((type as ArrayType).ElementsType, elementTypeEncoder);
                    arrayShapeEncoder.Shape(
                       (int)(type as ArrayType).Rank,
                       ImmutableArray.Create(1), //FIXME como se el size??
                       ImmutableArray.Create(0));
                }
                else if (type is PointerType)
                {
                    throw new Exception("TODO"); //FIXME 
                }
                else
                {
                    throw new Exception("Type not supported");
                }
            }
        }
    }
}
