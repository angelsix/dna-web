using System;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.Text;

namespace Dna.Web.Core
{
    /// <summary>
    /// A basic HTTP server that will listen and serve files based on URLs
    /// </summary>
    public class LiveServer : IDisposable
    {
        #region Protected Members

        /// <summary>
        /// The URL query string that if appended to a request should then just hang until there are file changes
        /// </summary>
        protected const string SignalNewContentQuery = "?newcontent";

        /// <summary>
        /// The Http Listener for this server
        /// </summary>
        protected HttpListener mListener;

        /// <summary>
        /// The folder watcher that listens out for file changes
        /// </summary>
        protected FolderWatcher mFolderWatcher;

        /// <summary>
        /// Lock for locking the Listen call so it doesn't run twice
        /// </summary>
        protected object mListenLock = new object();

        /// <summary>
        /// Lock for locking the Stop call so it doesn't run twice
        /// </summary>
        protected string mStopLock = "HttpListenerStopLock";

        /// <summary>
        /// The reset event that gets set when contents observed in the <see cref="ServingDirectory"/> change
        /// </summary>
        protected AutoResetEvent mContentChangedResetEvent = new AutoResetEvent(false);

        /// <summary>
        /// The special URL that requests the auto-reload script
        /// </summary>
        protected const string AutoReloadRequestQueryUrl = "?autoreload";

        /// <summary>
        /// The Javascript to return when requesting the auto-reload script
        /// </summary>
        protected const string AutoReloadJavascript = @"
// Checks for any changes to this file and reloads
function checkForChanges()
{
    // New XHR call
    var xhttp = new XMLHttpRequest();

    // On timeout, repeat
    xhttp.onTimeout = function ()
    {
        // Call again
        checkForChanges();
    }

    // On state change...
    xhttp.onreadystatechange = function() 
    {
        // If we are successful...
        if (this.readyState == 4)
        {
            if (this.status == 200) 
            {
                // Reload page
                // NOTE: Contents of response is this.responseText
                window.location.reload(true);
            }
        }
    };

    // Open connection
    xhttp.open(""GET"", window.location + ""?newcontent"", true);

    // Make sure the response is not cached
    xhttp.setRequestHeader('Cache-Control', 'no-cache');

    // Send
    xhttp.send(); 
}

// Check for changes to the file
window.onload = checkForChanges;";


        #endregion

        #region Public Properties

        /// <summary>
        /// The port to listen on. If left as 0, an available port will be chosen
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Flag indicating if we are disposing
        /// </summary>
        public bool Disposing { get; private set; }

        /// <summary>
        /// Flag indicating if we are stopping listening
        /// </summary>
        public bool Stopping { get; private set; }

        /// <summary>
        /// Flag indicating if we are listening
        /// </summary>
        public bool Listening { get; private set; }

        /// <summary>
        /// The directory that files should be served from
        /// </summary>
        public string ServingDirectory { get; set; }

        #endregion

        #region Constructor

        /// <summary>
        /// Default constructor
        /// </summary>
        public LiveServer() { }

        #endregion

        #region Public Static Helpers

        /// <summary>
        /// Finds the next available TCP port
        /// </summary>
        /// <returns></returns>
        public static int NextAvailablePort()
        {
            // Create a listener, and have it find an empty port
            var tcpListener = new TcpListener(IPAddress.Loopback, 0);

            // Start it
            tcpListener.Start();

            // Get the port it is listening on
            var port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;

            // Stop it
            tcpListener.Stop();

            return port;
        }

        #endregion

        #region Listen / Stop Methods

        /// <summary>
        /// Stop listening
        /// </summary>
        public async Task StopAsync()
        {
            // Log it
            CoreLogger.Log($"LiveServer '{ServingDirectory}' stopping...", type: LogType.Warning);

            // Lock call
            await AsyncAwaitor.AwaitAsync(mStopLock, async () =>
            {
                try
                {
                    // Stop listener
                    mListener.Stop();
                }
                catch (Exception ex)
                {
                    // Log it
                    Debugger.Break();
                    CoreLogger.Log($"Failed to stop LiveServer listener. {ex.Message}", type: LogType.Warning);
                }

                // Set stopping flag to true
                Stopping = true;

                // Dispose folder watcher
                mFolderWatcher?.Dispose();
                mFolderWatcher = null;

                // Set it so any blocked calls pass on
                mContentChangedResetEvent.Set();

                // Flag that we are no longer listening
                while (Listening)
                    await Task.Delay(100);

                // Log it
                CoreLogger.Log($"LiveServer {ServingDirectory} stopped", type: LogType.Attention);
            });
        }

        /// <summary>
        /// Starts listening on the specified <see cref="Port"/>
        /// </summary>
        /// <returns>Returns the URL that is being listened on</returns>
        public string Listen()
        {
            lock (mListenLock)
            {
                // If we are already listening...
                if (Listening)
                    // Ignore
                    return null;

                #region Get Port

                try
                {
                    // Get port if one is not specified
                    if (Port <= 0)
                    {
                        // Log it
                        CoreLogger.Log("LiveServer getting available port...");

                        // Get next available port
                        Port = NextAvailablePort();
                    }
                }
                catch (Exception ex)
                {
                    // Log it
                    Debugger.Break();
                    CoreLogger.Log($"LiveServer failed to find an available port. {ex.Message}", type: LogType.Error);

                    // Go no further
                    return null;
                }

                // Log port to be used
                CoreLogger.Log($"LiveServer will listen on port {Port}");

                #endregion

                // Expected listen URL
                var listenUrl = $"http://localhost:{Port}/";

                #region Listen

                try
                {
                    // Create new Http Listener
                    mListener = new HttpListener();

                    // Set port number
                    mListener.Prefixes.Add(listenUrl);

                    // Start listening
                    mListener.Start();

                    // Run new thread listening for content
                    // until we call Stop or Dispose
                    Task.Run(() => ListenForContent());

                    // Set Listening flag
                    Listening = true;

                    // Log it
                    CoreLogger.Log($"LiveServer listening on http://localhost:{Port}, directory '{ServingDirectory}'", type: LogType.Information);
                }
                catch (Exception ex)
                {
                    // Log it
                    Debugger.Break();
                    CoreLogger.Log($"LiveServer failed to start on port {Port}, directory '{ServingDirectory}'. {ex.Message}", type: LogType.Error);

                    // Go no further
                    return null;
                }

                #endregion

                #region File Change Watcher

                mFolderWatcher = new FolderWatcher
                {
                    Filter = "*.*",
                    Path = ServingDirectory,
                    UpdateDelay = 100
                };

                // Listen for file changes
                mFolderWatcher.FileChanged += FolderWatcher_FileChanged;

                mFolderWatcher.Start();

                #endregion

                // Return URL we are now listening on
                return listenUrl;
            }
        }

        /// <summary>
        /// Fired when a file has changed in the <see cref="ServingDirectory"/>
        /// </summary>
        /// <param name="obj"></param>
        private void FolderWatcher_FileChanged(string filePath)
        {
            // Make sure we have a path
            if (string.IsNullOrEmpty(filePath))
                return;

            // Make it lower case for quick checking of file extension
            var lowerString = filePath?.ToLower();

            // TODO: We could check the served files real references 
            //       and only refresh if they change
            //
            //       However for now its perfectly fine to just refresh 
            //       the page if any file changes in the folder that could
            //       be a file in the webpage
            //
            // Check if the file is a file that may be served to the browser
            if (lowerString.EndsWith(".htm") ||
                lowerString.EndsWith(".html") ||
                lowerString.EndsWith(".css") ||
                lowerString.EndsWith(".js") ||
                lowerString.EndsWith(".png") ||
                lowerString.EndsWith(".gif") ||
                lowerString.EndsWith(".jpg") ||
                lowerString.EndsWith(".jpeg"))
                // Let listeners know
                mContentChangedResetEvent.Set();
        }

        /// <summary>
        /// Listens forever for requests until the thread is terminated
        /// </summary>
        private void ListenForContent()
        {
            // Start new 
            while (!Stopping && !Disposing)
            {
                CoreLogger.Log($"LiveServer waiting for next response '{ServingDirectory}'...");

                // Note: The GetContext method blocks while waiting for a request.
                var result = mListener.BeginGetContext(new AsyncCallback((callback) =>
                {
                    try
                    {
                        // See if we are stopping
                        if (Stopping || Disposing)
                            return;

                        // Get context from result
                        var context = ((HttpListener)callback.AsyncState).EndGetContext(callback);

                        // Process it
                        Process(context);
                    }
                    catch (Exception ex)
                    {
                        // Log it
                        CoreLogger.Log($"LiveServer response failed. {ex.Message}", type: LogType.Warning);
                    }

                }), mListener);

                // Wait for result or stopping before moving to next loop
                while (!Stopping && !Disposing && !result.AsyncWaitHandle.WaitOne(10));
            }

            // Flag no longer listening
            Listening = false;
        }

        #endregion

        /// <summary>
        /// Processes the HTTP request
        /// </summary>
        /// <param name="context">The Http Context</param>
        private void Process(HttpListenerContext context)
        {
            // Log it
            CoreLogger.Log($"LiveServer Processing request {context.Request.Url.OriginalString}...");

            // Get the URL information after the hostname
            // i.e. http://localhost:8080/ would be /
            //      http://localhost:8080/some/path would be /some/path
            var url = context.Request.Url.AbsolutePath;

            // Get query string
            var query = context.Request.Url.Query;

            // If this is a request for the auto-reload script...
            if (query.EqualsIgnoreCase(AutoReloadRequestQueryUrl))
            {
                // Serve the Javascript script
                ServeString(AutoReloadJavascript, MimeTypes.GetExtension("file.js"), context);

                // Done
                return;
            }
            // If this is a request to return once there are changes...
            if (query.EqualsIgnoreCase(SignalNewContentQuery))
            {
                // Pass off this request to simply return successful once it get's told there is a file change
                HangUntilFileChange(context);

                return;
            }
            else
            {
                // If the URL is just / (root)
                if (string.IsNullOrWhiteSpace(url) || url == "/")
                    // Look for index by default
                    url = "index";
                // Otherwise...
                else
                    // Remove leading slash
                    url = url.Substring(1);

                // Now look in the watch directory for a file with this name...
                var filePath = DnaConfiguration.ResolveFullPath(ServingDirectory, url, false, out bool wasRelative);

                // If this file exists...
                if (File.Exists(filePath))
                {
                    // Serve it
                    ServeFile(filePath, context);

                    // Done
                    return;
                }

                // If the file has no extension, try adding .htm
                if (!Path.HasExtension(filePath) && File.Exists(filePath + ".htm"))
                {
                    // Serve it
                    ServeFile(filePath + ".htm", context);

                    // Done
                    return;
                }

                // If the file has no extension, try adding .html
                if (!Path.HasExtension(filePath) && File.Exists(filePath + ".html"))
                {
                    // Serve it
                    ServeFile(filePath + ".html", context);

                    // Done
                    return;
                }

                // Let client know the file is not found
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;

                // Close the response
                context.Response.OutputStream.Close();
            }
        }

        private void HangUntilFileChange(HttpListenerContext context)
        {
            Task.Run(() =>
            {
                // Wait here until files change
                mContentChangedResetEvent.WaitOne();

                // If we are stopping, just return
                if (Stopping)
                    return;

                // Response code OK (200)
                context.Response.StatusCode = (int)HttpStatusCode.OK;

                // End stream
                context.Response.OutputStream.Close();
            });
        }

        /// <summary>
        /// Writes a files contents to the response stream
        /// </summary>
        /// <param name="filePath">The absolute file path</param>
        /// <param name="context">The Http context</param>
        private void ServeFile(string filePath, HttpListenerContext context)
        {
            try
            {
                // Get file info
                var fileInfo = new FileInfo(filePath);

                // Adding http response headers
                context.Response.ContentType = MimeTypes.GetExtension(filePath);
                context.Response.AddHeader("Date", DateTime.Now.ToString("r"));
                context.Response.AddHeader("Last-Modified", fileInfo.LastAccessTime.ToString("r"));

                // If it is a HTML file just read it all in
                if (filePath.ToLower().EndsWith(".htm") || filePath.ToLower().EndsWith(".html"))
                {
                    // Read all the HTML
                    var htmlContents = File.ReadAllText(filePath);

                    // Inject the javascript auto-reload script
                    var headIndex = htmlContents.ToLower().IndexOf("<head>");

                    // If we found a head... inject JS
                    if (headIndex > 0)
                    {
                        // Find location just after the opening <head> tag
                        var injectHeader = headIndex + "<head>".Length;

                        // Inject the javascript src
                        htmlContents = htmlContents.Insert(injectHeader, $"<script type=\"text/javascript\" src=\"/{AutoReloadRequestQueryUrl}\" charset=\"UTF-8\"></script>");
                    }

                    // Get bytes for content
                    var bytes = Encoding.UTF8.GetBytes(htmlContents);

                    // Set content length
                    context.Response.ContentLength64 = htmlContents.Length;

                    // Write to response
                    context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                }
                else
                {
                    // Open file
                    using (var input = new FileStream(filePath, FileMode.Open))
                    {
                        // Set content length
                        context.Response.ContentLength64 = fileInfo.Length;

                        // Create 16kb buffer
                        var buffer = new byte[1024 * 16];
                        int nbytes;

                        // Read file into output stream in chunks until done
                        while ((nbytes = input.Read(buffer, 0, buffer.Length)) > 0)
                            context.Response.OutputStream.Write(buffer, 0, nbytes);
                    }
                }

                // Response code OK (200)
                context.Response.StatusCode = (int)HttpStatusCode.OK;

                // Flush stream so everything is written
                context.Response.OutputStream.Flush();

                // Log it
                CoreLogger.Log($"LiveServer Served File {filePath} ({Math.Round(fileInfo.Length / 1024f, 2)}kb)");
            }
            catch (Exception ex)
            {
                // Log it
                CoreLogger.Log($"LiveServer Unexpected Error Serving File {filePath}. {ex.Message}", type: LogType.Error);

                // Let browser know internal error
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
            finally
            {
                // Close the response
                context.Response.OutputStream.Close();
            }
        }

        /// <summary>
        /// Writes a string contents to the response stream
        /// </summary>
        /// <param name="contents">The contents to write</param>
        /// <param name="mimeType">The Mime type of this content</param>
        /// <param name="context">The Http context</param>
        private void ServeString(string contents, string mimeType, HttpListenerContext context)
        {
            // Adding http response headers
            context.Response.ContentType = mimeType;
            context.Response.ContentLength64 = contents.Length;
            context.Response.AddHeader("Date", DateTime.Now.ToString("r"));
            context.Response.AddHeader("Last-Modified", DateTime.Now.ToString("r"));

            // Get bytes for content
            var bytes = Encoding.UTF8.GetBytes(contents);

            // Write to response
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);

            // Response code OK (200)
            context.Response.StatusCode = (int)HttpStatusCode.OK;

            // Flush stream so everything is written
            context.Response.OutputStream.Flush();
        }

        #region Dispose

        /// <summary>
        /// Dispose
        /// </summary>
        public async void Dispose()
        {
            // Let threads know to exit
            Disposing = true;

            await StopAsync();
        }

        #endregion
    }
}