namespace ExamplesEXE
{
    public class MainClass
    {
        public static void Main(string[] args)
        {
            new Other.SomeClass().Print("hola");
        }
    }
}

namespace Other
{

    public class SomeClass
    {
        public void Print<T>(T arg)
        {
            System.Console.WriteLine();
        }
    }
}
