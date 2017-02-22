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
                // Set color
                switch (message.Type)
                {
                    case LogType.Diagnostic:
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        break;

                    case LogType.Error:
                        Console.ForegroundColor = ConsoleColor.Red;
                        break;

                    case LogType.Information:
                        Console.ForegroundColor = ConsoleColor.White;
                        break;

                    case LogType.Success:
                        Console.ForegroundColor = ConsoleColor.Green;
                        break;

                    case LogType.Warning:
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        break;

                    default:
                        Console.ResetColor();
                        break;
                }

                // Output title
                Console.WriteLine(message.Title);

                // Output detailed message if we have one
                if (!string.IsNullOrEmpty(message.Message))
                    Console.WriteLine(message.Message);

                // Clear color
                Console.ResetColor();
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
