using System;

namespace FirstNamespace
{

    public enum AtTheBeginingEnum
    {
        CONSTANT1,
        CONSTANT2
    }

    public class ClassWithProperties
    {
        public double AutoImplementedProperty { get; set; }
        public char AnotherAutoImplementedProperty { get; }

        public int PropertyWithBackingField { get => backingField; set => backingField = value; }
        private int backingField;

        public string AnotherPropertyWithBackingField { get => otherBackingField; }
        private string otherBackingField;

    }

    public class EmptyClass
    {
    }

    public static class StaticClass
    {
        static readonly double StaticDouble;

        static StaticClass()
        {
            StaticDouble = 0.5;
        }

    }

    public enum AtTheMiddleEnumOfBytes : byte
    {
        Byte10 = 10,
        Byte20 = 20
    }

    public class ComplexClass
    {
        private readonly int readOnlyIntField = 212;
        public string unassignedString;
        public const string CONST_STRING = "const";

        public void DoNothing() { }

        private int Sum(int arg1, int arg2)
        {

            return arg1 + arg2;
        }

        protected string ReturnStringArg(string arg)
        {
            return arg;
        }

        internal double ReturnADobule(double arg)
        {
            return arg;
        }

        float ReturnAFloat(float arg)
        {
            return arg;
        }

        static void StaticMethod() { }

    }

    public abstract class AbstractClass
    {

        public virtual void VirtualMethod() { }

        public abstract void AbstractMethod();
    }

    public enum AtTheEndEnumOfUShorts : ushort
    {
        USHORT1 = 1,
        USHORT2 = 2
    }


}

namespace SecondNamespace
{
    public enum EnumOfLongValues : long
    {
        LONG2 = 2L,
        LONG50 = 50L
    }

    public class ClassImplementingInterface : ISampleInterface
    {
        public void DoSomething()
        {
        }

    }

    public interface ISampleInterface
    {

        void DoSomething();

        // todo  interface can have properties

    }

}

namespace ThirdNamespace
{

    public class BaseClass
    {
    }

    public class DerivedClass : BaseClass
    {

    }


    public class ClassDerivedFromSystemClass : Exception
    {

    }

    public class ClassDerivedFromAccessibilityClass : Accessibility.CAccPropServicesClass
    {
    }

}

namespace FourthNamespace
{
    public struct EmptyStruct
    {
    }
    public struct ComplexStruct : SecondNamespace.ISampleInterface
    {
        private readonly int x;

        public void DoNothing()
        {
        }

        public void DoSomething()
        {
        }

        // todo  structs can have properties

    }

}

namespace FifthNamespace
{

    public class ClassContainingNestedTypes
    {
        public class NestedClass
        {
        }

        public enum NestedEnum
        {
            CONSTANT1 = 1
        }

        public struct NestedStruct
        {
        }
    }

    public struct StructContainingNestedTypes
    {

        public struct NestedStruct { }

        public enum NestedEnum
        {
            CONSTANT1 = 1
        }

        public class NestedClass
        {
        }
    }

}

//TODO class with fields or methods that return other clases, structs, etc