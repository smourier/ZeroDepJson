using System;
using ZeroDep;

namespace ZeroDepJsonCore
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(Json.Serialize(true));
        }
    }
}
