using System;
using System.IO;
using common;

namespace core
{
    class Program
    {
        static void Main(string[] args)
        {
            if (!(args[0] != null && args[0].EndsWith(".json") && File.Exists(args[0])))
            {
                var error = "Can't find options json file.";
                Console.Error.WriteLine(error);
                throw new Exception(error);
            }

            Collector.Run(args[0]);
        }
    }
}
