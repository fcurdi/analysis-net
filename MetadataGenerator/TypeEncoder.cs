using System;
using System.Collections.Immutable;
using Model.Types;
using ECMA335 = System.Reflection.Metadata.Ecma335;

namespace MetadataGenerator
{
    public class TypeEncoder
    {
        private readonly ReferencesProvider referencesProvider;
        public TypeEncoder(ReferencesProvider referencesProvider)
        {
            this.referencesProvider = referencesProvider;
        }

        // SignatureTypeEncoder is a struct but it is not necessary to pass it by reference since 
        // it operates on its Builder (BlobBuilber) which is a class (tha means the builder refernece is always the same)
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
                if (type is IBasicType basicType)
                {
                    if (basicType.GenericType != null)
                    {
                        var genericInstantiation = encoder.GenericInstantiation(
                             referencesProvider.TypeReferenceOf(basicType),
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
                        encoder.Type(referencesProvider.TypeReferenceOf(basicType), type.TypeKind == TypeKind.ValueType);
                    }
                }
                else if (type is ArrayType arrayType)
                {
                    encoder.Array(
                        elementTypeEncoder => Encode(arrayType.ElementsType, elementTypeEncoder),
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
                        encoder.VoidPointer();
                    }
                    else
                    {
                        Encode(targetType, encoder.Pointer());
                    }
                }
                else if (type is GenericParameter genericParameter)
                {
                    switch (genericParameter.Kind)
                    {
                        case GenericParameterKind.Type:
                            encoder.GenericTypeParameter(genericParameter.Index);
                            break;
                        case GenericParameterKind.Method:
                            encoder.GenericMethodTypeParameter(genericParameter.Index);
                            break;
                    }
                }
                else throw new Exception($"Type {type} not supported");
            }
        }
    }
}