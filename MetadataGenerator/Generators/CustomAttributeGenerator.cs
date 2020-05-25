using System;
using System.Collections.Immutable;
using MetadataGenerator.Metadata;
using Model;
using Model.ThreeAddressCode.Values;
using Model.Types;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SR = System.Reflection;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generators
{
    internal class CustomAttributeGenerator
    {
        private readonly MetadataContainer metadataContainer;

        public CustomAttributeGenerator(MetadataContainer metadataContainer)
        {
            this.metadataContainer = metadataContainer;
        }

        public void Generate(SRM.EntityHandle owner, CustomAttribute customAttribute)
        {
            var customAttributeEncodedValue = new SRM.BlobBuilder();
            new ECMA335.BlobEncoder(customAttributeEncodedValue)
                .CustomAttributeSignature(
                    fixedArgumentsEncoder =>
                    {
                        foreach (var argument in customAttribute.Arguments)
                        {
                            if (argument.Type is ArrayType)
                            {
                                EncodeVector(customAttribute, argument, fixedArgumentsEncoder.AddArgument());
                            }
                            else if (argument.Type.Equals(PlatformTypes.Type))
                            {
                                EncodeComplexValue(customAttribute, argument, fixedArgumentsEncoder.AddArgument());
                            }
                            else
                            {
                                EncodeSimpleValue(customAttribute, argument, fixedArgumentsEncoder.AddArgument());
                            }
                        }
                    },
                    namedArgumentsEncoder =>
                    {
                        // TODO named arguments are not read in AssemblyExtractor
                        namedArgumentsEncoder.Count(0);
                    });

            metadataContainer.metadataBuilder.AddCustomAttribute(
                owner,
                metadataContainer.metadataResolver.HandleOf(customAttribute.Constructor),
                metadataContainer.metadataBuilder.GetOrAddBlob(customAttributeEncodedValue));
        }

        /*
         * Encodes vector (only SZArray are permitted as arguments of a custom attribute constructor)
         * The constructor can have this value as an object, object[] or type[]. In the first two cases, the type needs to be encoded as well.
         */
        private static void EncodeVector(CustomAttribute customAttribute, Constant argument, ECMA335.LiteralEncoder encoder)
        {
            var type = ((ArrayType) argument.Type).ElementsType;
            var elements = (ImmutableArray<SRM.CustomAttributeTypedArgument<IType>>) argument.Value;
            if (TypeOfMatchedParameterInConstructorIsObject(customAttribute, argument))
            {
                if (type.Equals(PlatformTypes.Object))
                {
                    encoder.TaggedVector(
                        arrayTypeEncoder => arrayTypeEncoder.ObjectArray(),
                        vectorEncoder =>
                        {
                            var literalsEncoder = vectorEncoder.Count(elements.Length);
                            foreach (var element in elements)
                            {
                                literalsEncoder.AddLiteral().TaggedScalar(
                                    typeEncoder => EncodeType(element.Type, typeEncoder),
                                    scalarEncoder => scalarEncoder.Constant(element.Value));
                            }
                        });
                }
                else
                {
                    encoder.TaggedVector(
                        vectorTypeEncoder => EncodeType(type, vectorTypeEncoder.ElementType()),
                        vectorElementsEncoder =>
                        {
                            var literalsEncoder = vectorElementsEncoder.Count(elements.Length);
                            foreach (var element in elements)
                            {
                                literalsEncoder.AddLiteral().Scalar().Constant(element.Value);
                            }
                        });
                }
            }
            else
            {
                if (type.Equals(PlatformTypes.Object))
                {
                    var literalsEncoder = encoder.Vector().Count(elements.Length);
                    foreach (var element in elements)
                    {
                        literalsEncoder.AddLiteral().TaggedScalar(
                            typeEncoder => EncodeType(element.Type, typeEncoder),
                            scalarEncoder => scalarEncoder.Constant(element.Value));
                    }
                }
                else
                {
                    var literalsEncoder = encoder.Vector().Count(elements.Length);
                    foreach (var element in elements)
                    {
                        literalsEncoder.AddLiteral().Scalar().Constant(element.Value);
                    }
                }
            }
        }

        /*
         * Encode types. Types are represented by their serialized name (namespace.type or namespace.type+anotherType if nested)
         * If this parameter has type object in the customAttribute constructor, then its real type also needs to be encoded.
         */
        private static void EncodeComplexValue(CustomAttribute customAttribute, Constant argument, ECMA335.LiteralEncoder encoder)
        {
            var serializedTypeName = ((IBasicType) argument.Value).Name;
            if (TypeOfMatchedParameterInConstructorIsObject(customAttribute, argument))
            {
                encoder.TaggedScalar(
                    typeEncoder => EncodeType(argument.Type, typeEncoder),
                    scalarEncoder => scalarEncoder.SystemType(serializedTypeName));
            }
            else
            {
                encoder.Scalar().SystemType(serializedTypeName);
            }
        }

        /*
         * Simple values: bool (as byte), char, float32, float64, int8, int16, int32, int64, unsigned int8, unsigned int16, unsigned int32,unsigned int64, enums (integer value)
         * If this parameter has type object in the customAttribute constructor, then its real type also needs to be encoded.
         */
        private static void EncodeSimpleValue(CustomAttribute customAttribute, Constant argument, ECMA335.LiteralEncoder encoder)
        {
            var type = argument.Type;
            var value = type.Equals(PlatformTypes.Boolean) ? Convert.ToByte(argument.Value) : argument.Value;
            if (TypeOfMatchedParameterInConstructorIsObject(customAttribute, argument))
            {
                encoder.TaggedScalar(
                    typeEncoder => EncodeType(type, typeEncoder),
                    scalarEncoder => scalarEncoder.Constant(value));
            }
            else
            {
                encoder.Scalar().Constant(value);
            }
        }

        private static bool TypeOfMatchedParameterInConstructorIsObject(CustomAttribute customAttribute, Constant argument) =>
            customAttribute.Constructor.Parameters[customAttribute.Arguments.IndexOf(argument)].Type.Equals(PlatformTypes.Object);

        private static void EncodeType(IType type, ECMA335.CustomAttributeElementTypeEncoder encoder)
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
            else if (type.Equals(PlatformTypes.Type)) encoder.SystemType();
            else encoder.Enum(type.GetFullName());
        }
    }
}