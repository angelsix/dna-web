using SharpScss;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Dna.Web.Core
{
    /// <summary>
    /// An engine that processes Sass files into CSS
    /// </summary>
    public partial class DnaSassEngine : DebugEngine
    {
        #region Private Members

        /// <summary>
        /// The extension of a Sass file 
        /// </summary>
        private const string ScssExtension = ".scss";

        /// <summary>
        /// Captures a line starting with @import and ending with ;
        /// </summary>
        private string mSassImportLineRegex = @"@import\s*(.*?(?=\;))\;\s*$";

        /// <summary>
        /// Breaks up the import values into each item
        /// For example "a", "b" would be 2 groups, a and b
        /// A single "a" would be a
        /// </summary>
        private string mSassImportSplitRegex = "['\"]([^ '\"]*)['\"]";

        #endregion

        #region Public Properties

        public override string EngineName => "Sass";

        #endregion

        #region Constructor

        /// <summary>
        /// Default constructor
        /// </summary>
        public DnaSassEngine()
        {
            // Set input extensions
            EngineExtensions = new List<string> { ScssExtension };

            // Set output extension
            OutputExtension = ".css";
        }

        #endregion

        #region Override Methods

        protected override Task PreProcessFile(FileProcessingData data)
        {
            return Task.Run(() =>
            {
                // We don't need any processing for Sass files
                // It is all done via the Sass engine
                WillProcessDataTags = false;
                WillProcessMainTags = false;
                WillProcessOutputTags = false;

                // Set this file to partial if it starts with _
                // As per the Sass rule 
                data.IsPartial = Path.GetFileName(data.FullPath).StartsWith("_");

                // Ignore .sass-cache folder
                if (data.FullPath.Replace("\\", "/").Contains("/.sass-cache/"))
                    data.SkipMessage = "Ignoring .sass-cache folder";
            });
        }

        /// <summary>
        /// Specifies Sass output paths based on Dna Config settings, if specified
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        protected override Task PostProcessOutputPaths(FileProcessingData data)
        {
            return Task.Run(() =>
            {
                // For now just output in the same directory as Sass file
                data.OutputPaths.Add(new FileOutputData
                {
                    FullPath = GetDefaultOutputPath(data.FullPath),
                    FileContents = data.UnprocessedFileContents
                });
            });
        }

        /// <summary>
        /// Tweak the output path based on the settings
        /// </summary>
        /// <param name="data"></param>
        /// <param name="output"></param>
        /// <returns></returns>
        protected override Task PreGenerateFile(FileProcessingData data, FileOutputData output)
        {
            return Task.Run(() =>
            {
                var sassPath = data.LocalConfiguration.SassOutputPath;

                if (!string.IsNullOrEmpty(sassPath))
                {
                    // Resolve relative path
                    if (!Path.IsPathRooted(sassPath))
                        sassPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(data.FullPath), sassPath));

                    // Set the output path to absolute path
                    output.FullPath = Path.Combine(sassPath, Path.GetFileNameWithoutExtension(data.FullPath) + OutputExtension);
                }
            });
        }

        /// <summary>
        /// Generates the css contents from the Sass input, storing it in CompiledContents ready for saving
        /// </summary>
        /// <param name="data"></param>
        /// <param name="output"></param>
        protected override void GenerateOutput(FileProcessingData data, FileOutputData output)
        {
            try
            {
                // Convert file to css
                var result = Scss.ConvertToCss(output.FileContents, new ScssOptions()
                {
                    // Note: It will not generate the file, 
                    // only used for exception reporting
                    // includes and source maps
                    InputFile = data.FullPath,
                    OutputFile = output.FullPath,

                    // Include source map output in result
                    GenerateSourceMap = true
                });

                output.CompiledContents = result.Css;

                // TODO: Could output source map based on configuration
            }
            catch (Exception ex)
            {
                data.Error = $"Unexpected error generating Scss output. {ex.Message}";
            }
        }

        /// <summary>
        /// Finds the first occurrence Sass @import statement in the file contents
        /// </summary>
        /// <param name="fileContents">The path of the file to look in</param>
        /// <param name="fileContents">The contents of the file</param>
        /// <param name="match">The <see cref="Match"/> that found the include statement</param>
        /// <param name="includePaths">The include path(s) found</param>
        /// <returns></returns>
        protected override bool GetIncludeTag(string filePath, string fileContents, ref Match match, out List<string> includePaths)
        {
            // Blank list to start with
            includePaths = new List<string>();

            // Find any of the following:
            // 
            // @import "x";
            // @import "_x";
            // @import "x.scss";
            // @import "_x.scss";
            // @import "../x.scss";
            // @import "x", "y", "z";
            //
            // Also match all of the above replacing " with '
            //
            // Partial _ in filename
            // ========================
            // If import excludes _ at the start of the name, add it and then
            // if no file is found with an _ then resort to looking for one without the _
            //
            // If both are found, only the file with the _ is used
            //

            // Try and find match of @import ... ;
            match = Regex.Match(fileContents, mSassImportLineRegex, RegexOptions.Multiline);

            // Make sure we have enough groups
            if (match.Groups.Count < 2)
                return false;

            // Get the area between @import and ; for example
            // @import "a";          "a"
            // @import "a", "b";     "a", "b"
            var innerImport = match.Groups[1].Value;

            // Now get the values between the comma's and quotes
            var innerMatches = Regex.Matches(innerImport, mSassImportSplitRegex, RegexOptions.Singleline);

            // For each match...
            foreach (Match innerMatch in innerMatches)
            {
                // Make sure we have enough groups
                if (innerMatch.Groups.Count < 2)
                    continue;

                // Get include path value
                var includePath = innerMatch.Groups[1].Value;

                // Add extension if not added
                if (!includePath.EndsWith(ScssExtension))
                    includePath += ScssExtension;

                // Resolve any relative aspects of the path
                if (!Path.IsPathRooted(includePath))
                    includePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(filePath), includePath));

                // Sass rules (from testing other Sass compilers) show that if an include doesn't start with an underscore
                // but both the underscore file and file without an underscore exist (such as a.scss and _a.scss)
                // then the _ file will be the one that get's included
                //
                // So check for that
                if (!Path.GetFileName(includePath).StartsWith("_"))
                {
                    var underscoredPath = Path.Combine(Path.GetDirectoryName(includePath), $"_{Path.GetFileName(includePath)}");

                    if (File.Exists(underscoredPath))
                        includePath = underscoredPath;
                }

                // Add this path
                includePaths.Add(includePath);
            }

            // Return successful
            return true;
        }

        #endregion
    }
}
