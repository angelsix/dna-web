using System.Collections.Generic;

namespace Dna.HtmlEngine.Core
{
    /// <summary>
    /// Information about a file as it is being processed, for use throughout the stages
    /// </summary>
    public class FileProcessingData
    {
        /// <summary>
        /// The full path of the file
        /// </summary>
        public string FullPath { get; set; }

        /// <summary>
        /// Indicates if this file is a partial file
        /// NOTE: Partial files don't generate output themselves
        /// </summary>
        public bool IsPartial { get; set; }

        /// <summary>
        /// A list of output file data
        /// </summary>
        public List<FileOutputData> OutputPaths { get; set; } = new List<FileOutputData>();

        /// <summary>
        /// Information about any error in processing the file
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Indicates if the processing of this file is still ok and has not had an error
        /// </summary>
        public bool Successful => string.IsNullOrEmpty(Error);


        /// <summary>
        /// The files full contents, that get's edit during the course of being edit
        /// </summary>
        public string UnprocessedFileContents { get; set; }
    }
}
