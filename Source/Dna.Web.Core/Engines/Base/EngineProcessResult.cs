namespace Dna.Web.Core
{
    /// <summary>
    /// The results of an engine processing a file
    /// </summary>
    public class EngineProcessResult
    {
        /// <summary>
        /// If the processing succeeded
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The file path that caused the engine process to start
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// A list, if any, of the generated output files
        /// </summary>
        public string[] GeneratedFiles { get; set; }

        /// <summary>
        /// If the process failed, the error message
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// True if the processing of the file was skipped
        /// </summary>
        public bool SkippedProcessing { get; set; }
    }
}
