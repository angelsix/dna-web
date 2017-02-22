using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dna.HtmlEngine.Core
{
    /// <summary>
    /// A base engine that any specific engine should implement
    /// </summary>
    public abstract class BaseEngine : IDisposable
    {
        #region Private Members

        /// <summary>
        /// A list of folder watchers that listen out for file changes of the given extensions
        /// </summary>
        private List<FolderWatcher> mWatchers;

        #endregion

        #region Public Properties

        /// <summary>
        /// The paths to monitor for files
        /// </summary>
        public string MonitorPath { get; set; }

        /// <summary>
        /// The desired default output extension for generated files if not overridden
        /// </summary>
        public string OutputExtension { get; set; } = ".dna";

        /// <summary>
        /// The time in milliseconds to wait for file edits to stop occurring before processing the file
        /// </summary>
        public int ProcessDelay { get; set; }

        /// <summary>
        /// The filename extensions to monitor for
        /// All files: *.*
        /// Specific types: *.dnaweb
        /// </summary>
        public List<string> EngineExtensions { get; set; }

        #endregion

        #region Public Events

        /// <summary>
        /// Called when processing of a file succeeded
        /// </summary>
        public event Action<EngineProcessResult> ProcessSuccessful = (result) => { };

        /// <summary>
        /// Called when processing of a file failed
        /// </summary>
        public event Action<EngineProcessResult> ProcessFailed = (result) => { };

        /// <summary>
        /// Called when the engine started
        /// </summary>
        public event Action Started = () => { };

        /// <summary>
        /// Called when the engine stopped
        /// </summary>
        public event Action Stopped = () => { };

        /// <summary>
        /// Called when the engine started watching for a specific file extension
        /// </summary>
        public event Action<string> StartedWatching = (extension) => { };

        /// <summary>
        /// Called when the engine stopped watching for a specific file extension
        /// </summary>
        public event Action<string> StoppedWatching = (extension) => { };

        /// <summary>
        /// Called when a log message is raised
        /// </summary>
        public event Action<LogMessage> LogMessage = (message) => { };

        #endregion

        #region Abstract Methods

        /// <summary>
        /// The processing action to perform when the given file has been edited
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        protected abstract Task<EngineProcessResult> ProcessFile(string path);

        #endregion

        #region Engine Methods

        /// <summary>
        /// Starts the engine ready to handle processing of the specified files
        /// </summary>
        public void Start()
        {
            // Lock this class so only one call can happen at a time
            lock (this)
            {
                // Dipose of any previous engine setup
                Dispose();

                // Make sure we have extensions
                if (this.EngineExtensions?.Count == 0)
                    throw new InvalidOperationException("No engine extensions specified");

                // Let listener know we started
                Started();

                // Log the message
                Log($"Engine started listening to '{this.MonitorPath}' with {this.ProcessDelay}ms delay...");
                    
                // Create a new list of watchers
                mWatchers = new List<FolderWatcher>();

                // We need to listen out for file changes per extension
                EngineExtensions.ForEach(extension => mWatchers.Add(new FolderWatcher
                {
                    Filter = extension, 
                    Path = MonitorPath,
                    UpdateDelay = ProcessDelay
                }));

                // Listen on all watchers
                mWatchers.ForEach(watcher =>
                {
                    // Listen for file changes
                    watcher.FileChanged += Watcher_FileChanged;

                    // Inform listener
                    StartedWatching(watcher.Filter);

                    // Log the message
                    Log($"Engine listening for file type {watcher.Filter}");

                    // Start watcher
                    watcher.Start();
                });
            }
        }

        /// <summary>
        /// Fired when a watcher has detected a file change
        /// </summary>
        /// <param name="path">The path of the file that has changed</param>
        private void Watcher_FileChanged(string path)
        {
            Task.Run(async () =>
            {
                try
                {
                    // Process the file
                    var result = await ProcessFile(path);

                    // Check if we have an unknown response
                    if (result == null)
                        throw new ArgumentNullException("Unknown error processing file. No result provided");

                    // If we succeeded, let the listeners know
                    if (result.Success)
                    {
                        // Inform listeners
                        ProcessSuccessful(result);

                        // Log the message
                        Log($"Successfully processed file {path}", type: LogType.Success);
                    }
                    // If we failed, let the listeners know
                    else
                    {
                        // Inform listeners
                        ProcessFailed(result);

                        // Log the message
                        Log($"Failed to processed file {path}", type: LogType.Error);
                    }
                }
                // Catch any unexpected failures
                catch (Exception ex)
                {
                    // Generate an unexpected error report
                    ProcessFailed(new EngineProcessResult
                    {
                        Path = path,
                        Error = ex.Message,
                        Success = false,
                    });

                    // Log the message
                    Log($"Unexpected fail to processed file {path}", message: ex.Message, type: LogType.Error);
                }
            });
        }

        #endregion

        #region Logger

        /// <summary>
        /// Logs a message and raises the <see cref="LogMessage"/> event
        /// </summary>
        /// <param name="title">The title of the log</param>
        /// <param name="message">The main message of the log</param>
        /// <param name="type">The type of the log message</param>
        public void Log(string title, string message = "", LogType type = LogType.Diagnostic)
        {
            LogMessage(new LogMessage
            {
                Title = title,
                Message = message,
                Time = DateTime.UtcNow,
                Type = type
            });
        }

        #endregion

        #region Dispose

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            // Clean up all file watchers
            mWatchers?.ForEach(watcher =>
            {
                // Get extension
                var extension = watcher.Filter;

                // Dispose of watcher
                watcher.Dispose();

                // Inform listener
                StoppedWatching(extension);

                // Log the message
                Log($"Engine stopped listening for file type {watcher.Filter}");
            });

            if (mWatchers != null)
            {
                // Let listener know we stopped
                Stopped();

                // Log the message
                Log($"Engine stopped");
            }

            mWatchers = null;
        }

        #endregion
    }
}
