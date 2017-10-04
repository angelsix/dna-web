using System;
using System.IO;
using System.Threading.Tasks;

namespace Dna.Web.Core
{
    /// <summary>
    /// Watches a given folder for a specific file type, and reports on file edits
    /// </summary>
    public class FolderWatcher : IDisposable
    {
        #region Private Members

        /// <summary>
        /// The native file system watcher
        /// </summary>
        private FileSystemWatcher mFileSystemWatcher;

        /// <summary>
        /// The Id of the newest file update event
        /// </summary>
        private Guid mLastUpdateId;

        #endregion

        #region Public Properties

        /// <summary>
        /// The absolute path to monitor
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// The time in milliseconds the file edits must stop occuring before the file changed event fires
        /// </summary>
        public int UpdateDelay { get; set; } = 100;

        /// <summary>
        /// The file type filter to monitoring only specific files
        /// To monitor all files use *.*
        /// To monitor for txt files do *.txt
        /// </summary>
        public string Filter { get; set; } = "*.*";

        #endregion

        #region Public Events

        /// <summary>
        /// Fired when a file has had its contents changed
        /// </summary>
        public event Action<string> FileChanged = (path) => { };

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts listening for file changes
        /// </summary>
        public void Start()
        {
            // Lock our class so only a single call to start can happen at once
            lock (this)
            {
                // Dispose any previous one
                Dispose();

                try
                {
                    // Create the native file system watcher
                    mFileSystemWatcher = new FileSystemWatcher
                    {
                        Path = Path,
                        // LastWrite for file content changes, FileName and DirectoryName to catch renames
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                        IncludeSubdirectories = true,
                        Filter = Filter
                    };
                }
                catch (Exception ex)
                {
                    // Log error nicely
                    // NOTE: Errors happen if there are issues accessing the specified path
                    CoreLogger.Log($"Failed to listen to folder {Path}.", ex.Message, LogType.Error);
                    return;
                }

                // Hook in the changed event
                mFileSystemWatcher.Changed += FileSystemWatcher_Changed;

                // Hook into renames separately
                mFileSystemWatcher.Renamed += FileSystemWatcher_Renamed;

                // Turn on raising events
                mFileSystemWatcher.EnableRaisingEvents = true;
            }
        }

        #endregion

        #region File Watcher Events

        /// <summary>
        /// Fired when a file has been renamed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileSystemWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            OnFileChanged(e.FullPath);
        }

        /// <summary>
        /// Fired when a file has been changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            OnFileChanged(e.FullPath);
        }

        /// <summary>
        /// Process a file that has been changed
        /// </summary>
        /// <param name="fullPath">The path to the file that was changed</param>
        private void OnFileChanged(string fullPath)
        {
            // Store file change path
            var path = fullPath;

            // Set the last update Id to this one
            var updateId = Guid.NewGuid();
            mLastUpdateId = updateId;

            // Wait the delay period
            Task.Delay(Math.Max(1, UpdateDelay)).ContinueWith((t) =>
            {
                // Check if the last update Id still matches, meaning no updates since that time
                if (updateId != mLastUpdateId)
                    return;

                // Settle time reached, so fire off the change event
                FileChanged(path);
            });
        }

        #endregion

        #region Dispose

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            // Clean up native file system watcher
            mFileSystemWatcher?.Dispose();
        }

        #endregion
    }
}
