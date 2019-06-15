namespace FirstNamespace
{

    public enum AtTheBeginingEnum
    {
        CONSTANT1 = 10,
        CONSTANT2 = 12
    }

    public class EmptyClass
    {
    }

    public enum AtTheMiddleEnum
    {
        CONSTANT1,
        CONSTANT2
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

    public enum AtTheEndEnum
    {
        CONSTANT1,
        CONSTANT2
    }


}

namespace SecondNamespace
{
    public enum AnotheEnum
    {
        CONSTANT1,
        CONSTANT2
    }

    public class AnotherClassWithMethods
    {

        public string Hello()
        {
            return "Hello";
        }

    }

}