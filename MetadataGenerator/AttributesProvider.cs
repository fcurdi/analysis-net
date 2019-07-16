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
                //TODO Delegate
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
            switch (field.Visibility)
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

        public static MethodAttributes GetAttributesFor(Model.Types.MethodDefinition method)
        {
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
            return methodAttributes;

        }

    }
}
