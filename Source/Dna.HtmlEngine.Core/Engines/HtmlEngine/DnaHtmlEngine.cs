using System;
using System.Collections.Generic;
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

        #endregion

        #region Constructor

        /// <summary>
        /// Default constructor
        /// </summary>
        public DnaHtmlEngine()
        {
            // Set input extensions
            EngineExtensions = new List<string> { ".dnaweb", "._dnaweb" };

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
            if (!FileManager.FileExists(path))
                return new EngineProcessResult { Success = false, Path = path, Error = "File no longer exists" };

            // Read all the file into memory (it's ok we will never have large files they are text web files)
            var fileContents = FileManager.ReadAllText(path);

            // Flag indicating if this is a partial file
            // NOTE: Partial files don't generate output themselves
            var isPartial = false;

            // Process those tags
            if (!ProcessBaseTags(path, ref fileContents, ref isPartial, outputPaths, out string error))
                // If any failed, return the failure
                return new EngineProcessResult { Success = false, Path = path, Error = error };

            // All ok, generate HTML files if not a partial file

            if (!isPartial)
            {
                // If we have no outputs specified, add the default
                if (outputPaths.Count == 0)
                    // Get default output name
                    outputPaths.Add(GetDefaultOutputPath(path));

                outputPaths.ForEach(outputPath =>
                {
                    try
                    {
                        FileManager.SaveFile(fileContents, outputPath);

                        // Log it
                        Log($"Generated file {outputPath}");
                    }
                    catch (Exception ex)
                    {
                        // If any failed, return the failure
                        error += $"Error saving generated file {outputPath}. {ex.Message}. {System.Environment.NewLine}";
                    }
                });
            }
            else
            {
                // If it is a partial file, searc the root monitor folder for all files with the extensions
                // and search within those for a tag that includes this partial fie
                // 
                // Then fire off a process event for each of them
                Log($"Partial file edit. Updating referenced files to {path}...");

                // Find all files references this path
                var referencedFiles = FindReferencedFiles(path);

                // Process any referenced files
                foreach (var reference in referencedFiles)
                    await ProcessFileChanged(reference);
            }

            // Return result
            return new EngineProcessResult { Success = string.IsNullOrEmpty(error), Path = path, Error = error };
        }
    }
}
