﻿namespace FirstNamespace
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

    public enum AtTheMiddleEnumOfBytes : byte
    {
        Byte10 = 10,
        Byte20 = 20
    }

    public class ComplexClass
    {

        static readonly double staticDouble = 5.27;
        private readonly int readOnlyIntField = 212;
        public string unassignedString;

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