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
    using System.Net;
    using System.Net.Sockets;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;

    using log4net;

    using Mystiko.Database.Records;

    /// <summary>
    /// A server capable of listening for <see cref="TcpClientChannel"/> connections from other nodes in the network
    /// </summary>
    public class Server : IDisposable
    {
        public const ushort PROTOCOL_VERSION = 1;

        /// <summary>
        /// The logging implementation for recording the activities that occur in the methods of this class
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Server));

        /// <summary>
        /// The configuration of the node in which this server instance is executing
        /// </summary>
        private readonly NodeConfiguration _nodeConfiguration;

        /// <summary>
        /// The channel over which this server accepts connections
        /// </summary>
        private readonly IServerChannel _serverChannel;

        private readonly CancellationTokenSource _acceptCancellationTokenSource = new();

        /// <summary>
        /// A value indicating whether or not this object is disposed
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="Server"/> class.
        /// </summary>
        /// <param name="configuration">
        /// The configuration of this node
        /// </param>
        /// <param name="serverChannelFactory">
        /// A function that returns a <see cref="IServerChannel"/> for listening for incoming connections
        /// </param>
        public Server(
            NodeConfiguration configuration,
            Func<IServerChannel>? serverChannelFactory = null)
        {
            Debug.Assert(configuration != null);
            Debug.Assert(configuration.Identity != null, "configuration.Identity != null");
            Debug.Assert(configuration.Identity.PrivateKey != null, "configuration.Identity.PrivateKey != null");
            Debug.Assert(configuration.Identity.PublicKeyX != null, "configuration.Identity.PublicKeyX != null");
            Debug.Assert(configuration.Identity.PublicKeyX.Length == 32, "configuration.Identity.PublicKeyX.Length == 32");
            Debug.Assert(configuration.Identity.PublicKeyY != null, "configuration.Identity.PublicKeyY != null");
            Debug.Assert(configuration.Identity.PublicKeyY.Length == 32, "configuration.Identity.PublicKeyY.Length == 32");

            _nodeConfiguration = configuration;

            // Create network channel
            Debug.Assert(_nodeConfiguration.Identity != null, "this._nodeConfiguration.Identity != null");
            var channel = (serverChannelFactory ?? (() => new TcpServerChannel(_nodeConfiguration.Identity, IPAddress.Any, ListenerPort))).Invoke() 
                ?? throw new ArgumentException("Server channel factory returned null on invocation", nameof(serverChannelFactory));
            Debug.Assert(channel != null, "channel != null");
            _serverChannel = channel;
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
        /// Gets the port on which to listen for peer client connections.  By default, this is 5109
        /// </summary>
        public int ListenerPort { get; private set; }

        /// <summary>
        /// Places the server into a state where it listens for new connections
        /// </summary>
        /// <param name="disableLogging">A value indicating or not to disable logging</param>
        /// <param name="cancellationToken">A cancellation token to stop attempting to discover peers</param>
        /// <returns>A task that can be awaited while the operation completes</returns>
        public async Task StartAsync(bool disableLogging = false, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // Determine if this instance if behind a firewall.  Open a random port, and try to access it.
            if (_serverChannel is TcpServerChannel sc)
            {
#if !DEBUG
                Firewalled = await DetermineFirewallStatus(cancellationToken);
#endif
                sc.DisableLogging = disableLogging;
            }

            _serverChannel.Passive = Passive;
            await _serverChannel.StartAsync(_acceptCancellationTokenSource.Token);
        }

        /// <summary>
        /// Initiates a connection from this server to a peer node
        /// </summary>
        /// <param name="endpoint">The endpoint information for how to connect to the peer, as interpreted by the <see cref="IServerChannel"/> implementation</param>
        /// <returns>A task that can be awaited while the operation completes</returns>
        public async Task<IClientChannel?> ConnectToPeerAsync(IPEndPoint endpoint)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(endpoint);
            return await _serverChannel.ConnectToPeerAsync(endpoint, _acceptCancellationTokenSource.Token);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                // Dispose managed resources.
                _acceptCancellationTokenSource.Cancel();
                var channel = _serverChannel as IDisposable;
                channel?.Dispose();

                // There are no unmanaged resources to release, but
                // if we add them, they need to be released here.
            }

            _disposed = true;
        }

        /// <summary>
        /// Determines whether the server is behind a firewall by attempting to contact itself over a random TCP port to its public IP address
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to stop attempting to discover peers</param>
        /// <returns>A value indicating whether the server is behind a firewall or NAT that prevent it from operating a server process on the Internet that can accept inbound connection requests</returns>
        private static async Task<bool?> DetermineFirewallStatus(CancellationToken cancellationToken = default)
        {
            // Determine if this instance if behind a firewall.  Open a random port, and try to access it.
            Logger.Debug("Determining whether this node is behind a firewall...");
            var publicIp = await NetUtility.FindPublicIPAddressAsync(cancellationToken);
            if (publicIp == null)
            {
                return null;
            }

            Random random;
            using (var rng = RandomNumberGenerator.Create())
            {
                var randomBytes = new byte[4];
                Debug.Assert(rng != null, "rng != null");
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
                        Logger.Debug($"Accepted connection from {((IPEndPoint?)receivingClient.Client.RemoteEndPoint)?.Address}:{((IPEndPoint?)receivingClient.Client.RemoteEndPoint)?.Port} to {((IPEndPoint?)receivingClient.Client.LocalEndPoint)?.Address}:{((IPEndPoint?)receivingClient.Client.LocalEndPoint)?.Port}");
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
                        using var initiatingClient = new TcpClient();
                        Logger.Debug($"Connecting to TCP {publicIp}:{port}");
                        await initiatingClient.ConnectAsync(publicIp, port);
                    }
                    catch (SocketException sex)
                    {
                        Logger.Debug($"Exception when attempting to connect {sex.Message}");
                    }
                },
                cancellationToken);

            var completed = Task.WaitAll([listenerTask, connectorTask], 10000, cancellationToken);
            var firewalled = !completed || !connectionMade;
            Logger.Info($"Firewall status: {firewalled}");
            return firewalled;
        }
    }
}
