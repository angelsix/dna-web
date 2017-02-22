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
            this.LogMessage += (message) =>
            {
                Console.WriteLine(message.Title);
            };
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        protected override Task<EngineProcessResult> ProcessFile(string path)
        {
            // Return success
            return Task.FromResult(new EngineProcessResult
            {
                Success = true,
                Path = path,
            });
        }
    }
}
