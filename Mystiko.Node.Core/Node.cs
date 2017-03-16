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
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using log4net;

    using Mystiko.Cryptography;
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
        [NotNull]
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Node));

        /// <summary>
        /// Gets or sets whether or not to supress casual INFO logging of the methods of this class
        /// </summary>
        public bool DisableLogging { get; set; }

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
            var libraryDirectory = Path.Combine(AppContext.BaseDirectory, "Library" + this.Tag);
            if (!Directory.Exists(libraryDirectory))
            {
                Directory.CreateDirectory(libraryDirectory);
            }

            var repoDirectory = Path.Combine(AppContext.BaseDirectory, "Repository" + this.Tag);
            if (!Directory.Exists(repoDirectory))
            {
                Directory.CreateDirectory(repoDirectory);
            }


            var lockFile = Path.Combine(libraryDirectory, "lock");
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
                    throw new InvalidOperationException($"Library directory {libraryDirectory} already in use by another process.", ex);
                }

                this.lockFileStream = fileStreamAttempt;
            }
            
            // Start network subsystem
            Logger.Info($"Starting server initialization{(" " + this.Tag).TrimEnd()}");
            await this.server.StartAsync(this.DisableLogging, cancellationToken);
            Logger.Info($"Server process initialization{(" " + this.Tag).TrimEnd()} has completed");
        }

        /// <summary>
        /// Inserts content into the local store and makes it available to the wider network
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public async Task<bool> InsertFileAsync([NotNull] FileInfo file)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));
            if (!file.Exists)
                throw new FileNotFoundException("File does not exist", file.FullName);

            if (!this.DisableLogging)
                Logger.Info($"Inserting file {file.FullName}...");

            /*
             * Store chunks and manifest locally in the LIBRARY directory
             */
            var dataDirectory = Path.Combine(AppContext.BaseDirectory, "Library" + this.Tag);
            var manifestFile = new FileInfo(Path.Combine(dataDirectory, file.Name + ".manifest"));
            if (manifestFile.Exists)
            {
                Logger.Warn($"Manifest {manifestFile.FullName} already exists.  Skipping insert.");
                return false;
            }

            FileManifest chunkResult;
            try
            {
                chunkResult = await FileUtility.ChunkFileViaOutputDirectory(file, new DirectoryInfo(dataDirectory));
            }
            catch (Exception ex)
            {
                Logger.Warn($"Problem chunking file {file.FullName}.  Skipping insert.", ex);
                return false;
            }

            using (var fs = new FileStream(manifestFile.FullName, FileMode.CreateNew))
            using (var sw = new StreamWriter(fs))
            {
                var json = JsonConvert.SerializeObject(chunkResult);
                if (json != null)
                {
                    await sw.WriteAsync(json);
                }
            }

            /*
             * Encrypt manifest with Manifest Decrypt Key (MDK)
             */
            var repoDirectory = Path.Combine(AppContext.BaseDirectory, "Repository" + this.Tag);
            byte[] encKey;
            using (var fs = new FileStream(manifestFile.FullName, FileMode.Open, FileAccess.Read))
            using (var bfs = new BufferedStream(fs))
            {
                var tempFile = manifestFile.FullName + ".encrypted";
                encKey = await EncryptUtility.GenerateKeyAndEncryptFileAsync(bfs, new FileInfo(tempFile));
                File.Move(tempFile, Path.Combine(repoDirectory, FileUtility.ByteArrayToString(encKey) + ".0"));
            }

            /*
             * Copy LIBRARY parts, except the manifest into the REPO folder
             */
            foreach (var fileName in Directory.GetFiles(dataDirectory, chunkResult.Name + ".*"))
            {
                if (fileName.EndsWith(".manifest"))
                    continue;

                File.Copy(fileName, Path.Combine(repoDirectory, FileUtility.ByteArrayToString(encKey) + Path.GetExtension(fileName)));
            }

            return true;
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