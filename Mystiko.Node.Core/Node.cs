// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Node.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   A node is a host that handles local management of files and communication
//   with remote peers in the network
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Node.Core
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using log4net;

    using Mystiko.IO;

    using Net;

    using Newtonsoft.Json;

    /// <summary>
    /// A node is a host that handles local management of files and communication
    /// with remote peers in the network
    /// </summary>
    /// <remarks>
    /// Think of this as the master wrapper for a <see cref="Net.Server"/>,
    /// with many of the functions of a package manager.
    /// </remarks>
    public class Node : IDisposable
    {
        /// <summary>
        /// The logging implementation for recording the activities that occur in the methods of this class
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Node));

        /// <summary>
        /// The network server object
        /// </summary>
        [NotNull]
        private readonly Server server;

        /// <summary>
        /// If a lock file exists, this is the stream holding that lock
        /// </summary>
        [CanBeNull]
        private FileStream lockFileStream;

        /// <summary>
        /// A value indicating whether or not this object is disposed
        /// </summary>
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="Node"/> class.
        /// </summary>
        /// <param name="passive">
        /// A value indicating whether the server channel will not broadcast its presence, but will listen for other nodes only
        /// </param>
        /// <param name="listenerPort">
        /// The port on which to listen for peer client connections.  By default, this is 5109
        /// </param>
        public Node(bool passive = false, int listenerPort = 5109)
        {
            this.server = new Server(passive, listenerPort: listenerPort);
        }

        /// <summary>
        /// Gets or sets the tag, a name of a node that can be used to identify it in multiple-node local simulations
        /// </summary>
        public string Tag { get; set; }

        /// <summary>
        /// Starts a node's local file system and networking functions
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to stop attempting to discover peers</param>
        /// <returns>A value indicating whether the server is behind a firewall or NAT that prevent it from operating a server process on the Internet that can accept inbound connection requests</returns>
        public async Task StartAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            // Setup data directory

            // Lock file
            var dataDirectory = Path.Combine(AppContext.BaseDirectory, "Data" + this.Tag);
            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
            }

            var lockFile = Path.Combine(dataDirectory, "lock");
            if (!File.Exists(lockFile))
            {
                this.lockFileStream = File.Open(lockFile, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            }
            else
            {
                FileStream fileStreamAttempt;
                try
                {
                    fileStreamAttempt = File.Open(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Data directory {dataDirectory} already in use by another process.", ex);
                }

                this.lockFileStream = fileStreamAttempt;
            }
            
            // Start network subsystem
            Logger.Info($"Starting server initialization{(" " + this.Tag).TrimEnd()}");
            await this.server.StartAsync(cancellationToken);
            Logger.Info($"Server process initialization{(" " + this.Tag).TrimEnd()} has completed");
        }

        public async Task InsertFileAsync([NotNull] FileInfo file)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            Logger.Info($"Inserting file {file.FullName}...");

            var dataDirectory = Path.Combine(AppContext.BaseDirectory, "Data" + this.Tag);
            var manifestFile = new FileInfo(Path.Combine(dataDirectory, file.Name + ".manifest"));
            if (manifestFile.Exists)
            {
                Logger.Warn($"Manifest {manifestFile.FullName} already exists.  Skipping insert.");
                return;
            }

            var chunkResult = await FileUtility.ChunkFileViaOutputDirectory(file, new DirectoryInfo(dataDirectory));

            using (var fs = new FileStream(manifestFile.FullName, FileMode.CreateNew))
            using (var sw = new StreamWriter(fs))
            {
                var json = JsonConvert.SerializeObject(chunkResult);
                if (json != null)
                {
                    await sw.WriteAsync(json);
                }
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (!this.disposed)
            {
                // Dispose managed resources.
                this.server.Dispose();

                if (this.lockFileStream != null)
                {
                    var lockFileName = this.lockFileStream.Name;
                    this.lockFileStream.Dispose();
                    this.lockFileStream = null;
                    File.Delete(lockFileName);
                }

                // There are no unmanaged resources to release, but
                // if we add them, they need to be released here.
            }

            this.disposed = true;
        }
    }
}