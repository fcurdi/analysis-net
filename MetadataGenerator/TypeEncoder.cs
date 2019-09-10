using System;
using System.Collections.Immutable;
using System.Reflection.Metadata.Ecma335;
using Model.Types;

namespace MetadataGenerator
{
    public class TypeEncoder
    {
        // FIXME not public
        public readonly TypeReferences typeReferences;
        public TypeEncoder(TypeReferences typeReferences)
        {
            this.typeReferences = typeReferences;
        }

        // FIXME signatureTypeEncoder should be by reference? or value?
        public void Encode(IType type, SignatureTypeEncoder signatureTypeEncoder)
        {
            if (type.Equals(PlatformTypes.Boolean))
            {
                signatureTypeEncoder.Boolean();
            }
            else if (type.Equals(PlatformTypes.Byte))
            {
                signatureTypeEncoder.Byte();
            }
            else if (type.Equals(PlatformTypes.SByte))
            {
                signatureTypeEncoder.SByte();
            }
            else if (type.Equals(PlatformTypes.Char))
            {
                signatureTypeEncoder.Char();
            }
            else if (type.Equals(PlatformTypes.Double))
            {
                signatureTypeEncoder.Double();
            }
            else if (type.Equals(PlatformTypes.Int16))
            {
                signatureTypeEncoder.Int16();
            }
            else if (type.Equals(PlatformTypes.UInt16))
            {
                signatureTypeEncoder.UInt16();
            }
            else if (type.Equals(PlatformTypes.Int32))
            {
                signatureTypeEncoder.Int32();
            }
            else if (type.Equals(PlatformTypes.UInt32))
            {
                signatureTypeEncoder.UInt32();
            }
            else if (type.Equals(PlatformTypes.Int64))
            {
                signatureTypeEncoder.Int64();
            }
            else if (type.Equals(PlatformTypes.UInt64))
            {
                signatureTypeEncoder.UInt64();
            }
            else if (type.Equals(PlatformTypes.String))
            {
                signatureTypeEncoder.String();
            }
            else if (type.Equals(PlatformTypes.Single))
            {
                signatureTypeEncoder.Single();
            }
            else if (type.Equals(PlatformTypes.Object))
            {
                signatureTypeEncoder.Object();
            }
            else
            {
                if (type is IBasicType basicType)
                {
                    if (basicType.GenericType != null)
                    {
                        var genericInstantiation = signatureTypeEncoder.GenericInstantiation(
                             typeReferences.TypeReferenceOf(basicType),
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
                        signatureTypeEncoder.Type(typeReferences.TypeReferenceOf(basicType), type.TypeKind == TypeKind.ValueType);
                    }
                }
                else if (type is ArrayType arrayType)
                {
                    signatureTypeEncoder.Array(
                        elementTypeEncoder =>
                        {
                            Encode(arrayType.ElementsType, elementTypeEncoder);
                        },
                        arrayShapeEncoder =>
                        {
                            // FIXME real values for sizes and lowerBounds
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
                        signatureTypeEncoder.VoidPointer();
                    }
                    else
                    {
                        Encode(targetType, signatureTypeEncoder.Pointer());
                    }
                }
                else if (type is GenericParameter genericParameter)
                {
                    switch (genericParameter.Kind)
                    {
                        case GenericParameterKind.Type:
                            signatureTypeEncoder.GenericTypeParameter(genericParameter.Index);
                            break;
                        case GenericParameterKind.Method:
                            signatureTypeEncoder.GenericMethodTypeParameter(genericParameter.Index);
                            break;
                    }
                }
                else
                {
                    throw new Exception($"Type {type} not supported");
                }
            }
        }
    }
}
