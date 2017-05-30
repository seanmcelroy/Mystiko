// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TcpPeerDiscoveryChannel.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   An out-of-band channel for transmitting and receiving information about peer discovery information
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Net
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;

    using Cryptography;

    using JetBrains.Annotations;

    using log4net;

    using Messages;

    /// <summary>
    /// An out-of-band channel for transmitting and receiving information about peer discovery information
    /// </summary>
    internal class TcpPeerDiscoveryChannel
    {
        /// <summary>
        /// The logging implementation for recording the activities that occur in the methods of this class
        /// </summary>
        [NotNull]
        // ReSharper disable once AssignNullToNotNullAttribute
        private static readonly ILog Logger = LogManager.GetLogger(typeof(TcpPeerDiscoveryChannel));

        /// <summary>
        /// Gets or sets whether or not to supress casual INFO logging of the methods of this class
        /// </summary>
        public bool DisableLogging { get; set; }

        /// <summary>
        /// The identity of the server node
        /// </summary>
        [NotNull]
        private readonly ServerNodeIdentity _serverIdentity;

        /// <summary>
        /// The IP multicast address used for local peer discovery broadcasts.  By default, this is 224.0.23.191
        /// </summary>
        [NotNull]
        private readonly IPAddress _multicastGroupAddress;

        /// <summary>
        /// The port used for local peer discovery broadcasts.  By default this is 5110
        /// </summary>
        private readonly int _multicastReceivePort;

        /// <summary>
        /// The network client for sending and receiving local peer discovery broadcasts
        /// </summary>
        [NotNull]
        private readonly UdpClient _multicastUdpClient;

        /// <summary>
        /// The temporary storage queue of incoming peer discovery broadcast traffic
        /// </summary>
        [NotNull]
        private readonly Queue<byte> _multicastReceiveQueue = new Queue<byte>();

        /// <summary>
        /// The temporary storage queue of incoming peer discovery broadcast traffic
        /// </summary>
        [NotNull]
        private readonly object _multicastReceiveQueueLock = new object();

        /// <summary>
        /// The task that listens for peer discovery broadcast announcements and remembers them for future peer connectivity
        /// </summary>
        [CanBeNull]
        private Task _multicastReceiveTask;

        /// <summary>
        /// The task that broadcasts peer discovery broadcast about this node to other peers that may be listening on the multicast group
        /// </summary>
        [CanBeNull]
        private Task _multicastBroadcastTask;

        /// <summary>
        /// A value indicating whether this object has been disposed
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// A value indicating how many seconds since a message was last observed from any other client.
        /// This is used to set exponential backoff for broadcasting my identity
        /// </summary>
        private int _exponentialBackOffTick = 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="TcpPeerDiscoveryChannel"/> class. 
        /// </summary>
        /// <param name="serverIdentity">
        /// The identity of the server node
        /// </param>
        /// <param name="multicastGroupAddress">
        /// The IP multicast address used for local peer discovery broadcasts.  By default, this is 224.0.23.191
        /// </param>
        /// <param name="multicastReceivePort">
        /// The port used for local peer discovery broadcasts.  By default this is 5110
        /// </param>
        public TcpPeerDiscoveryChannel(
            [NotNull] ServerNodeIdentity serverIdentity,
            [CanBeNull] IPAddress multicastGroupAddress = null,
            int multicastReceivePort = 5110)
        {
            this._serverIdentity = serverIdentity ?? throw new ArgumentNullException(nameof(serverIdentity));

            // Setup multicast UDP for local peer discovery
            this._multicastGroupAddress = multicastGroupAddress ?? IPAddress.Parse("224.0.23.191");
            this._multicastReceivePort = multicastReceivePort;
            this._multicastUdpClient = new UdpClient
            {
                ExclusiveAddressUse = false
            };
            this._multicastUdpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            this._multicastUdpClient.Client.Bind(new IPEndPoint(IPAddress.Any, this._multicastReceivePort));
        }

        /// <summary>
        /// Gets the public Internet IP address for this node as it would appear to remote nodes in other networks
        /// </summary>
        [CanBeNull]
        public IPAddress PublicIPAddress { get; private set; }

        /// <summary>
        /// Gets the public TCP port number for this node as it would appear to remote nodes in other networks
        /// </summary>
        [CanBeNull]
        public ushort? PublicPort { get; private set; }

        /// <summary>
        /// Gets a dictionary of peers discovered through this <see cref="TcpPeerDiscoveryChannel"/>, keyed by the composite hash of the
        /// node identity's components
        /// </summary>
        [NotNull]
        private Dictionary<string, DiscoveredPeer> DiscoveredPeers { get; } = new Dictionary<string, DiscoveredPeer>();

        /// <summary>
        /// Gets a list of handlers that receive notifications when peers are discovered
        /// </summary>
        [NotNull]
        private List<Action<DiscoveredPeer>> DiscoveredPeerHandlers { get; } = new List<Action<DiscoveredPeer>>();

        /// <summary>
        /// Starts the peer discovery process
        /// </summary>
        /// <param name="localPort">The TCP port number of this local instance, so if we are broadcasting (that is, if <paramref name="passive"/> is set to false), we can share that with the broadcast receivers</param>
        /// <param name="passive">A value indicating whether this peer should broadcast its presence, rather than simply listening for the presence of other nodes</param>
        /// <param name="cancellationToken">A cancellation token to stop attempting to discover peers</param>
        /// <returns>A task that can be awaited while the operation completes</returns>
        public async Task StartAsync(
            ushort localPort, 
            bool passive = false, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            this.PublicPort = localPort;

            // First, we need to find out our public IP address
            if (!await this.FindPublicIPAddress(cancellationToken))
            {
                Logger.Warn("Unable to locate public IP address this node routes out");
            }

            // Setup and start multicast receiver
            this._multicastReceiveTask = new Task(async () =>
            {
                if (!this.DisableLogging)
                {
                    Logger.Info($"Joining multicast group for peer discovery on {((IPEndPoint)this._multicastUdpClient.Client.LocalEndPoint).Address}:{((IPEndPoint)this._multicastUdpClient.Client.LocalEndPoint).Port}");
                }

                this._multicastUdpClient.JoinMulticastGroup(this._multicastGroupAddress);
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var udpReceiveResult = await this._multicastUdpClient.ReceiveAsync();

                        // Is this a message from myself?
                        if (udpReceiveResult.RemoteEndPoint.Equals(this._multicastUdpClient.Client.LocalEndPoint))
                        {
                            Logger.Warn("Received multicast loopback packet; ignoring.");
                            continue;
                        }

                        if (udpReceiveResult.Buffer.Length > 0)
                        {
                            lock (this._multicastReceiveQueueLock)
                            {
                                foreach (var b in udpReceiveResult.Buffer)
                                {
                                    this._multicastReceiveQueue.Enqueue(b);
                                }

                                var queueBufferCopy = this._multicastReceiveQueue.ToArray();
                                if (queueBufferCopy.Length >= 5)
                                {
                                    // Read 4 bytes for payload length
                                    var lengthNextMessage = BitConverter.ToInt32(queueBufferCopy, 0);

                                    // If we don't have the whole message in our buffer, we need to receive more data.
                                    if (queueBufferCopy.Length < lengthNextMessage + 1)
                                        continue;

                                    // Move a fully-formed message out of our queue and process it
                                    var nextMessageBytes = new byte[lengthNextMessage + 1];

                                    // Ignore the 4 bytes that comprise the length
                                    var c = 0;
                                    while (c < 4)
                                    {
                                        this._multicastReceiveQueue.Dequeue();
                                        c++;
                                    }

                                    c = 0;
                                    while (c < lengthNextMessage + 1)
                                    {
                                        nextMessageBytes[c] = this._multicastReceiveQueue.Dequeue();
                                        c++;
                                    }

                                    // The message should end in the \0 terminator
                                    Debug.Assert(nextMessageBytes[c - 1] == 0, "Message does not end in NUL terminator");

                                    switch ((MessageType)nextMessageBytes[0])
                                    {
                                        case MessageType.PeerAnnounce:
                                            if (nextMessageBytes.Length == 0)
                                            {
                                                Logger.Error($"Unable to parse peer announcement from {udpReceiveResult.RemoteEndPoint.Address}");
                                                continue;
                                            }

                                            try
                                            {
                                                var peerAnnounce = new PeerAnnounce(nextMessageBytes);
                                                if (this.HandlePeerAnnouncement(udpReceiveResult.RemoteEndPoint, peerAnnounce))
                                                    this._exponentialBackOffTick = 1;
                                            }
                                            catch (Exception ex)
                                            {
                                                Logger.Error($"Unable to parse peer announcement from {udpReceiveResult.RemoteEndPoint.Address}", ex);
                                            }

                                            break;
                                        default:
                                            throw new InvalidOperationException($"Unknown message type {nextMessageBytes[0]}");
                                    }
                                }
                            }
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    lock (this._multicastReceiveQueueLock)
                    {
                        this._multicastReceiveQueue.Clear();
                    }
                }
                catch (TaskCanceledException)
                {
                    lock (this._multicastReceiveQueueLock)
                    {
                        this._multicastReceiveQueue.Clear();
                    }
                }
                finally
                {
                    if (!this.DisableLogging)
                    {
                        Logger.Info($"Dropping multicast group for peer discovery from {((IPEndPoint)this._multicastUdpClient.Client.LocalEndPoint).Address}:{((IPEndPoint)this._multicastUdpClient.Client.LocalEndPoint).Port}");
                    }

                    this._multicastUdpClient.DropMulticastGroup(this._multicastGroupAddress);
                }
            });
            this._multicastReceiveTask.Start();

            this._multicastBroadcastTask = new Task(async () =>
            {
                using (var rng = RandomNumberGenerator.Create())
                {
                    Debug.Assert(rng != null, "rng != null");
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        Debug.Assert(this.PublicIPAddress != null, "this.PublicIPAddress != null");
                        Debug.Assert(this.PublicPort != null, "this.PublicPort != null");
                        Debug.Assert(this.PublicPort > 0, "this.PublicPort > 0");

                        if (!this.DisableLogging)
                        {
                            Logger.Debug($"{this._serverIdentity.GetCompositeHash().Substring(3, 8)}: Sending my peer announcement");
                        }

                        await this.SendAsync(new PeerAnnounce(1, this.PublicIPAddress, this.PublicPort.Value, this._serverIdentity.DateEpoch, this._serverIdentity.PublicKeyX, this._serverIdentity.PublicKeyY, this._serverIdentity.Nonce));

                        this._exponentialBackOffTick++;
                        Thread.Sleep(500 * rng.GetNext(10, this._exponentialBackOffTick + 10)); // At most once every 5 seconds

                    }
                }
            });

            if (!passive)
            {
                Thread.Sleep(5000);
                this._multicastBroadcastTask.Start();
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (!this._disposed)
            {
                // Dispose managed resources.
                this._multicastUdpClient.Dispose();

                // There are no unmanaged resources to release, but
                // if we add them, they need to be released here.
            }

            this._disposed = true;
        }

        /// <summary>
        /// Registers a handler that receives notifications when new peers are discovered
        /// </summary>
        /// <param name="handler">The handler for the newly discovered <see cref="DiscoveredPeer"/></param>
        public void RegisterPeerDiscoveryHandler([NotNull] Action<DiscoveredPeer> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            this.DiscoveredPeerHandlers.Add(handler);
        }

        /// <summary>
        /// Determines the public Internet IP address for this node as it would appear to remote nodes in other networks
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to stop attempting to discover peers</param>
        /// <returns>A value indicating whether an address was located and set in the <see cref="PublicIPAddress"/> property</returns>
        private async Task<bool> FindPublicIPAddress(CancellationToken cancellationToken = default(CancellationToken))
        {
            var publicIp = await NetUtility.FindPublicIPAddressAsync(cancellationToken);
            this.PublicIPAddress = publicIp;
            return publicIp != null;
        }

        /// <summary>
        /// Handle a <see cref="PeerAnnounce"/> message
        /// </summary>
        /// <param name="remoteEndpoint">The remote endpoint that sent the message</param>
        /// <param name="announcement">The message to handle</param>
        /// <returns>A value indicating whether a new peer was actually discovered</returns>
        private bool HandlePeerAnnouncement([NotNull] IPEndPoint remoteEndpoint, [NotNull] PeerAnnounce announcement)
        {
            if (remoteEndpoint == null)
            {
                throw new ArgumentNullException(nameof(remoteEndpoint));
            }

            if (announcement == null)
            {
                throw new ArgumentNullException(nameof(announcement));
            }

            if (!announcement.DateEpoch.HasValue)
            {
                throw new ArgumentException("DateEpoch value not supplied", nameof(announcement));
            }

            if (announcement.PublicKeyX == null)
            {
                throw new ArgumentException("PublicKeyX value not supplied", nameof(announcement));
            }

            if (announcement.PublicKeyY == null)
            {
                throw new ArgumentException("PublicKeyY value not supplied", nameof(announcement));
            }

            if (!announcement.Nonce.HasValue)
            {
                throw new ArgumentException("Nonce value not supplied", nameof(announcement));
            }

            if (announcement.PublicPort == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(announcement), "TCP port number must be greater than 0");
            }

            // Is this my own multicast message?
            if (this._serverIdentity.DateEpoch == announcement.DateEpoch && this._serverIdentity.PublicKeyX.SequenceEqual(announcement.PublicKeyX) && this._serverIdentity.PublicKeyY.SequenceEqual(announcement.PublicKeyY) && this._serverIdentity.Nonce == announcement.Nonce)
            {
                // Message from myself
                return false;
            }

            // Validate the presented identity hashes out
            byte difficultyTarget = 3;
            var result = HashUtility.ValidateIdentity(announcement.DateEpoch.Value, announcement.PublicKeyX, announcement.PublicKeyY, announcement.Nonce.Value, difficultyTarget);
            if (!result.DifficultyValidated)
            {
                Logger.Warn($"{this._serverIdentity.GetCompositeHash().Substring(3, 8)}: Unverifiable hash in announcement from {remoteEndpoint.Address}");
                return false;
            }

            Debug.Assert(result.CompositeHash != null, "result.CompositeHash != null");
            if (this.DiscoveredPeers.ContainsKey(result.CompositeHash))
            {
                return false;
            }

            if (!this.DisableLogging)
            {
                Logger.Debug($"{this._serverIdentity.GetCompositeHash().Substring(3, 8)}: Peer announcement received from: {announcement.PublicIPAddress}(#...{result.CompositeHash.Substring(difficultyTarget, 8)})");
            }

            // FOR LOCAL TESTING ONLY
            var remoteAddress = announcement.PublicIPAddress;
            if (announcement.PublicIPAddress.Equals(this.PublicIPAddress))
            {
                remoteAddress = IPAddress.Loopback;
            }

            var discoveredPeer = new DiscoveredPeer(new ServerNodeIdentity(
                announcement.DateEpoch.Value,
                announcement.PublicKeyX,
                announcement.PublicKeyY,
                announcement.Nonce.Value),
                new IPEndPoint(remoteAddress, announcement.PublicPort));

            this.DiscoveredPeers.Add(result.CompositeHash, discoveredPeer);

            foreach (var handler in this.DiscoveredPeerHandlers)
            {
                Debug.Assert(handler != null, "handler != null");
                handler(discoveredPeer);
            }

            return true;
        }

        /// <summary>
        /// Sends a multicast message to those who may be listening for peer broadcast messages
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <returns>A task that can be awaited while the operation completes</returns>
        [NotNull]
        private async Task SendAsync([NotNull] IMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var endpoint = new IPEndPoint(this._multicastGroupAddress, this._multicastReceivePort);
            var payloadBytes = message.ToMessage();
            if (payloadBytes.Length > 65507)
            {
                throw new InvalidOperationException("Payload is too large; would have caused UDP packet to fragment");
            }

            if (payloadBytes.Length > 8192)
            {
                Logger.Warn($"{this._serverIdentity.GetCompositeHash().Substring(3, 8)}: Sending a large packet across peer channel ({payloadBytes.Length} bytes); may be lost in transmission");
            }

            /* Wire format is:
             * 4 bytes / 32 bit length of payload
             * X bytes / payload(messageType+serializedValues)
             * 1 byte  / NUL, last byte left blank as a separator/sanity check
             */
            var wrappedPayload = new byte[4 + payloadBytes.Length + 1];
            Array.Copy(BitConverter.GetBytes(payloadBytes.Length), wrappedPayload, 4);
            Array.Copy(payloadBytes, 0, wrappedPayload, 4, payloadBytes.Length);

            await this._multicastUdpClient.SendAsync(wrappedPayload, wrappedPayload.Length, endpoint);
        }
    }
}