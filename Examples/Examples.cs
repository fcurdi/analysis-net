namespace FirstNamespace
{

    public enum AtTheBeginingEnum
    {
        CONSTANT1,
        CONSTANT2
    }

    public class EmptyClass
    {
    }

    public enum AtTheMiddleEnumOfBytes : byte
    {
        CONSTANT1 = 10,
        CONSTANT2 = 20
    }

    public class ClassWithMethods
    {
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

    }

    public enum AtTheEndEnumOfUShorts : ushort
    {
        CONSTANT1 = 1,
        CONSTANT2 = 2
    }


}

namespace SecondNamespace
{
    public enum EnumOfLongValues : long
    {
        CONSTANT = 2L,
        CONSTANT2 = 50L
    }

    public class AnotherClassWithMethods
    {

        public string Hello()
        {
            return "Hello";
        }

    }

}