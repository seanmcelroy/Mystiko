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
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using log4net;

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
        private readonly StringBuilder _multicastReceiveQueue = new StringBuilder();

        /// <summary>
        /// A transient threading lock object for handling incoming peer discovery broadcast traffic
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

        /// <inheritdoc />
        public async Task StartAsync(CancellationToken cancellationToken)
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
                        var x = await this._multicastUdpClient.ReceiveAsync();
                        if (x.Buffer.Length > 0)
                        {
                            var results = Encoding.UTF8.GetString(x.Buffer);
                            lock (this._multicastReceiveQueueLock)
                            {
                                this._multicastReceiveQueue.Append(results);
                                var split = this._multicastReceiveQueue.ToString().Split('\0');
                                if (split.Length > 1)
                                {
                                    this._multicastReceiveQueue.Clear();
                                    foreach (var s in split.Skip(1))
                                        this._multicastReceiveQueue.AppendFormat("{0}\0", s);

                                    if (this._multicastReceiveQueue.Length > 1)
                                        this._multicastReceiveQueue.Remove(this._multicastReceiveQueue.Length - 2, 1);
                                    else
                                        this._multicastReceiveQueue.Clear();

                                    var receivedMessage = split[0];
                                    throw new NotImplementedException($"What do I do with this? {receivedMessage}");
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
                    await this.SendAsync("HEY\0");

                    Thread.Sleep(5000);
                }
            });
            this._multicastBroadcastTask.Start();
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
        /// Sends a multicast message to those who may be listening for peer broadcast messages
        /// </summary>
        /// <param name="message">The message to send</param>
        public void Send([NotNull] string message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var endpoint = new IPEndPoint(this._multicastGroupAddress, this._multicastReceivePort);
            var bytes = Encoding.UTF8.GetBytes(message);
            this._multicastUdpClient.Send(bytes, bytes.Length, endpoint);
        }

        /// <summary>
        /// Sends a multicast message to those who may be listening for peer broadcast messages
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <returns>A task that can be awaited while the operation completes</returns>
        [NotNull]
        public async Task SendAsync([NotNull] string message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var endpoint = new IPEndPoint(this._multicastGroupAddress, this._multicastReceivePort);
            var bytes = Encoding.UTF8.GetBytes(message);
            await this._multicastUdpClient.SendAsync(bytes, bytes.Length, endpoint);
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
    }
}