using System;

namespace Dna.Web.Core
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
            LogMessage += (message) =>
            {
                // Log message
                message.Write(DnaEnvironment?.Configuration.LogLevel ?? LogLevel.All);
            };
        }

        #endregion

        #region Public Properties

        public override string EngineName => "Debug";

        #endregion
    }
}
