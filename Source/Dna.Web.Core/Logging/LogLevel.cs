namespace Dna.Web.Core
{
    /// <summary>
    /// The level of detail to output in the log
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// Don't log anything
        /// </summary>
        None = 0,

        /// <summary>
        /// Log successful and error messages
        /// </summary>
        Minimal = 3,

        /// <summary>
        /// Log successful, error, warning, attention and information level messages
        /// </summary>
        Informative = 5,

        /// <summary>
        /// Log all messages
        /// </summary>
        All = 6,
    }
}
