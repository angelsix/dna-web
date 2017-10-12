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
        Success = 1,

        /// <summary>
        /// An error message
        /// </summary>
        Error = 2,

        /// <summary>
        /// An information message
        /// </summary>
        Information = 3,

        /// <summary>
        /// A warning message
        /// </summary>
        Attention = 4,

        /// <summary>
        /// A warning message
        /// </summary>
        Warning = 5,

        /// <summary>
        /// A verbose diagnostic level message
        /// </summary>
        Diagnostic = 6,
    }
}
