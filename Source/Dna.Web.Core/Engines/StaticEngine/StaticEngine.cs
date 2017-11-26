using SharpScss;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Dna.Web.Core
{
    /// <summary>
    /// An engine that processes all files just copying the source files
    /// to the destination without any processing.
    /// 
    /// It also monitors for renames/moves/deletes and mimics them in
    /// the source folder
    /// </summary>
    public partial class StaticEngine : DebugEngine
    {
        #region Private Members

        #endregion

        #region Public Properties

        public override string EngineName => "Static";

        /// <summary>
        /// The details about the static folder
        /// </summary>
        public DnaConfigurationStaticFolder StaticFolderDetails { get; set; }

        #endregion

        #region Constructor

        /// <summary>
        /// Default constructor
        /// </summary>
        public StaticEngine()
        {
            // Set input extensions
            EngineExtensions = new List<string> { "*.*" };

            // Set output extension (use original)
            OutputExtension = null;

            // Don't regenerate on folder rename
            // We handle it manually
            RegenerateOnFolderRename = false;

            // Don't treat file renames as file changes
            TreatFileRenameAsChange = false;
        }

        #endregion

        #region Override Methods

        protected override Task PreProcessFile(FileProcessingData data)
        {
            return SafeTask.Run(() =>
            {
                // We don't need any processing for static files
                // It is all done via the Static engine
                WillProcessDataTags = false;
                WillProcessMainTags = false;
                WillProcessOutputTags = false;
                WillProcessVariables = false;
                WillProcessLiveVariables = false;

                // Don't read the file into memory
                WillReadFileIntoMemory = false;
            });
        }

        /// <summary>
        /// Specifies output paths based on Dna configuration settings, if specified
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        protected override Task PostProcessOutputPaths(FileProcessingData data)
        {
            return SafeTask.Run(() =>
            {
                // Output path is the destination folder
                var relativePath = data.FullPath.Substring(StaticFolderDetails.SourceFolder.Length + 1);
                var outputPath = Path.GetFullPath(Path.Combine(StaticFolderDetails.DestinationFolder, relativePath));

                data.OutputPaths.Add(new FileOutputData
                {
                    FullPath = outputPath,
                    FileContents = data.UnprocessedFileContents
                });
            });
        }

        /// <summary>
        /// Generate no output as we will do a plain file copy action on save instead
        /// </summary>
        /// <param name="data"></param>
        /// <param name="output"></param>
        protected override void GenerateOutput(FileProcessingData data, FileOutputData output)
        {
            // NOTE: Do nothing on generate output as we will override the save function
        }

        /// <summary>
        /// Save the files contents to the destination location
        /// </summary>
        /// <param name="processingData"></param>
        /// <param name="outputPath"></param>
        /// <returns></returns>
        public override Task SaveFileContents(FileProcessingData processingData, FileOutputData outputPath)
        {
            return SafeTask.Run(() =>
            {
                // Do normal file copy when saving static file
                FileManager.CopyFile(processingData.FullPath, outputPath.FullPath);
            });
        }

        /// <summary>
        /// Delete the same file at the destination
        /// </summary>
        /// <param name="path">The source file that has been deleted</param>
        /// <returns></returns>
        protected override Task ProcessFileDeletedAsync(string path)
        {
            return SafeTask.Run(() =>
            {
                var relativePath = path.Substring(StaticFolderDetails.SourceFolder.Length + 1);
                var outputPath = Path.GetFullPath(Path.Combine(StaticFolderDetails.DestinationFolder, relativePath));

                // Sanity check we are still inside output path
                if (!outputPath.Replace("/", "\\").StartsWith(StaticFolderDetails.DestinationFolder + "\\"))
                {
                    CoreLogger.Log($"Ignoring file delete as it appears outside of the destination folder. Source: '{path}' Destination: '{outputPath}'");
                    return;
                }

                // Log it
                CoreLogger.Log($"Deleting static file {outputPath}", type: LogType.Warning);

                // Delete file
                FileManager.DeleteFile(outputPath);
            });
        }

        /// <summary>
        /// Delete the same folder at the destination
        /// </summary>
        /// <param name="path">The source folder that has been deleted</param>
        /// <returns></returns>
        protected override Task ProcessFolderDeletedAsync(string path)
        {
            return SafeTask.Run(() =>
            {
                var relativePath = path.Substring(StaticFolderDetails.SourceFolder.Length + 1);
                var outputPath = Path.GetFullPath(Path.Combine(StaticFolderDetails.DestinationFolder, relativePath));

                // Sanity check we are still inside output path
                if (!outputPath.Replace("/", "\\").StartsWith(StaticFolderDetails.DestinationFolder + "\\"))
                {
                    CoreLogger.Log($"Ignoring folder delete as it appears outside of the destination folder. Source: '{path}' Destination: '{outputPath}'");
                    return;
                }

                // Log it
                CoreLogger.Log($"Deleting static folder {outputPath}", type: LogType.Warning);

                // Delete folder
                FileManager.DeleteFolder(outputPath);
            });
        }

        /// <summary>
        /// Mimic the file rename at the destination
        /// </summary>
        /// <param name="details"></param>
        /// <returns></returns>
        protected override Task ProcessFileRenamedAsync((string from, string to) details)
        {
            return SafeTask.Run(() =>
            {
                // Get destination from
                var relativeFromPath = details.from.Substring(StaticFolderDetails.SourceFolder.Length + 1);
                var outputFromPath = Path.GetFullPath(Path.Combine(StaticFolderDetails.DestinationFolder, relativeFromPath));

                // Get destination to
                var relativeToPath = details.to.Substring(StaticFolderDetails.SourceFolder.Length + 1);
                var outputToPath = Path.GetFullPath(Path.Combine(StaticFolderDetails.DestinationFolder, relativeToPath));

                // Sanity check we are still inside output path
                if (!outputFromPath.Replace("/", "\\").StartsWith(StaticFolderDetails.DestinationFolder + "\\"))
                {
                    CoreLogger.Log($"Ignoring file rename as 'from' path appears outside of the destination folder. Source: '{details.from}' Destination: '{outputFromPath}'");
                    return;
                }

                if (!outputToPath.Replace("/", "\\").StartsWith(StaticFolderDetails.DestinationFolder + "\\"))
                {
                    CoreLogger.Log($"Ignoring file rename as 'to' path appears outside of the destination folder. Source: '{details.to}' Destination: '{outputToPath}'");
                    return;
                }

                // Presume if from path exists in output...
                if (FileManager.FileExists(outputFromPath)  &&
                    // And the to file does not
                    !FileManager.FileExists(outputToPath) &&
                    // And it is the same size
                    new FileInfo(details.to).Length == new FileInfo(outputFromPath).Length)
                {
                    // Log it
                    CoreLogger.Log($"Renaming static file from {outputFromPath} to {outputToPath}", type: LogType.Success);

                    // Rename the file
                    FileManager.RenameFile(outputFromPath, outputToPath);
                }
                // Otherwise, clean copy a new file
                else
                {
                    // Log it
                    CoreLogger.Log($"Clean copy of renaming static file from {outputFromPath} to {outputToPath}", type: LogType.Success);

                    // Delete destination files if they already exists
                    if (FileManager.FileExists(outputFromPath))
                        FileManager.DeleteFile(outputFromPath);
                    if (FileManager.FileExists(outputToPath))
                        FileManager.DeleteFile(outputToPath);

                    // Copy fresh file from source to destination
                    FileManager.CopyFile(details.to, outputToPath);
                }
            });
        }

        /// <summary>
        /// Mimic the folder rename at the destination
        /// </summary>
        /// <param name="details"></param>
        /// <returns></returns>
        protected override Task ProcessFolderRenamedAsync((string from, string to) details)
        {
            return SafeTask.Run(() =>
            {
                // Get destination from
                var relativeFromPath = details.from.Substring(StaticFolderDetails.SourceFolder.Length + 1);
                var outputFromPath = Path.GetFullPath(Path.Combine(StaticFolderDetails.DestinationFolder, relativeFromPath));

                // Get destination to
                var relativeToPath = details.to.Substring(StaticFolderDetails.SourceFolder.Length + 1);
                var outputToPath = Path.GetFullPath(Path.Combine(StaticFolderDetails.DestinationFolder, relativeToPath));

                // Sanity check we are still inside output path
                if (!outputFromPath.Replace("/", "\\").StartsWith(StaticFolderDetails.DestinationFolder + "\\"))
                {
                    CoreLogger.Log($"Ignoring folder rename as 'from' path appears outside of the destination folder. Source: '{details.from}' Destination: '{outputFromPath}'");
                    return;
                }

                if (!outputToPath.Replace("/", "\\").StartsWith(StaticFolderDetails.DestinationFolder + "\\"))
                {
                    CoreLogger.Log($"Ignoring folder rename as 'to' path appears outside of the destination folder. Source: '{details.to}' Destination: '{outputToPath}'");
                    return;
                }

                // Presume if from path exists in output...
                if (FileManager.FolderExists(outputFromPath) &&
                    // And the to folder does not
                    !FileManager.FolderExists(outputToPath))
                {
                    // Log it
                    CoreLogger.Log($"Renaming static folder from {outputFromPath} to {outputToPath}", type: LogType.Success);

                    // Rename folder
                    FileManager.RenameFolder(outputFromPath, outputToPath);
                }
                // Otherwise, clean copy a new file
                else
                {
                    // Log it
                    CoreLogger.Log($"Clean copy of a renaming static folder from {outputFromPath} to {outputToPath}", type: LogType.Success);

                    // Delete destination files if they already exists
                    if (FileManager.FolderExists(outputFromPath))
                        FileManager.DeleteFolder(outputFromPath);
                    if (FileManager.FolderExists(outputToPath))
                        FileManager.DeleteFolder(outputToPath);

                    // Copy fresh file from source to destination
                    FileManager.CopyFolder(details.to, outputToPath);
                }
            });
        }
        #endregion
    }
}
