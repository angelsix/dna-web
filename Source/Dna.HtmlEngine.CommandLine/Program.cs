using Dna.HtmlEngine.Core;
using System;
using System.IO;

namespace Dna.HtmlEngine.CommandLine
{
    class Program
    {
        static void Main(string[] args)
        {

            using (var engine = new DnaHtmlEngine())
            {
                // Start the engine
                engine.Start();

                Console.WriteLine("Press enter to stop");
                Console.ReadLine();
            }

            Console.WriteLine("Press any key to close");
            Console.ReadKey();
        }
    }
}