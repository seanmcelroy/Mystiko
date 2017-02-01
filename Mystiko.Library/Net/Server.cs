// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Server.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   A server capable of listening for <see cref="Client" /> connections from other nodes in the network
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Net
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.IO.IsolatedStorage;
    using System.Net;
    using System.Net.Sockets;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using log4net;

    using Newtonsoft.Json;

    /// <summary>
    /// A server capable of listening for <see cref="Client"/> connections from other nodes in the network
    /// </summary>
    public class Server : IDisposable
    {
        /// <summary>
        /// The logging implementation for recording the activities that occur in the methods of this class
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Server));

        /// <summary>
        /// The identity of the server node, consisting of a date of generation, key pair, and nonce proving the date and public key
        /// meet a target difficulty requirement.  It also includes the private key for local serialization and storage.
        /// </summary>
        [NotNull]
        private readonly ServerNodeIdentityAndKey _serverNodeIdentityAndKey;

        /// <summary>
        /// The channel over which this server accepts connections
        /// </summary>
        [NotNull]
        private readonly IServerChannel _serverChannel;

        [NotNull]
        private readonly CancellationTokenSource _acceptCancellationTokenSource = new CancellationTokenSource();

        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="Server"/> class.
        /// </summary>
        /// <param name="passive">
        /// A value indicating whether the server channel will not broadcast its presence, but will listen for other nodes only
        /// </param>
        /// <param name="serverNodeIdentityFactory">
        /// A function that returns a tuple of a <see cref="ServerNodeIdentity"/> and the private key of that identity as a byte array
        /// </param>
        /// <param name="serverChannelFactory">
        /// A function that returns a <see cref="IServerChannel"/> for listening for incoming connections
        /// </param>
        /// <param name="listenerPort">
        /// The port on which to listen for peer client connections.  By default, this is 5109
        /// </param>
        public Server(
            bool passive = false,
            [CanBeNull] Func<Tuple<ServerNodeIdentity, byte[]>> serverNodeIdentityFactory = null,
            [CanBeNull] Func<IServerChannel> serverChannelFactory = null,
            int listenerPort = 5109)
        {
            this.Passive = passive;

            // Load or create node identity
            using (var isoStore = IsolatedStorageFile.GetStore(IsolatedStorageScope.User | IsolatedStorageScope.Assembly, null, null))
            {
                if (!isoStore.FileExists($"node.identity.{listenerPort}"))
                {
                    // Create new node identity
                    var newIdentityAndKey = (serverNodeIdentityFactory ?? (() => ServerNodeIdentity.Generate(3))).Invoke();

                    Debug.Assert(newIdentityAndKey != null, "newIdentityAndKey != null");
                    this._serverNodeIdentityAndKey = new ServerNodeIdentityAndKey
                                                       {
                                                           DateEpoch = newIdentityAndKey.Item1.DateEpoch,
                                                           Nonce = newIdentityAndKey.Item1.Nonce,
                                                           PrivateKey = newIdentityAndKey.Item2,
                                                           PublicKeyX = newIdentityAndKey.Item1.PublicKeyX,
                                                           PublicKeyY = newIdentityAndKey.Item1.PublicKeyY,
                                                       };

                    using (var isoStream = new IsolatedStorageFileStream($"node.identity.{listenerPort}", FileMode.CreateNew, isoStore))
                    using (var sw = new StreamWriter(isoStream))
                    {
                        sw.Write(JsonConvert.SerializeObject(this._serverNodeIdentityAndKey));
                        sw.Close();
                    }
                }

                // Store the newly-created identity in isolated storage so we can quickly retrieve it again
                using (var isoStream = new IsolatedStorageFileStream($"node.identity.{listenerPort}", FileMode.Open, isoStore))
                using (var sr = new StreamReader(isoStream))
                {
                    this._serverNodeIdentityAndKey = JsonConvert.DeserializeObject<ServerNodeIdentityAndKey>(sr.ReadToEnd());
                    isoStream.Close();
                }
            }

            // Create network channel
            var channel = (serverChannelFactory ?? (() => new TcpServerChannel(this._serverNodeIdentityAndKey, IPAddress.Any, listenerPort))).Invoke();
            if (channel == null)
            {
                throw new ArgumentException("Server channel factory returned null on invocation", nameof(serverChannelFactory));
            }

            Debug.Assert(channel != null, "channel != null");
            this._serverChannel = channel;
        }

        /// <summary>
        /// Gets a value indicating whether the server is behind a firewall or NAT that prevent it from operating a server process on the Internet that can accept inbound connection requests
        /// </summary>
        public bool? Firewalled { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether the server channel will not broadcast its presence, but will listen for other nodes only.
        /// </summary>
        public bool Passive { get; set; }

        /// <summary>
        /// Places the server into a state where it listens for new connections
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to stop attempting to discover peers</param>
        /// <returns>A task that can be awaited while the operation completes</returns>
        [NotNull]
        public async Task StartAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (this._disposed)
            {
                throw new ObjectDisposedException("Server");
            }

            // Determine if this instance if behind a firewall.  Open a random port, and try to access it.
            var sc = this._serverChannel as TcpServerChannel;
            if (sc != null)
            {
                #if !DEBUG
                this.Firewalled = await DetermineFirewallStatus(cancellationToken);
                #endif
            }

            this._serverChannel.Passive = this.Passive;
            await this._serverChannel.StartAsync(this._acceptCancellationTokenSource.Token);
        }

        /// <summary>
        /// Initiates a connection from this server to a peer node
        /// </summary>
        /// <param name="endpoint">The endpoint information for how to connect to the peer, as interpreted by the <see cref="IServerChannel"/> implementation</param>
        /// <returns>A task that can be awaited while the operation completes</returns>
        [NotNull, ItemCanBeNull]
        public async Task<IClientChannel> ConnectToPeerAsync([NotNull] IPEndPoint endpoint)
        {
            if (this._disposed)
            {
                throw new ObjectDisposedException("Server");
            }

            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            if (this._disposed)
            {
                throw new ObjectDisposedException("Server");
            }

            return await this._serverChannel.ConnectToPeerAsync(endpoint, this._acceptCancellationTokenSource.Token);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (!this._disposed)
            {
                // Dispose managed resources.
                this._acceptCancellationTokenSource.Cancel();
                var channel = this._serverChannel as IDisposable;
                channel?.Dispose();

                // There are no unmanaged resources to release, but
                // if we add them, they need to be released here.
            }

            this._disposed = true;
        }

        /// <summary>
        /// Determines whether the server is behind a firewall by attempting to contact itself over a random TCP port to its public IP address
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to stop attempting to discover peers</param>
        /// <returns>A value indicating whether the server is behind a firewall or NAT that prevent it from operating a server process on the Internet that can accept inbound connection requests</returns>
        private static async Task<bool?> DetermineFirewallStatus(CancellationToken cancellationToken = default(CancellationToken))
        {
            // Determine if this instance if behind a firewall.  Open a random port, and try to access it.
            Logger.Debug("Determining whether this node is behind a firewall...");
            var publicIp = await NetUtility.FindPublicIPAddressAsync(cancellationToken);
            if (publicIp == null)
                return null;

            Random random;
            using (var rng = new RNGCryptoServiceProvider())
            {
                var randomBytes = new byte[4];
                rng.GetBytes(randomBytes);
                var seed = BitConverter.ToInt32(randomBytes, 0);
                random = new Random(seed);
            }

            var port = random.Next(10000, 65535);
            Logger.Debug($"Checking on random port {port}");
            var listener = new TcpListener(IPAddress.Any, port);
            Logger.Debug($"Listening on TCP {port}...");
            var connectionMade = false;
            listener.Start();
            var listenerTask = Task.Run(
                async () =>
                {
                    try
                    {
                        var receivingClient = await listener.AcceptTcpClientAsync();
                        Debug.Assert(receivingClient != null, "receivingClient != null");
                        Logger.Debug($"Accepted connection from {((IPEndPoint)receivingClient.Client.RemoteEndPoint).Address}:{((IPEndPoint)receivingClient.Client.RemoteEndPoint).Port} to {((IPEndPoint)receivingClient.Client.LocalEndPoint).Address}:{((IPEndPoint)receivingClient.Client.LocalEndPoint).Port}");
                        connectionMade = true;
                    }
                    catch (SocketException sex)
                    {
                        Logger.Debug($"Exception when attempting to accept connection {sex.Message}", sex);
                    }
                }, 
                cancellationToken);

            var connectorTask = Task.Run(
                async () =>
                {
                    try
                    {
                        using (var initiatingClient = new TcpClient())
                        {
                            Logger.Debug($"Connecting to TCP {publicIp}:{port}");
                            await initiatingClient.ConnectAsync(publicIp, port);
                        }
                    }
                    catch (SocketException sex)
                    {
                        Logger.Debug($"Exception when attempting to connect {sex.Message}");
                    }
                }, 
                cancellationToken);

            var completed = Task.WaitAll(new[] { listenerTask, connectorTask }, 10000, cancellationToken);
            var firewalled = !completed || !connectionMade;
            Logger.Info($"Firewall status: {firewalled}");
            return firewalled;
        }
    }
}
