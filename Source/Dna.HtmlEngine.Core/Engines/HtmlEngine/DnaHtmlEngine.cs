using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Dna.HtmlEngine.Core
{
    /// <summary>
    /// An engine that processes the Dna HTML format
    /// </summary>
    public partial class DnaHtmlEngine : DebugEngine
    {
        #region Private Members

        /// <summary>
        /// The regex to match special tags
        /// For example: <!--@ include header --> to include the file header._dnaweb or header.dnaweb if found
        /// </summary>
        private string mRegex2Group = @"<!--@\s*(\w+)\s*(.*?)\s*-->";

        #endregion

        #region Constructor

        /// <summary>
        /// Default constructor
        /// </summary>
        public DnaHtmlEngine()
        {
            // Set input extensions
            EngineExtensions = new List<string> { "*.dnaweb", "*._dnaweb" };

            // Set output extension
            OutputExtension = ".html";
        }

        #endregion

        protected override async Task<EngineProcessResult> ProcessFile(string path)
        {
            // Let debugger report output
            await base.ProcessFile(path);

            // Now process the file

            // Create a new list of outputs
            var outputPaths = new List<string>();
            
            // Make sure the file exists
            if (!FileExists(path))
                return new EngineProcessResult { Success = false, Path = path, Error = "File no longer exists" };

            // Read all the file into memory (it's ok we will never have large files they are text web files)
            var fileContents = ReadFile(path);

            // Find all special tags that have 2 groups
            var tags = Regex.Match(fileContents, mRegex2Group);

            // Process those tags
            if (!ProcessTags(path, ref fileContents, outputPaths, tags, out string error))
                // If any failed, return the failure
                return new EngineProcessResult { Success = false, Path = path, Error = error };

            // All ok, generate HTML files

            // If we have no outputs specified, add the default
            if (outputPaths.Count == 0)
                // Get default output name
                outputPaths.Add(GetDefaultOutputPath(path));

                outputPaths.ForEach(outputPath =>
                {
                    try
                    {
                        SaveFile(fileContents, outputPath);
                    }
                    catch (Exception ex)
                    {
                        // If any failed, return the failure
                        error += $"Error saving generated file {outputPath}. {ex.Message}. {System.Environment.NewLine}";
                    }
                });

            // Return result
            return new EngineProcessResult { Success = string.IsNullOrEmpty(error), Path = path, Error = error };
        }

        /// <summary>
        /// Processes the tags in the list and edits the files contents as required
        /// </summary>
        /// <param name="path">The file that is being edit</param>
        /// <param name="fileContents">The full contents of the file</param>
        /// <param name="outputPaths">The output paths, can be changed by tags</param>
        /// <param name="tags">The tags to process</param>
        /// <param name="error">Set the error if there is a failure</param>
        private bool ProcessTags(string path, ref string fileContents, List<string> outputPaths, Match tags, out string error)
        {
            // No error to start with
            error = string.Empty;

            // Keep track of all includes to monitor for circular references
            var includes = new List<string>();

            // Loop through all matches
            while (tags.Success)
            {
                // NOTE: The first group is the full match
                //       The second group and onwards are the matches

                // Make sure we have enough groups
                if (tags.Groups.Count < 3)
                {
                    error = $"Malformed match {tags.Value}";
                    return false;
                }

                // Take the first match as the header for the type of tag
                var tagType = tags.Groups[1].Value.ToLower().Trim();

                // Now process each tag type
                switch (tagType)
                {
                    // OUTPUT NAME
                    case "output":

                        // Get output path
                        var outputPath = tags.Groups[2].Value;

                        // Process the output command
                        if (!ProcessCommandOutput(path, ref fileContents, outputPaths, outputPath, tags, out error))
                            // Return false if it fails
                            return false;

                        break;

                    // INCLUDE (Replace file)
                    case "include":

                        // Get include path
                        var includePath = tags.Groups[2].Value;

                        // Make sure we have not already included it in this run
                        if (includes.Contains(includePath.ToLower().Trim()))
                        {
                            error = $"Circular reference detected {includePath}";
                            return false;
                        }

                        // Process the include command
                        if (!ProcessCommandInclude(path, ref fileContents, outputPaths, includePath, tags, out error))
                            // Return false if it fails
                            return false;

                        // Add this to the list of already processed includes
                        includes.Add(includePath.ToLower().Trim());

                        break;

                    // UNKNOWN
                    default:
                        // Report error of unknown match
                        error = $"Unknown match {tags.Value}";
                        return false;
                }

                // Find the next command
                tags = Regex.Match(fileContents, mRegex2Group, RegexOptions.Singleline);
            }

            return true;
        }

        #region Private Helpers

        /// <summary>
        /// Saves the files content to disk
        /// </summary>
        /// <param name="fileContents">The full files contents</param>
        /// <param name="outputName">The absolute path to save the file to</param>
        private void SaveFile(string fileContents, string outputName)
        {
            File.WriteAllText(outputName, fileContents);

            // Log it
            Log($"Generated file {outputName}");
        }

        /// <summary>
        /// Changes the file extension to the default output file extension
        /// </summary>
        /// <param name="path">The full path to the file</param>
        /// <returns></returns>
        private string GetDefaultOutputPath(string path)
        {
            return Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + this.OutputExtension);
        }

        /// <summary>
        /// Checks if the file exists
        /// </summary>
        /// <param name="path">The full path to the file</param>
        /// <returns></returns>
        private bool FileExists(string path)
        {
            return File.Exists(path);
        }

        /// <summary>
        /// Reads the files full contents 
        /// </summary>
        /// <param name="path">The full path to the file</param>
        /// <returns></returns>
        private string ReadFile(string path)
        {
            return File.ReadAllText(path);
        }

        #endregion
    }
}
