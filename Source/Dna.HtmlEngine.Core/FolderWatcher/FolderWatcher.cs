using System;
using System.IO;
using System.Threading.Tasks;

namespace Dna.HtmlEngine.Core
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

                // Create the native file system watcher
                mFileSystemWatcher = new FileSystemWatcher
                {
                    Path = Path,
                    NotifyFilter = NotifyFilters.LastWrite,
                    IncludeSubdirectories = true,
                    Filter = Filter
                };

                // Hook in the changed event
                mFileSystemWatcher.Changed += FileSystemWatcher_Changed;

                // Turn on raising events
                mFileSystemWatcher.EnableRaisingEvents = true;
            }
        }

        #endregion

        #region File Watcher Events

        /// <summary>
        /// Fired when a file has been changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            // Store file change path
            var path = e.FullPath;

            // Set the last update Id to this one
            var updateId = Guid.NewGuid();
            mLastUpdateId = updateId;

            // Wait the delay period
            Task.Delay(Math.Max(1, this.UpdateDelay)).ContinueWith((t) =>
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
