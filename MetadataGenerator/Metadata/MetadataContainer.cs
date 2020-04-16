using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Model;
using Model.Types;
using Assembly = Model.Assembly;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;
using static MetadataGenerator.Metadata.AttributesProvider;

namespace MetadataGenerator.Metadata
{
    internal class MetadataContainer
    {
        public readonly ECMA335.MetadataBuilder metadataBuilder;
        public readonly MetadataResolver metadataResolver;
        public readonly ECMA335.MethodBodyStreamEncoder methodBodyStream;
        private SRM.MethodDefinitionHandle? mainMethodHandle;
        private SRM.ModuleDefinitionHandle? moduleHandle;
        private readonly ISet<GenericParamRow> genericParameterRows = new HashSet<GenericParamRow>();
        private readonly ISet<NestedTypeRow> nestedTypeRows = new HashSet<NestedTypeRow>();
        private readonly ISet<InterfaceImplementationRow> interfaceImplementationRows = new HashSet<InterfaceImplementationRow>();

        public SRM.MethodDefinitionHandle MainMethodHandle
        {
            get => mainMethodHandle ?? throw new Exception("Main method handle was not set");
            set
            {
                if (mainMethodHandle != null) throw new Exception("Assembly has more than one main method");
                mainMethodHandle = value;
            }
        }

        public SRM.ModuleDefinitionHandle ModuleHandle
        {
            get => moduleHandle ?? throw new Exception("Module handle was not set");
            set
            {
                if (moduleHandle != null) throw new Exception("Multiple modules not supported");
                moduleHandle = value;
            }
        }

        public MetadataContainer(Assembly assembly)
        {
            metadataBuilder = new ECMA335.MetadataBuilder();
            methodBodyStream = new ECMA335.MethodBodyStreamEncoder(new SRM.BlobBuilder());
            metadataResolver = new MetadataResolver(this, assembly);
        }

        public void RegisterGenericParameter(SRM.TypeDefinitionHandle parent, GenericParameter genericParameter) =>
            DoRegisterGenericParameter(parent, genericParameter);

        public void RegisterGenericParameter(SRM.MethodDefinitionHandle parent, GenericParameter genericParameter) =>
            DoRegisterGenericParameter(parent, genericParameter);

        private void DoRegisterGenericParameter(SRM.EntityHandle parent, GenericParameter genericParameter) =>
            genericParameterRows.Add(new GenericParamRow(
                parent,
                GetGenericParameterAttributesFor(genericParameter),
                metadataBuilder.GetOrAddString(genericParameter.Name),
                genericParameter.Index,
                genericParameter.Constraints.Select(type => metadataResolver.HandleOf(type)).ToSet()
            ));

        public void GenerateGenericParameters() =>
            genericParameterRows
                .OrderBy(row => ECMA335.CodedIndex.TypeOrMethodDef(row.parent))
                .ThenBy(row => row.index)
                .ToList()
                .ForEach(row =>
                {
                    var genericParameterHandle = metadataBuilder.AddGenericParameter(
                        row.parent,
                        row.attributes,
                        row.name,
                        row.index
                    );
                    foreach (var constraint in row.constraints)
                    {
                        metadataBuilder.AddGenericParameterConstraint(genericParameterHandle, constraint);
                    }
                });

        public void RegisterNestedType(SRM.TypeDefinitionHandle type, SRM.TypeDefinitionHandle enclosingType) =>
            nestedTypeRows.Add(new NestedTypeRow(type, enclosingType));

        public void GenerateNestedTypes() =>
            nestedTypeRows
                .OrderBy(row => ECMA335.CodedIndex.TypeDefOrRef(row.type))
                .ToList()
                .ForEach(row => metadataBuilder.AddNestedType(row.type, row.enclosingType));

        public void RegisterInterfaceImplementation(SRM.TypeDefinitionHandle type, SRM.EntityHandle implementedInterface) =>
            interfaceImplementationRows.Add(new InterfaceImplementationRow(type, implementedInterface));

        public void GenerateInterfaceImplementations() =>
            interfaceImplementationRows
                .OrderBy(row => ECMA335.CodedIndex.TypeDefOrRef(row.type))
                .ThenBy(row => ECMA335.CodedIndex.TypeDefOrRefOrSpec(row.implementedInterface))
                .ToList()
                .ForEach(row => metadataBuilder.AddInterfaceImplementation(row.type, row.implementedInterface));

        #region Rows

        // FIXME hacen falta los equality members? Deberian mirar todos los campos?

        private class InterfaceImplementationRow
        {
            public readonly SRM.TypeDefinitionHandle type;
            public readonly SRM.EntityHandle implementedInterface;

            public InterfaceImplementationRow(SRM.TypeDefinitionHandle type, SRM.EntityHandle implementedInterface)
            {
                this.type = type;
                this.implementedInterface = implementedInterface;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                var other = (InterfaceImplementationRow) obj;
                return type.Equals(other.type) && implementedInterface.Equals(other.implementedInterface);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (type.GetHashCode() * 397) ^ implementedInterface.GetHashCode();
                }
            }

            public static bool operator ==(InterfaceImplementationRow left, InterfaceImplementationRow right)
            {
                return Equals(left, right);
            }

            public static bool operator !=(InterfaceImplementationRow left, InterfaceImplementationRow right)
            {
                return !Equals(left, right);
            }
        }

        private class GenericParamRow
        {
            public readonly SRM.EntityHandle parent;
            public readonly GenericParameterAttributes attributes;
            public readonly SRM.StringHandle name;
            public readonly ushort index;
            public readonly ISet<SRM.EntityHandle> constraints;

            public GenericParamRow(
                SRM.EntityHandle parent,
                GenericParameterAttributes attributes,
                SRM.StringHandle name,
                ushort index,
                ISet<SRM.EntityHandle> constraints)
            {
                this.parent = parent;
                this.attributes = attributes;
                this.name = name;
                this.index = index;
                this.constraints = constraints;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                GenericParamRow other = (GenericParamRow) obj;
                return parent.Equals(other.parent) && attributes == other.attributes && name.Equals(other.name) && index == other.index &&
                       Equals(constraints, other.constraints);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = parent.GetHashCode();
                    hashCode = (hashCode * 397) ^ (int) attributes;
                    hashCode = (hashCode * 397) ^ name.GetHashCode();
                    hashCode = (hashCode * 397) ^ index.GetHashCode();
                    hashCode = (hashCode * 397) ^ (constraints != null ? constraints.GetHashCode() : 0);
                    return hashCode;
                }
            }

            public static bool operator ==(GenericParamRow left, GenericParamRow right)
            {
                return Equals(left, right);
            }

            public static bool operator !=(GenericParamRow left, GenericParamRow right)
            {
                return !Equals(left, right);
            }
        }

        private class NestedTypeRow
        {
            public readonly SRM.TypeDefinitionHandle type;
            public readonly SRM.TypeDefinitionHandle enclosingType;

            public NestedTypeRow(SRM.TypeDefinitionHandle type, SRM.TypeDefinitionHandle enclosingType)
            {
                this.type = type;
                this.enclosingType = enclosingType;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                var other = (NestedTypeRow) obj;
                return type.Equals(other.type) && enclosingType.Equals(other.enclosingType);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (type.GetHashCode() * 397) ^ enclosingType.GetHashCode();
                }
            }

            public static bool operator ==(NestedTypeRow left, NestedTypeRow right)
            {
                return Equals(left, right);
            }

            public static bool operator !=(NestedTypeRow left, NestedTypeRow right)
            {
                return !Equals(left, right);
            }
        }

        #endregion
    }
}