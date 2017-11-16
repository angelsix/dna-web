using ICSharpCode.SharpZipLib.Zip;
using System;
using System.IO;

namespace Dna.Web.Core
{
    /// <summary>
    /// Helper methods for working with zip files
    /// </summary>
    public static class ZipHelpers
    {
        /// <summary>
        /// Extracts a zip file to a specific destination folder
        /// </summary>
        /// <param name="zipPath">The path to the zip file</param>
        /// <param name="destinationPath">The path to extract to</param>
        /// <param name="cleanupFolderOnFail">If the extract fails, clean up (delete) the destination folder</param>
        /// <returns></returns>
        public static bool Unzip(string zipPath, string destinationPath, bool cleanupFolderOnFail = true)
        {
            try
            {
                // Ensure destination folder exists
                if (!Directory.Exists(destinationPath))
                    Directory.CreateDirectory(destinationPath);

                // Open the zip stream
                using (var s = new ZipInputStream(File.OpenRead(zipPath)))
                {
                    // Create zip entry variable
                    ZipEntry zipEntry;

                    // For each zip entry in the stream...
                    while ((zipEntry = s.GetNextEntry()) != null)
                    {
                        // Get absolute path of file/folder
                        var zipEntryPath = Path.Combine(destinationPath, zipEntry.Name).Replace("/", "\\");

                        // If this item is a directory
                        if (!zipEntry.IsFile)
                        {
                            // If directory doesn't exist...
                            if (!Directory.Exists(zipEntryPath))
                                // Create it
                                Directory.CreateDirectory(zipEntryPath);
                        }
                        // If it is a file...
                        else
                        {
                            // Ensure folder
                            var filesFolder = Path.GetDirectoryName(zipEntryPath);
                            if (!Directory.Exists(filesFolder))
                                Directory.CreateDirectory(filesFolder);

                            // Create file stream
                            using (var streamWriter = File.Create(zipEntryPath))
                                // Write to the destination in 2kb chunks
                                s.CopyTo(streamWriter, 2048);
                        }
                    }
                }

                // Success
                return true;
            }
            catch (Exception ex)
            {
                // Log it
                CoreLogger.Log($"Zip: Failed to unzip '{zipPath}' to '{destinationPath}'. {ex.Message}", type: LogType.Warning);

                // Try and clean up folder
                if (cleanupFolderOnFail)
                {
                    try
                    {
                        // If it exists
                        if (Directory.Exists(destinationPath))
                            // Delete it
                            Directory.Delete(destinationPath, true);
                    }
                    catch (Exception innerEx)
                    {
                        // Log it
                        CoreLogger.Log($"Zip: Failed to cleanup failed extraction folder '{destinationPath}'. {innerEx.Message}", type: LogType.Warning);
                    }
                }

                // Return fail
                return false;
            }
        }

        /// <summary>
        /// Zips up the contents of the specified folder
        /// </summary>
        /// <param name="sourceFolder">The folder with the contents to zip</param>
        /// <param name="destinationFile">The output zip file</param>
        /// <returns></returns>
        public static bool Zip(string sourceFolder, string destinationFile)
        {
            try
            {
                // Ensure source folder exists
                if (!Directory.Exists(sourceFolder))
                {
                    // Log it
                    CoreLogger.Log($"Zip: Source folder does not exist '{sourceFolder}'", type: LogType.Error);

                    // Done
                    return false;
                }

                // Ensure destination file is not already in existence
                if (File.Exists(destinationFile))
                {
                    // Log it
                    CoreLogger.Log($"Zip: Destination zip file already exists '{destinationFile}'", type: LogType.Error);

                    // Done
                    return false;
                }

                // Get all files/folders in the folder
                var allFolders = Directory.GetDirectories(sourceFolder, "*", SearchOption.AllDirectories);
                var allFiles = Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories);

                // Create new zip file stream
                using (var zipStream = new ZipOutputStream(File.Create(destinationFile)))
                {
                    // This will close the underlying File stream
                    zipStream.IsStreamOwner = true;

                    // Highest compression
                    zipStream.SetLevel(9);

                    // Loop each folder
                    foreach (var folderPath in allFolders)
                    {
                        // Remove source from start of path
                        var folderRelativePath = folderPath.Substring(sourceFolder.Length);

                        // Make sure folder ends with \
                        if (!folderRelativePath.EndsWith("\\"))
                            folderRelativePath += "\\";

                        // Add this folder to stream
                        zipStream.PutNextEntry(new ZipEntry(folderRelativePath));

                        // Close entry
                        zipStream.CloseEntry();
                    }

                    // Loop each file
                    foreach (var filePath in allFiles)
                    {
                        // Remove source from start of path
                        var fileRelativePath = filePath.Substring(sourceFolder.Length);

                        // Create file entry
                        var newEntry = new ZipEntry(fileRelativePath)
                        {
                            // Set length of entry
                            Size = new FileInfo(filePath).Length
                        };

                        // Add this folder to stream
                        zipStream.PutNextEntry(newEntry);

                        // Write file contents
                        using (var inputStream = File.OpenRead(filePath))
                            // In 2kb chunks
                            inputStream.CopyTo(zipStream, 2048);

                        // Close entry
                        zipStream.CloseEntry();
                    }
                }

                // Success
                return true;
            }
            catch (Exception ex)
            {
                // Log it
                CoreLogger.Log($"Zip: Failed to zip '{sourceFolder}' to '{destinationFile}'. {ex.Message}", type: LogType.Error);

                // Clean failed destination zip
                try
                {
                    // If it exists
                    if (File.Exists(destinationFile))
                        // Delete it
                        File.Delete(destinationFile);
                }
                catch (Exception innerEx)
                {
                    // Log it
                    CoreLogger.Log($"Zip: Failed to cleanup failed zip file '{destinationFile}'. {innerEx.Message}", type: LogType.Warning);
                }

                // Return fail
                return false;
            }
        }
    }
}
