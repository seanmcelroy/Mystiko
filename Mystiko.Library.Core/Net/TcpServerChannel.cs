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
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    using log4net;

    using Mystiko.Net.Messages;

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
        /// Gets or sets whether or not to supress casual INFO logging of the methods of this class
        /// </summary>
        public bool DisableLogging { get; set; }

        /// <summary>
        /// The identity of the server node
        /// </summary>
        private readonly ServerNodeIdentity _serverIdentity;

        /// <summary>
        /// The network listener for incoming peer connections
        /// </summary>
        private readonly TcpListener _listener;

        /// <summary>
        /// The list of connected client peers
        /// </summary>
        private readonly ConcurrentBag<IClientChannel> _clients = [];

        private readonly TcpPeerDiscoveryChannel _peerDiscovery;

        /// <summary>
        /// The task that listens for incoming peer connections and accepts them into the list of clients
        /// </summary>
        private Task? _acceptTask;
        
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
            ServerNodeIdentity serverIdentity, 
            IPAddress? listenAddress = null, 
            int listenPort = 5109,
            IPAddress? multicastGroupAddress = null,
            int multicastReceivePort = 5110)
        {
            _serverIdentity = serverIdentity ?? throw new ArgumentNullException(nameof(serverIdentity));
            _listener = new TcpListener(listenAddress ?? IPAddress.Any, listenPort);

            // Setup multicast UDP for local peer discovery
            _peerDiscovery = new TcpPeerDiscoveryChannel(serverIdentity, multicastGroupAddress ?? IPAddress.Parse("224.0.23.191"), multicastReceivePort);
            _peerDiscovery.RegisterPeerDiscoveryHandler(async dp =>
                {
                    // We found a peer!
                    Debug.Assert(dp != null, "dp != null");
                    if (!DisableLogging)
                    {
                        Logger.Verbose($"{_serverIdentity.GetCompositeHash().Substring(3, 8)}: Peer discovered {dp.NodeIdentity.GetCompositeHash().Substring(3, 8)} at {dp.DiscoveryEndpoint.Address}:{dp.DiscoveryEndpoint.Port}");
                    }

                    // Sleep between 1 and 10 seconds for variability if two nodes start at the same time
                    Thread.Sleep(new Random(Environment.TickCount).Next(1000, 10000));

                    if (!_clients.Any(existing => existing?.RemoteEndpoint != null && existing.RemoteEndpoint.Equals(dp.DiscoveryEndpoint)))
                    {
                        var client = await ConnectToPeerAsync(dp.DiscoveryEndpoint);
                        _clients.Add(client);
                    }
                });
        }

        /// <inheritdoc />
        public ReadOnlyCollection<IClientChannel> Clients => new(_clients.ToList());

        /// <inheritdoc />
        public bool Passive { get; set; }

        /// <inheritdoc />
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (!DisableLogging)
            {
                Logger.Info($"{_serverIdentity.GetCompositeHash().Substring(3, 8)}: Listening for peers on {((IPEndPoint)_listener.LocalEndpoint).Address}:{((IPEndPoint)_listener.LocalEndpoint).Port}");
            }

            try
            {
                _listener.Start();
            }
            catch (SocketException sex)
            {
                Logger.Error($"{_serverIdentity.GetCompositeHash().Substring(3, 8)}: Error when starting the peer listener: {sex.SocketErrorCode}: {sex.Message}", sex);
                return;
            }

            // Setup and start incoming acceptor
            _acceptTask = new Task(async () =>
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var tcpClient = await _listener.AcceptTcpClientAsync();
                        Debug.Assert(tcpClient != null, "tcpClient != null");
                        if (!DisableLogging)
                        {
                            Logger.Verbose($"{_serverIdentity.GetCompositeHash().Substring(3, 8)}: Connection from {((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address}:{((IPEndPoint)tcpClient.Client.RemoteEndPoint).Port} to {((IPEndPoint)tcpClient.Client.LocalEndPoint).Address}:{((IPEndPoint)tcpClient.Client.LocalEndPoint).Port}");
                        }

                        _clients.Add(new TcpClientChannel(_serverIdentity, tcpClient, cancellationToken));
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Happens on shutdown, ignore.
                }
                finally
                {
                    Logger.Warn($"{_serverIdentity.GetCompositeHash().Substring(3, 8)}: Shutting down peer connection accept task");
                }
            });
            _acceptTask.Start();

            // Setup and start multicast receiver
            _peerDiscovery.DisableLogging = DisableLogging;
            await _peerDiscovery.StartAsync((ushort)((IPEndPoint)_listener.LocalEndpoint).Port, Passive, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IClientChannel?> ConnectToPeerAsync(IPEndPoint endpoint, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(endpoint);

            var tcpClient = new TcpClient();
            if (!DisableLogging)
            {
                Logger.Verbose($"{_serverIdentity.GetCompositeHash().Substring(3, 8)}: Connecting to peer {endpoint.Address}:{endpoint.Port}...");
            }

            try
            {
                await tcpClient.ConnectAsync(endpoint.Address, endpoint.Port);
                if (!DisableLogging)
                {
                    Logger.Verbose($"{_serverIdentity.GetCompositeHash().Substring(3, 8)}: Connected to peer {endpoint.Address}:{endpoint.Port}");
                }
                var channel = new TcpClientChannel(_serverIdentity, tcpClient, cancellationToken);

                // We just started.  Send the hello announcement
                Logger.Verbose($"{_serverIdentity.GetCompositeHash().Substring(3, 8)}: Sending NodeHello to peer {channel.RemoteEndpoint.Address}:{channel.RemoteEndpoint.Port}");
                channel.Send(new NodeHello(_serverIdentity.DateEpoch, _serverIdentity.PublicKeyX, _serverIdentity.PublicKeyY, _serverIdentity.Nonce));
                return channel;
            }
            catch (SocketException sex)
            {
                if (!DisableLogging)
                {
                    Logger.Verbose($"{_serverIdentity.GetCompositeHash().Substring(3, 8)}: Unable to connect to {endpoint.Address}:{endpoint.Port}: {sex.Message} ({sex.SocketErrorCode})");
                }
                return null;
            }
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
            if (!_disposed)
            {
                // Dispose managed resources.
                _listener.Stop();
                _peerDiscovery.Dispose();

                // There are no unmanaged resources to release, but
                // if we add them, they need to be released here.
            }

            _disposed = true;
        }
    }
}
