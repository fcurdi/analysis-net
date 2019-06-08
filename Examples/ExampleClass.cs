namespace Examples
{

    public enum ExampleEnum
    {
        CONSTANT1,
        CONSTANT2
    }

    public class EmptyExampleClass
    {
    }

    public class ExampleClass
    {
        public void DoNothing() { }

        private int Sum(int arg1, int arg2)
        {
            return arg1 + arg2;
        }

        private string ReturnStringArg(string arg)
        {
            return arg;
        }

        double ReturnADobule(double arg)
        {
            return arg;
        }

        //TODO Refernce types
    }

}