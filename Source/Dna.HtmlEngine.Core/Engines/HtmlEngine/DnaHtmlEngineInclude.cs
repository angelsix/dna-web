using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Dna.HtmlEngine.Core
{
    public partial class DnaHtmlEngine
    {
        /// <summary>
        /// Processes an Include command to replace a tag with the contents of another file
        /// </summary>
        /// <param name="path">The file that is being edit</param>
        /// <param name="fileContents">The full file contents to edit</param>
        /// <param name="includePath">The include path, typically a relative path</param>
        /// <param name="outputPaths">The list of output names, can be changed by tags</param>
        /// <param name="match">The original match that found this information</param>
        /// <param name="error">Set the error if there is a failure</param>
        private bool ProcessCommandInclude(string path, ref string fileContents, List<string> outputPaths, string includePath, Match match, out string error)
        {
            // No error to start with
            error = string.Empty;

            // Try and find the include file
            var includedContents = FindIncludeFile(path, includePath);

            // If we didn't find it, error out
            if (includedContents == null)
            {
                error = $"Include file not found {includePath}";
                return false;
            }

            // Otherwise we got it, so replace the tag with the contents
            ReplaceTag(ref fileContents, match, includedContents);

            // All done
            return true;
        }

        /// <summary>
        /// Searches for an input file in certain locations relative to the input file
        /// and returns the contents of it if found. 
        /// Returns null if not found
        /// </summary>
        /// <param name="path">The input path of the orignal file</param>
        /// <param name="includePath">The include path of the file trying to be included</param>
        /// <returns></returns>
        private string FindIncludeFile(string path, string includePath)
        {
            // First look in the same folder
            var foundPath = Path.Combine(Path.GetDirectoryName(path), includePath);

            // If we found it, return contents
            if (FileExists(foundPath))
                return File.ReadAllText(foundPath);

            // Not found
            return null;
        }
    }
}
