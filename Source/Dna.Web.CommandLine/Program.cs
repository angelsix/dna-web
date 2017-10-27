using Dna.Web.Core;
using System.Threading.Tasks;

namespace Dna.Web.CommandLine
{
    class Program
    {
        static void Main(string[] args)
        {
            RunAsync(args).Wait();
        }

        static async Task RunAsync(string[] args)
        {
            var environment = new DnaEnvironment();

            await environment.RunAsync(args);
        }
    }
}