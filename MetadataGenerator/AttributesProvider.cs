using System.Reflection;
using Model;
using Model.Types;

namespace MetadataGenerator
{
    public static class AttributesProvider
    {
        public static TypeAttributes AttributesFor(TypeDefinition typeDefinition)
        {
            switch (typeDefinition.Kind)
            {
                case TypeDefinitionKind.Class: return ClassTypeAttributes(typeDefinition);
                case TypeDefinitionKind.Enum: return EnumTypeAttributes(typeDefinition);
                case TypeDefinitionKind.Interface: return InterfaceTypeAttributes();
                case TypeDefinitionKind.Struct: return StructTypeAttributes(typeDefinition);
                case TypeDefinitionKind.Delegate: return DelegateTypeAttributes(typeDefinition);
                default: throw typeDefinition.Kind.ToUnknownValueException();
            }
        }

        private static TypeAttributes DelegateTypeAttributes(TypeDefinition typeDefinition) =>
            TypeAttributes.Class |
            TypeAttributes.Sealed |
            (typeDefinition.Serializable ? TypeAttributes.Serializable : 0) |
            VisibilityAttributesFor(typeDefinition);

        private static TypeAttributes StructTypeAttributes(TypeDefinition typeDefinition) =>
            TypeAttributes.Class |
            VisibilityAttributesFor(typeDefinition) |
            LayoutAttributesFor(typeDefinition.LayoutInformation) |
            (typeDefinition.Serializable ? TypeAttributes.Serializable : 0) |
            TypeAttributes.Sealed |
            (typeDefinition.BeforeFieldInit ? TypeAttributes.BeforeFieldInit : 0);

        private static TypeAttributes EnumTypeAttributes(TypeDefinition typeDefinition) =>
            TypeAttributes.Class |
            VisibilityAttributesFor(typeDefinition) |
            (typeDefinition.Serializable ? TypeAttributes.Serializable : 0) |
            TypeAttributes.Sealed;

        private static TypeAttributes ClassTypeAttributes(TypeDefinition typeDefinition) =>
            TypeAttributes.Class |
            (typeDefinition.BeforeFieldInit ? TypeAttributes.BeforeFieldInit : 0) |
            (typeDefinition.IsAbstract ? TypeAttributes.Abstract : 0) |
            (typeDefinition.IsSealed ? TypeAttributes.Sealed : 0) |
            (typeDefinition.IsStatic ? TypeAttributes.Abstract | TypeAttributes.Sealed : 0) |
            (typeDefinition.Serializable ? TypeAttributes.Serializable : 0) |
            LayoutAttributesFor(typeDefinition.LayoutInformation) |
            VisibilityAttributesFor(typeDefinition);

        private static TypeAttributes InterfaceTypeAttributes() => TypeAttributes.Interface | TypeAttributes.Public | TypeAttributes.Abstract;

        public static FieldAttributes AttributesFor(FieldDefinition field)
        {
            var fieldAttributes =
                (field.IsStatic ? FieldAttributes.Static : 0) |
                (field.IsLiteral ? FieldAttributes.Literal : 0) |
                (field.IsReadonly ? FieldAttributes.InitOnly : 0) |
                (field.SpecialName ? FieldAttributes.SpecialName : 0) |
                (field.RuntimeSpecialName ? FieldAttributes.RTSpecialName : 0) |
                (field.SpecifiesRelativeVirtualAddress
                    ? FieldAttributes.HasFieldRVA
                    : field.Value != null
                        ? FieldAttributes.HasDefault
                        : 0);
            switch (field.Visibility)
            {
                case VisibilityKind.Public:
                    fieldAttributes |= FieldAttributes.Public;
                    break;
                case VisibilityKind.Private:
                    fieldAttributes |= FieldAttributes.Private;
                    break;
                case VisibilityKind.Protected:
                    fieldAttributes |= FieldAttributes.Family;
                    break;
                case VisibilityKind.Internal:
                    fieldAttributes |= FieldAttributes.Assembly;
                    break;
                default:
                    throw field.Visibility.ToUnknownValueException();
            }

            return fieldAttributes;
        }

        public static MethodAttributes AttributesFor(MethodDefinition method)
        {
            var methodAttributes =
                MethodAttributes.HideBySig |
                (method.IsAbstract ? MethodAttributes.Abstract : 0) |
                (method.IsStatic ? MethodAttributes.Static : 0) |
                (method.IsVirtual ? MethodAttributes.Virtual : 0) |
                (method.HidesMember ? MethodAttributes.NewSlot : 0) |
                (method.IsSealed ? MethodAttributes.Final : 0) |
                (method.SpecialName ? MethodAttributes.SpecialName : 0) |
                (method.RuntimeSpecialName ? MethodAttributes.RTSpecialName : 0);
            switch (method.Visibility)
            {
                case VisibilityKind.Public:
                    methodAttributes |= MethodAttributes.Public;
                    break;
                case VisibilityKind.Private:
                    methodAttributes |= MethodAttributes.Private;
                    break;
                case VisibilityKind.Protected:
                    methodAttributes |= MethodAttributes.Family;
                    break;
                case VisibilityKind.Internal:
                    methodAttributes |= MethodAttributes.Assembly;
                    break;
                default:
                    throw method.Visibility.ToUnknownValueException();
            }

            return methodAttributes;
        }

        public static ParameterAttributes AttributesFor(MethodParameter parameter)
        {
            var attributes = parameter.HasDefaultValue ? ParameterAttributes.HasDefault : 0;
            switch (parameter.Kind)
            {
                case MethodParameterKind.In:
                    attributes |= ParameterAttributes.In;
                    break;
                case MethodParameterKind.Out:
                    attributes |= ParameterAttributes.Out;
                    break;
                case MethodParameterKind.Ref:
                    // There is no ParameterAttributes.Ref and no Params.Flags related (see ECMA)
                    break;
                case MethodParameterKind.Normal:
                    break;
            }

            if (parameter.HasDefaultValue)
            {
                attributes |= ParameterAttributes.Optional | ParameterAttributes.HasDefault;
            }

            return attributes;
        }

        public static GenericParameterAttributes AttributesFor(GenericParameter genericParameter)
        {
            var attributes = GenericParameterAttributes.None;
            if (genericParameter.DefaultConstructorConstraint)
            {
                attributes |= GenericParameterAttributes.DefaultConstructorConstraint;
            }

            switch (genericParameter.Variance)
            {
                case GenericParameterVariance.COVARIANT:
                    attributes |= GenericParameterAttributes.Covariant;
                    break;
                case GenericParameterVariance.CONTRAVARIANT:
                    attributes |= GenericParameterAttributes.Contravariant;
                    break;
            }

            switch (genericParameter.TypeKind)
            {
                case TypeKind.ValueType:
                    attributes |= GenericParameterAttributes.NotNullableValueTypeConstraint;
                    break;
                case TypeKind.ReferenceType:
                    attributes |= GenericParameterAttributes.ReferenceTypeConstraint;
                    break;
            }

            return attributes;
        }

        private static TypeAttributes VisibilityAttributesFor(TypeDefinition typeDefinition)
        {
            if (typeDefinition.ContainingType != null)
            {
                return VisibilityKind.Public == typeDefinition.Visibility ? TypeAttributes.NestedPublic : TypeAttributes.NestedPrivate;
            }
            else
            {
                return VisibilityKind.Public == typeDefinition.Visibility ? TypeAttributes.Public : TypeAttributes.NotPublic;
            }
        }

        private static TypeAttributes LayoutAttributesFor(LayoutInformation layoutInformation)
        {
            switch (layoutInformation.Kind)
            {
                case LayoutKind.SequentialLayout: return TypeAttributes.SequentialLayout;
                case LayoutKind.ExplicitLayout: return TypeAttributes.ExplicitLayout;
                default: return TypeAttributes.AutoLayout;
            }
        }
    }
}