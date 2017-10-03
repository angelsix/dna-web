using System;

namespace Dna.Web.Core
{
    /// <summary>
    /// A log message for an engine event
    /// </summary>
    public class LogMessage
    {
        /// <summary>
        /// The type of the log message
        /// </summary>
        public LogType Type { get; set; }

        /// <summary>
        /// The log title
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// The log message
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// The time of the log
        /// </summary>
        public DateTimeOffset Time { get; set; }
    }
}
