using System;
using System.Collections.Immutable;
using Model;
using Model.Types;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using static System.Linq.Enumerable;

namespace MetadataGenerator.Generation.Types
{
    internal class TypeEncoder
    {
        private readonly HandleResolver handleResolver;

        public TypeEncoder(HandleResolver handleResolver)
        {
            this.handleResolver = handleResolver;
        }

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
                                handleResolver.HandleOf(iBasicType.GenericType),
                                iBasicType.GenericArguments.Count,
                                isValueType);
                            foreach (var genericArg in iBasicType.GenericArguments)
                            {
                                Encode(genericArg, genericInstantiation.AddArgument());
                            }
                        }
                        else
                        {
                            encoder.Type(handleResolver.HandleOf(iBasicType), isValueType);
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
                                    // This assumes that all dimensions have 0 as lower bound and none declare sizes.
                                    // Lower bounds and sizes are not modelled. 
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
                                throw genericParameter.Kind.ToUnknownValueException();
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