using System;
using System.Threading.Tasks;

namespace Dna.HtmlEngine.Core
{
    /// <summary>
    /// An engine that does nothing but listen out for changes and report them to the console
    /// </summary>
    public class DebugEngine : BaseEngine
    {
        #region Constructor

        /// <summary>
        /// Default constructor
        /// </summary>
        public DebugEngine()
        {
            // Listen out for all events and write them
            this.Started += () => Console.WriteLine($"Engine started listening to '{this.MonitorPath}' with {this.ProcessDelay}ms delay...");
            this.Stopped += () => Console.WriteLine("Engine stopped...");
            this.StartedWatching += (extension) => Console.WriteLine($"Engine listening for file type {extension}");
            this.StoppedWatching += (extension) => Console.WriteLine($"Engine stopped listening for file type {extension}");
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        protected override Task<EngineProcessResult> ProcessFile(string path)
        {
            // Report processing to console
            Console.WriteLine($"Processed file {path}");

            // Return success
            return Task.FromResult(new EngineProcessResult
            {
                Success = true,
                Path = path,
            });
        }
    }
}
