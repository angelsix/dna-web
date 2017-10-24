using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dna.Web.Core
{
    /// <summary>
    /// Handles <see cref="LiveServer"/> setup/teardown in bulk based on watch directories
    /// </summary>
    public class LiveServerManager : IDisposable
    {
        #region Protected Members

        /// <summary>
        /// A list of servers that are running and listening on certain directories
        /// </summary>
        protected List<LiveServer> mLiveServers = new List<LiveServer>();

        #endregion

        #region Public Properties

        /// <summary>
        /// A list of servers that are running and listening on certain directories
        /// </summary>
        public LiveServer[] LiveServers => mLiveServers?.ToArray();

        #endregion

        /// <summary>
        /// Spins up a <see cref="LiveServer"/> for this watch directory
        /// </summary>
        /// <param name="watchDirectory">The directory to watch and serve files from</param>
        /// <returns>Returns the URL that is being listened on</returns>
        public string CreateLiveServer(string watchDirectory)
        {
            // Create new LiveServer
            var liveServer = new LiveServer
            {
                // Set watch directory
                ServingDirectory = watchDirectory
            };

            // Add server to list
            mLiveServers.Add(liveServer);

            // Start listening
            return liveServer.Listen();
        }

        /// <summary>
        /// Stops all LiveServer's
        /// </summary>
        /// <returns></returns>
        public Task StopAsync()
        {
            return AsyncAwaitor.AwaitAsync(nameof(LiveServerManager) + nameof(StopAsync), async () =>
            {
                // For each server
                foreach (var server in mLiveServers)
                    // Stop server
                    await server.StopAsync();

                // Clear list
                mLiveServers.Clear();
            });
        }

        #region Dispose

        /// <summary>
        /// Dispose
        /// </summary>
        public async void Dispose()
        {
            // Stop
            await StopAsync();
        }

        #endregion
    }
}
