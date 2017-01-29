// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TcpServerChannel.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   A channel for servers to accept TCP/IP clients
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Net
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using log4net;

    /// <summary>
    /// A channel for servers to accept TCP/IP clients
    /// </summary>
    public class TcpServerChannel : IServerChannel, IDisposable
    {
        /// <summary>
        /// The logging implementation for recording the activities that occur in the methods of this class
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger(typeof(TcpServerChannel));

        /// <summary>
        /// The identity of the server node
        /// </summary>
        [NotNull]
        private readonly ServerNodeIdentity _serverIdentity;

        /// <summary>
        /// The network listener for incoming peer connections
        /// </summary>
        [NotNull]
        private readonly TcpListener _listener;

        /// <summary>
        /// The list of connected client peers
        /// </summary>
        [NotNull]
        private readonly List<TcpClientChannel> _clients = new List<TcpClientChannel>();

        [NotNull]
        private readonly TcpPeerDiscoveryChannel _peerDiscovery;

        /// <summary>
        /// The task that listens for incoming peer connections and accepts them into the list of clients
        /// </summary>
        [CanBeNull]
        private Task _acceptTask;
        
        /// <summary>
        /// A value indicating whether this object has been disposed
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="TcpServerChannel"/> class. 
        /// </summary>
        /// <param name="serverIdentity">
        /// The identity of the server node
        /// </param>
        /// <param name="listenAddress">
        /// The IP address on which to listen for peer client connections.  By default, this is <see cref="IPAddress.Any"/>
        /// </param>
        /// <param name="listenPort">
        /// The port on which to listen for peer client connections.  By default, this is 5109
        /// </param>
        /// <param name="multicastGroupAddress">
        /// The IP multicast address used for local peer discovery broadcasts.  By default, this is 224.0.23.191
        /// </param>
        /// <param name="multicastReceivePort">
        /// The port used for local peer discovery broadcasts.  By default this is 5110
        /// </param>
        public TcpServerChannel(
            [NotNull] ServerNodeIdentity serverIdentity, 
            [CanBeNull] IPAddress listenAddress = null, 
            int listenPort = 5109,
            [CanBeNull] IPAddress multicastGroupAddress = null,
            int multicastReceivePort = 5110)
        {
            if (serverIdentity == null)
            {
                throw new ArgumentNullException(nameof(serverIdentity));
            }

            this._serverIdentity = serverIdentity;
            this._listener = new TcpListener(listenAddress ?? IPAddress.Any, listenPort);

            // Setup multicast UDP for local peer discovery
            this._peerDiscovery = new TcpPeerDiscoveryChannel(serverIdentity, multicastGroupAddress ?? IPAddress.Parse("224.0.23.191"), multicastReceivePort);
        }

        /// <inheritdoc />
        public ReadOnlyCollection<IClientChannel> Clients => new ReadOnlyCollection<IClientChannel>(this._clients.Select(c => (IClientChannel)c).ToList());

        /// <inheritdoc />
        public bool Passive { get; set; }

        /// <inheritdoc />
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Logger.Info($"Listening for peers on {((IPEndPoint)this._listener.LocalEndpoint).Address}:{((IPEndPoint)this._listener.LocalEndpoint).Port}");
            try
            {
                this._listener.Start();
            }
            catch (SocketException sex)
            {
                Logger.Error($"Error when starting the peer listener: {sex.ErrorCode}: {sex.Message}", sex);
                return;
            }

            // Setup and start incoming acceptor
            this._acceptTask = new Task(async () =>
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var tcpClient = await this._listener.AcceptTcpClientAsync();
                        Debug.Assert(tcpClient != null, "tcpClient != null");
                        Logger.Verbose($"Connection from {((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address}:{((IPEndPoint)tcpClient.Client.RemoteEndPoint).Port} to {((IPEndPoint)tcpClient.Client.LocalEndPoint).Address}:{((IPEndPoint)tcpClient.Client.LocalEndPoint).Port}");

                        this._clients.Add(new TcpClientChannel(this._serverIdentity, tcpClient, cancellationToken));
                    }
                }
                finally
                {
                    Logger.Warn("Shutting down peer connection accept task");
                }
            });
            this._acceptTask.Start();

            // Setup and start multicast receiver
            await this._peerDiscovery.StartAsync(this.Passive, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IClientChannel> ConnectToPeerAsync(dynamic addressInformation, CancellationToken cancellationToken)
        {
            if (addressInformation == null)
            {
                throw new ArgumentNullException(nameof(addressInformation));
            }

            IPAddress address = addressInformation.address;
            int port = addressInformation.port;

            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(address, port);
            return new TcpClientChannel(this._serverIdentity, tcpClient, cancellationToken);
        }

        /// <inheritdoc />
        public IEnumerable<Tuple<IPAddress, int>> DiscoverPotentialPeers(CancellationToken cancellationToken)
        {
            yield break;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (!this._disposed)
            {
                // Dispose managed resources.
                this._listener.Stop();
                this._acceptTask?.Dispose();
                this._peerDiscovery.Dispose();

                // There are no unmanaged resources to release, but
                // if we add them, they need to be released here.
            }

            this._disposed = true;
        }
    }
}
