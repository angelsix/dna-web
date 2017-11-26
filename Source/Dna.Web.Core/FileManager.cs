using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Dna.Web.Core
{
    /// <summary>
    /// Handles all the File system IO methods
    /// </summary>
    public static class FileManager
    {
        /// <summary>
        /// Saves the files content to disk
        /// </summary>
        /// <param name="fileContents">The full files contents</param>
        /// <param name="outputName">The absolute path to save the file to</param>
        public static void SaveFile(string fileContents, string outputName)
        {
            var dir = Path.GetDirectoryName(outputName);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(outputName, fileContents);
        }

        /// <summary>
        /// Saves the files content to disk
        /// </summary>
        /// <param name="fileContents">The full files contents</param>
        /// <param name="outputName">The absolute path to save the file to</param>
        public static void SaveFile(byte[] fileContents, string outputName)
        {
            var dir = Path.GetDirectoryName(outputName);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllBytes(outputName, fileContents);
        }

        /// <summary>
        /// Checks if the file exists
        /// </summary>
        /// <param name="path">The full path to the file</param>
        /// <returns></returns>
        public static bool FileExists(string path)
        {
            return File.Exists(path);
        }

        /// <summary>
        /// Checks if the folder exists
        /// </summary>
        /// <param name="path">The full path to the folder</param>
        /// <returns></returns>
        public static bool FolderExists(string path)
        {
            return Directory.Exists(path);
        }

        /// <summary>
        /// Reads the files full contents 
        /// </summary>
        /// <param name="path">The full path to the file</param>
        /// <returns></returns>
        public static async Task<string> ReadAllTextAsync(string path)
        {
            var i = 3;

            // Try to read the file 3 times if it's failed due to a lock
            while (i-- > 0)
            {
                try
                {
                    // Try and read the file
                    return File.ReadAllText(path);
                }
                catch (IOException)
                {
                    // If this is the last attempt, just throw
                    if (i == 0)
                        throw;

                    await Task.Delay(300);
                }
            }

            // NOTE: We can never get here, but VS isn't smart enough to know that. So just throw an IOException if we got here
            throw new IOException();
        }

        /// <summary>
        /// Copies a file to a destination, overriding any existing file
        /// </summary>
        /// <param name="sourcePath">The input path</param>
        /// <param name="destinationPath">The output path</param>
        public static void CopyFile(string sourcePath, string destinationPath)
        {
            // Ensure folder exists
            var directory = Path.GetDirectoryName(destinationPath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            // Do file copy
            File.Copy(sourcePath, destinationPath, true);
        }

        /// <summary>
        /// Copies a folder to a destination, overriding any existing folder
        /// </summary>
        /// <param name="sourcePath">The input path</param>
        /// <param name="destinationPath">The output path</param>
        public static void CopyFolder(string sourcePath, string destinationPath)
        {
            // Copy folder
            CopyFolderRecursive(new DirectoryInfo(sourcePath), new DirectoryInfo(destinationPath));
        }

        /// <summary>
        /// Recursively copy a folder to another destination
        /// </summary>
        /// <param name="source">The source folder</param>
        /// <param name="target">The destination folder</param>
        private static void CopyFolderRecursive(DirectoryInfo source, DirectoryInfo target)
        {
            // Make sure destination folder exists
            Directory.CreateDirectory(target.FullName);

            // Get each file in the folder...
            foreach (var file in source.GetFiles())
                // And copy it to the destination
                file.CopyTo(Path.Combine(target.FullName, file.Name), true);

            // Get each sub-folder...
            foreach (var subFolder in source.GetDirectories())
                // Recursively copy
                CopyFolderRecursive(subFolder, target.CreateSubdirectory(subFolder.Name));
        }

        /// <summary>
        /// Deletes the specified file
        /// </summary>
        /// <param name="path">The full path to the file to delete</param>
        public static bool DeleteFile(string path)
        {
            try
            {
                // Try and delete file
                File.Delete(path);

                return true;
            }
            catch (Exception ex)
            {
                // Log it
                CoreLogger.Log($"Failed to delete file: {path}. {ex.Message}", type: LogType.Error);

                return false;
            }
        }

        /// <summary>
        /// Deletes the specified folder
        /// </summary>
        /// <param name="path">The full path to the folder to delete</param>
        public static bool DeleteFolder(string path)
        {
            try
            {
                // Try and delete file
                Directory.Delete(path, true);

                return true;
            }
            catch (Exception ex)
            {
                // Log it
                CoreLogger.Log($"Failed to delete folder: {path}. {ex.Message}", type: LogType.Error);

                return false;
            }
        }

        /// <summary>
        /// Renames the specified file
        /// </summary>
        /// <param name="path">The full path to the file to rename</param>
        /// <param name="newPath">The new path to rename to</param>
        public static bool RenameFile(string path, string newPath)
        {
            try
            {
                // Try and delete file
                File.Move(path, newPath);

                return true;
            }
            catch (Exception ex)
            {
                // Log it
                CoreLogger.Log($"Failed to rename file: {path}. {ex.Message}", type: LogType.Error);

                return false;
            }
        }

        /// <summary>
        /// Renames the specified folder
        /// </summary>
        /// <param name="path">The full path to the folder to rename</param>
        /// <param name="newPath">The new path to rename to</param>
        public static bool RenameFolder(string path, string newPath)
        {
            try
            {
                // Try and delete file
                Directory.Move(path, newPath);

                return true;
            }
            catch (Exception ex)
            {
                // Log it
                CoreLogger.Log($"Failed to rename folder: {path}. {ex.Message}", type: LogType.Error);

                return false;
            }
        }
    }
}
