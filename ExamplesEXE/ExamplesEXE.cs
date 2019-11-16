using System;
using Other;

namespace ExamplesEXE
{
    public static class MainClass
    {
        public static void Main(string[] args)
        {
            new SomeClass().Print("hola");
        }
    }
}

namespace Other
{
    public class SomeClass
    {
        public void Print<T>(T arg)
        {
            Console.WriteLine(arg);
        }
    }
}