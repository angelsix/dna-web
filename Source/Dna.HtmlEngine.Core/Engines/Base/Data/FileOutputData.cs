using System.Collections.Generic;

namespace Dna.HtmlEngine.Core
{
    /// <summary>
    /// Information about a file output profile
    /// </summary>
    public class FileOutputData
    {
        /// <summary>
        /// The full output path of the file
        /// </summary>
        public string FullPath { get; set; }

        /// <summary>
        /// The profile name to use for replacing variables and data, if any.
        /// Leave blank to use the default variables and data
        /// </summary>
        public string ProfileName { get; set; }


        /// <summary>
        /// The files full contents, that get's edit during the course of being edit
        /// </summary>
        public string FileContents { get; set; }

        /// <summary>
        /// A list of variables for the file
        /// </summary>
        public List<EngineVariable> Variables { get; set; } = new List<EngineVariable>();
    }
}
