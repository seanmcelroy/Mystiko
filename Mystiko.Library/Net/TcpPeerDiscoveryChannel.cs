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
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using log4net;

    using Mystiko.Net.Messages;

    /// <summary>
    /// An out-of-band channel for transmitting and receiving information about peer discovery information
    /// </summary>
    internal class TcpPeerDiscoveryChannel
    {
        /// <summary>
        /// The logging implementation for recording the activities that occur in the methods of this class
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger(typeof(TcpPeerDiscoveryChannel));

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
            if (serverIdentity == null)
            {
                throw new ArgumentNullException(nameof(serverIdentity));
            }

            this._serverIdentity = serverIdentity;

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

        public async Task StartAsync(bool passive = false, CancellationToken cancellationToken = default(CancellationToken))
        {
            // First, we need to find out our public IP address
            if (!await this.FindPublicIPAddress(cancellationToken))
            {
                Logger.Warn("Unable to locate public IP address this node routes out");
            }

            // Setup and start multicast receiver
            this._multicastReceiveTask = new Task(async () =>
            {
                Logger.Info($"Joining multicast group for peer discovery on {((IPEndPoint)this._multicastUdpClient.Client.LocalEndPoint).Address}:{((IPEndPoint)this._multicastUdpClient.Client.LocalEndPoint).Port}");
                this._multicastUdpClient.JoinMulticastGroup(this._multicastGroupAddress);
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var udpReceiveResult = await this._multicastUdpClient.ReceiveAsync();

                        // Is this a message from myself?
                        if (udpReceiveResult.RemoteEndPoint.Equals(this._multicastUdpClient.Client.LocalEndPoint))
                        {
                            Logger.Warn("Receipved multicast loopback packet; ignoring.");
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
                                            var peerAnnounce = new PeerAnnounce();
                                            try
                                            {
                                                peerAnnounce.FromPayload(nextMessageBytes);
                                            }
                                            catch (Exception ex)
                                            {
                                                Logger.Error($"Unable to parse peer announcement from {udpReceiveResult.RemoteEndPoint.Address}", ex);
                                                continue;
                                            }

                                            Logger.Debug($"Peer announcement received from: {peerAnnounce.PublicIPAddress}(nonce@{peerAnnounce.Nonce})");
                                            break;
                                        default:
                                            throw new InvalidOperationException($"Unknown message type {nextMessageBytes[0]}");
                                    }
                                }
                            }
                        }
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
                    Logger.Info($"Dropping multicast group for peer discovery from {((IPEndPoint)this._multicastUdpClient.Client.LocalEndPoint).Address}:{((IPEndPoint)this._multicastUdpClient.Client.LocalEndPoint).Port}");
                    this._multicastUdpClient.DropMulticastGroup(this._multicastGroupAddress);
                }
            });
            this._multicastReceiveTask.Start();

            this._multicastBroadcastTask = new Task(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    Logger.Debug("Sending peer announcement");
                    await this.SendAsync(new PeerAnnounce
                                             {
                                                 PeerNetworkingProtocolVersion = 1,
                                                 PublicIPAddress = this.PublicIPAddress,
                                                 DateEpoch = this._serverIdentity.DateEpoch,
                                                 PublicKeyX = this._serverIdentity.PublicKeyX,
                                                 PublicKeyY = this._serverIdentity.PublicKeyY,
                                                 Nonce = this._serverIdentity.Nonce
                                             });

                    Thread.Sleep(3 * 1000);
                }
            });

            if (!passive)
            {
                Thread.Sleep(5000);
                this._multicastBroadcastTask.Start();
            }
        }

        /// <summary>
        /// Determines the public Internet IP address for this node as it would appear to remote nodes in other networks
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to stop attempting to discover peers</param>
        /// <returns>A value indicating whether an address was located and set in the <see cref="PublicIPAddress"/> property</returns>
        public async Task<bool> FindPublicIPAddress(CancellationToken cancellationToken = default(CancellationToken))
        {
            var sources = new[] { @"https://icanhazip.com", @"http://checkip.amazonaws.com", @"http://ipecho.net", @"http://l2.io/ip", @"http://eth0.me" };
            
            foreach (var source in sources)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return false;
                }

                using (var wc = new WebClient())
                {
                    try
                    {
                        Logger.Debug($"Requesting IP address from remote source {source}");
                        Debug.Assert(source != null, "source != null");
                        var myIp = await wc.DownloadStringTaskAsync(source);
                        if (string.IsNullOrWhiteSpace(myIp))
                        {
                            Logger.Warn($"IP lookup source {source} returned an empty response");
                            continue;
                        }

                        IPAddress publicIp;
                        if (!IPAddress.TryParse(myIp.Trim().TrimEnd('\r', '\n'), out publicIp))
                        {
                            Logger.Warn($"IP lookup source {source} returned a value that could not be parsed into an IP address: {myIp}");
                            continue;
                        }

                        Logger.Info($"External IP address determined to be {publicIp} from remote source {source}");
                        this.PublicIPAddress = publicIp;
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"Exception when attempting to gather public IP address from {source}", ex);
                    }
                }
            }

            Logger.Warn($"Unable to find public IP address after querying {sources.Length} sources");
            return false;
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
                this._multicastReceiveTask?.Dispose();
                this._multicastBroadcastTask?.Dispose();

                // There are no unmanaged resources to release, but
                // if we add them, they need to be released here.
            }

            this._disposed = true;
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
            var payloadBytes = message.ToPayload();
            if (payloadBytes.Length > 65507)
            {
                throw new InvalidOperationException("Payload is too large; would have caused UDP packet to fragment");
            }

            if (payloadBytes.Length > 8192)
            {
                Logger.Warn($"Sending a large packet across peer channel ({payloadBytes.Length} bytes); may be lost in transmission");
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