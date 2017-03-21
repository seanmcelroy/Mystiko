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
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using log4net;

    using Mystiko.Cryptography;
    using Mystiko.Database.Records;
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
        /// Gets or sets the configuration of this node
        /// </summary>
        [CanBeNull]
        private NodeConfiguration Configuration { get; set; }

        /// <summary>
        /// Gets or sets the location of this node
        /// </summary>
        [CanBeNull]
        private byte[] Location { get; set; }

        /// <summary>
        /// Gets or sets the salt for the encryption of the configuration of this node
        /// </summary>
        [CanBeNull]
        private byte[] Salt { get; set; }

        /// <summary>
        /// Gets or sets the encryption key for the configuration of this node
        /// </summary>
        [CanBeNull]
        private byte[] EncryptionKey { get; set; }

        /// <summary>
        /// Gets or sets whether or not to supress casual INFO logging of the methods of this class
        /// </summary>
        public bool DisableLogging { get; set; }

        /// <summary>
        /// The network server object
        /// </summary>
        [CanBeNull]
        private Server server;

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
        /// <param name="password">The password used to unlock the node's database</param>
        /// <param name="passive">
        /// A value indicating whether the server channel will not broadcast its presence, but will listen for other nodes only
        /// </param>
        /// <param name="listenerPort">
        /// The port on which to listen for peer client connections.  By default, this is 5109
        /// </param>
        public Node([NotNull] string tag, [NotNull] string password, bool? passive = null, int? listenerPort = null)
        {
            if (tag == null)
                throw new ArgumentNullException(nameof(tag));

            if (password == null)
                throw new ArgumentNullException(nameof(password));

            this.Tag = tag;
            this.LoadConfigurationInternalAsync(null, password, passive, listenerPort).GetAwaiter().GetResult();
            Debug.Assert(this.Configuration != null, "this.Configuration != null");
        }

        /// <summary>
        /// Gets or sets the tag, a name of a node that can be used to identify it in multiple-node local simulations
        /// </summary>
        public string Tag { get; set; }

        /// <summary>
        /// Loads the configuration from a configuration file
        /// </summary>
        /// <param name="filePath">The path of the configuration file to load</param>
        /// <param name="password">The user-supplied password used to derive the encryption key used to decode the configuration file</param>
        /// <param name="passive">A value indicating whether the server channel will not broadcast its presence, but will listen for other nodes only</param>
        /// <param name="listenerPort">The port on which to listen for peer client connections.  By default, this is 5109</param>
        /// <returns>A tuple containing the node configuration, the salt value, and the encryption key used to decode the configuration file</returns>
        [NotNull, Pure, ItemNotNull]
        public static async Task<Tuple<NodeConfiguration, byte[], byte[]>> LoadConfigurationAsync(
            [NotNull] string filePath,
            [NotNull] string password,
            bool? passive = null,
            int? listenerPort = null)
        {
            NodeConfiguration nodeConfiguration;
            var configurationFile = filePath;

            if (!File.Exists(configurationFile))
            {
                // Create new node configuration file
                nodeConfiguration = new NodeConfiguration
                {
                    Passive = passive ?? false,
                    ListenerPort = listenerPort ?? 5109
                };

                var newIdentityAndKey = ServerNodeIdentity.Generate(3);
                Debug.Assert(newIdentityAndKey != null, "newIdentityAndKey != null");
                nodeConfiguration.Identity = new ServerNodeIdentityAndKey
                {
                    DateEpoch = newIdentityAndKey.Item1.DateEpoch,
                    Nonce = newIdentityAndKey.Item1.Nonce,
                    PrivateKey = newIdentityAndKey.Item2,
                    PublicKeyX = newIdentityAndKey.Item1.PublicKeyX,
                    PublicKeyY = newIdentityAndKey.Item1.PublicKeyY
                };
                
                await SaveConfigurationAsync(filePath, password, nodeConfiguration);
            }

            // Read configuration file
            var salt = new byte[64];
            byte[] encKey;
            using (var fs = new FileStream(configurationFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                // Read salt
                var read = await fs.ReadAsync(salt, 0, 64);
                Debug.Assert(read == 64);
                Debug.Assert(fs.Position == 64);

                // Get encryption key
                using (var pbkdf2Encoder = new Rfc2898DeriveBytes(password, salt, 100000))
                {
                    encKey = pbkdf2Encoder.GetBytes(32);
                    pbkdf2Encoder.Reset();
                }
                Debug.Assert(encKey != null, "encKey != null");
                
                var encryptedBytes = new byte[fs.Length - 64];
                await fs.ReadAsync(encryptedBytes, 0, encryptedBytes.Length);
                var decryptedStream = new MemoryStream();
                await EncryptUtility.DecryptStreamAsync(new MemoryStream(encryptedBytes), encKey, decryptedStream);
                var decryptedBytes = decryptedStream.ToArray();
                Debug.Assert(decryptedBytes != null, "decryptedBytes.Length != null");
                Debug.Assert(decryptedBytes.Length > 0, "decryptedBytes.Length > 0");

                var decryptedString = System.Text.Encoding.UTF8.GetString(decryptedBytes).TrimEnd('\0');
                Debug.Assert(decryptedString != null, "decryptedString != null");
                if (string.CompareOrdinal(decryptedString, 0, "MYSTIKO!", 0, 8) != 0)
                {
                    throw new InvalidOperationException("Incorrect password");
                }

                var version = Convert.ToInt32(decryptedString.Substring(8, 8));

                var serializedConfiguration = decryptedString.Substring(16);
                nodeConfiguration = JsonConvert.DeserializeObject<NodeConfiguration>(serializedConfiguration);
            }

            return new Tuple<NodeConfiguration, byte[], byte[]>(nodeConfiguration, salt, encKey);
        }

        [NotNull]
        private async Task LoadConfigurationInternalAsync(
            [CanBeNull] string configurationFile,
            [NotNull] string password,
            bool? passive = null,
            int? listenerPort = null)
        {
            configurationFile = configurationFile ?? Path.Combine(AppContext.BaseDirectory, $"node.{this.Tag}.config");
            Debug.Assert(configurationFile != null, "configurationFile != null");
            var ret = await LoadConfigurationAsync(configurationFile, password, passive, listenerPort);
            Debug.Assert(ret.Item1 != null, "ret.Item1 != null");
            Debug.Assert(ret.Item1.Identity != null, "ret.Item1.Identity != null");
            this.Configuration = ret.Item1;
            this.Salt = ret.Item2;
            this.EncryptionKey = ret.Item3;

            this.Location = FileUtility.ExclusiveOr(this.Configuration.Identity.PublicKeyX, this.Configuration.Identity.PublicKeyY);
            Logger.Debug($"{this.Configuration.Identity.GetCompositeHash().Substring(3, 8)}: Node location: {FileUtility.ByteArrayToString(this.Location)}");
        }

        [NotNull]
        public static async Task SaveConfigurationAsync(
            [NotNull] string configurationFile,
            [NotNull] string password,
            [NotNull] NodeConfiguration configuration)
        {
            var salt = new byte[64];
            if (File.Exists(configurationFile))
            {
                // Get existing salt
                using (var fs = new FileStream(configurationFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var read = await fs.ReadAsync(salt, 0, 64);
                    Debug.Assert(read == 64);
                }

                File.Delete(configurationFile);
            }
            else
            {
                // Create new salt
                using (var rng = RandomNumberGenerator.Create())
                {
                    Debug.Assert(rng != null, "rng != null");
                    rng.GetBytes(salt);
                }
            }

            // Get encryption key
            byte[] encKey;
            using (var pbkdf2Encoder = new Rfc2898DeriveBytes(password, salt, 100000))
            {
                encKey = pbkdf2Encoder.GetBytes(32);
                pbkdf2Encoder.Reset();
            }
            Debug.Assert(encKey != null, "encKey != null");

            await SaveConfigurationAsync(configurationFile, salt, encKey, configuration);
        }

        [NotNull]
        public static async Task SaveConfigurationAsync(
            [NotNull] string configurationFile,
            [NotNull] byte[] salt,
            [NotNull] byte[] encryptionKey,
            [NotNull] NodeConfiguration configuration)
        {
            if (configuration == null)
                throw new InvalidOperationException("No configuration has been loaded");
            if (encryptionKey == null)
                throw new ArgumentNullException(nameof(encryptionKey));
            
            // Write header then data
            if (File.Exists(configurationFile))
                File.Delete(configurationFile);

            using (var fs = new FileStream(configurationFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var outputStream = new MemoryStream();

                // Write salt
                await fs.WriteAsync(salt, 0, 64);

                // Write known text
                var knownTextBytes = System.Text.Encoding.UTF8.GetBytes("MYSTIKO!");
                Debug.Assert(knownTextBytes != null, "knownTextBytes != null");
                await outputStream.WriteAsync(knownTextBytes, 0, knownTextBytes.Length);

                // Write version
                var versionBytes = System.Text.Encoding.UTF8.GetBytes("00000001");
                await outputStream.WriteAsync(versionBytes, 0, versionBytes.Length);

                // Write content
                var serializedConfiguration = JsonConvert.SerializeObject(configuration);
                var serializedConfigurationBytes = System.Text.Encoding.UTF8.GetBytes(serializedConfiguration);
                await outputStream.WriteAsync(serializedConfigurationBytes, 0, serializedConfigurationBytes.Length);

                outputStream.Seek(0, SeekOrigin.Begin);
                await EncryptUtility.EncryptStreamAsync(outputStream, encryptionKey, fs);
            }
        }
        
        [NotNull]
        private async Task SaveConfigurationInternalAsync(
            [CanBeNull] string configurationFile, [NotNull] byte[] salt, [NotNull] byte[] encryptionKey)
        {
            if (encryptionKey == null)
                throw new ArgumentNullException(nameof(encryptionKey));
            if (this.Configuration == null)
                throw new InvalidOperationException("No configuration has been loaded");

            configurationFile = configurationFile ?? Path.Combine(AppContext.BaseDirectory, $"node.{this.Tag}.config");
            Debug.Assert(configurationFile != null, "configurationFile != null");

            await SaveConfigurationAsync(configurationFile, salt, encryptionKey, this.Configuration);
        }

        /// <summary>
        /// Starts a node's local file system and networking functions
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to stop attempting to discover peers</param>
        /// <returns>A value indicating whether the server is behind a firewall or NAT that prevent it from operating a server process on the Internet that can accept inbound connection requests</returns>
        public async Task StartAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (this.Configuration == null)
                throw new InvalidOperationException("Configuration has not been loaded");

            this.server = new Server(this.Configuration);

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
            string tfid;
            using (var fs = new FileStream(manifestFile.FullName, FileMode.Open, FileAccess.Read))
            using (var bfs = new BufferedStream(fs))
            {
                var tempFile = manifestFile.FullName + ".encrypted";
                var keyManifest = await EncryptUtility.GenerateKeyAndEncryptFileAsync(bfs, new FileInfo(tempFile));

                /*
                 * Generate temporal file identifier (TFID)
                 */
                var blockXors = new byte[32];
                foreach (var fileName in Directory.GetFiles(repoDirectory, chunkResult.Name + ".*"))
                {
                    // Get first 32 bytes to prepare XOR
                    using (var br = new BinaryReader(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, 32)))
                    {
                        var fileFirst32 = br.ReadBytes(32);
                        Debug.Assert(fileFirst32 != null, "fileFirst32 != null");
                        Debug.Assert(fileFirst32.Length == 32, "fileFirst32.Length == 32");
                        blockXors = FileUtility.ExclusiveOr(blockXors, fileFirst32);
                    }
                }

                // TODO: XOR progressive network entropy
                tfid = FileUtility.ByteArrayToString(FileUtility.ExclusiveOr(blockXors, keyManifest));

                File.Move(tempFile, Path.Combine(dataDirectory, tfid + ".0"));
            }
            manifestFile.Delete();

            /*
             * Copy LIBRARY parts, except the manifest into the REPO folder
             */
            foreach (var fileName in Directory.GetFiles(dataDirectory, chunkResult.Name + ".*"))
            {
                File.Move(fileName, Path.Combine(dataDirectory, tfid + Path.GetExtension(fileName)));
            }

            foreach (var fileName in Directory.GetFiles(dataDirectory, tfid + ".*"))
            {
                File.Copy(fileName, Path.Combine(repoDirectory, tfid + Path.GetExtension(fileName)));
            }

            /*
             * Create Resource Record
             */
            using (var rng = RandomNumberGenerator.Create())
            {
                Debug.Assert(rng != null, "rng != null");

                var rr = new ResourceRecord
                         {
                             TemporalFileID = tfid,
                             EntropyTimestamp = 0,
                             BlockHashes = chunkResult.BlockHashes.OrderBy(h =>
                             {
                                 var iBytes = new byte[16];
                                 rng.GetBytes(iBytes);
                                 return BitConverter.ToInt32(iBytes, 0);
                             }).ToArray()
                         };

                Debug.Assert(this.Configuration != null, "this.Configuration != null");
                Debug.Assert(this.Configuration.ResourceRecords != null, "this.Configuration.ResourceRecords != null");
                this.Configuration.ResourceRecords.Add(rr);

                await this.SaveConfigurationInternalAsync(null, this.Salt, this.EncryptionKey);

                using (var fs = new FileStream(Path.Combine(dataDirectory, tfid + ".rr"), FileMode.CreateNew))
                using (var sw = new StreamWriter(fs))
                {
                    sw.Write(JsonConvert.SerializeObject(rr));
                }
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