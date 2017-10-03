namespace Dna.Web.Core
{
    /// <summary>
    /// The log message type
    /// </summary>
    public enum LogType
    {
        /// <summary>
        /// A successfull message
        /// </summary>
        Success = 0,

        /// <summary>
        /// A warning message
        /// </summary>
        Warning = 1,

        /// <summary>
        /// An error message
        /// </summary>
        Error = 2,

        /// <summary>
        /// An information message
        /// </summary>
        Information = 3,

        /// <summary>
        /// A verbose diagnostic level message
        /// </summary>
        Diagnostic = 4,
    }
}
