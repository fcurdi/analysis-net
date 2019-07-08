using System;
using System.Reflection;
using Model;
using Model.Types;

namespace MetadataGenerator
{
    //FIXME class and file name
    public static class AttributesProvider
    {
        public static TypeAttributes GetAttributesFor(Model.Types.TypeDefinition typedefinition)
        {
            switch (typedefinition.Kind)
            {
                case Model.Types.TypeDefinitionKind.Class: return ClassTypeAttributes(typedefinition);
                case Model.Types.TypeDefinitionKind.Enum: return EnumTypeAttributes(typedefinition);
                case Model.Types.TypeDefinitionKind.Interface: return InterfaceTypeAttributes(typedefinition);
                case Model.Types.TypeDefinitionKind.Struct: return StructTypeAttributes(typedefinition);
                default: throw new Exception(); // FIXME
            };
        }

        private static TypeAttributes StructTypeAttributes(TypeDefinition typedefinition)
        {
            return TypeAttributes.Class |
                (Model.Types.VisibilityKind.Public.Equals(typedefinition.Visibility) ? TypeAttributes.Public : TypeAttributes.NotPublic) |
                TypeAttributes.SequentialLayout |
                TypeAttributes.Sealed |
                TypeAttributes.BeforeFieldInit;  //FIXME: when?
        }

        private static TypeAttributes EnumTypeAttributes(Model.Types.TypeDefinition typedefinition)
        {
            return TypeAttributes.Class |
                (Model.Types.VisibilityKind.Public.Equals(typedefinition.Visibility) ? TypeAttributes.Public : TypeAttributes.NotPublic) |
                TypeAttributes.Sealed;
        }

        private static TypeAttributes ClassTypeAttributes(Model.Types.TypeDefinition typedefinition)
        {
            return TypeAttributes.Class |
                  //TODO: static => abstract & sealed and no BeforeFieldInitBeforeFieldInit
                  TypeAttributes.BeforeFieldInit | //FIXME: when?
                  (Model.Types.VisibilityKind.Public.Equals(typedefinition.Visibility) ? TypeAttributes.Public : TypeAttributes.NotPublic);
        }

        private static TypeAttributes InterfaceTypeAttributes(Model.Types.TypeDefinition typedefinition)
        {
            return TypeAttributes.Interface | TypeAttributes.Public | TypeAttributes.Abstract;
        }

        public static FieldAttributes GetAttributesFor(Model.Types.FieldDefinition field)
        {
            var fieldAttributes =
                (field.IsStatic ? FieldAttributes.Static : 0) |
                (field.ContainingType.Kind.Equals(Model.Types.TypeDefinitionKind.Enum) ? FieldAttributes.Literal : 0); //FIXME also applies for const fields. 
            // (field is readonly ? FieldAttributes.InitOnly : 0); //FIXME
            switch (field.Visibility) //TODO extract, duplicated
            {

                case Model.Types.VisibilityKind.Public:
                    fieldAttributes |= FieldAttributes.Public;
                    break;
                case Model.Types.VisibilityKind.Private:
                    fieldAttributes |= FieldAttributes.Private;
                    break;
                case Model.Types.VisibilityKind.Protected:
                    fieldAttributes |= FieldAttributes.Family;
                    break;
                case Model.Types.VisibilityKind.Internal:
                    fieldAttributes |= FieldAttributes.Assembly;
                    break;
                default:
                    throw field.Visibility.ToUnknownValueException();
            }
            return fieldAttributes;
        }
    }
}
