﻿// --------------------------------------------------------------------------------------------------------------------
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
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using Newtonsoft.Json;

    /// <summary>
    /// A server capable of listening for <see cref="Client"/> connections from other nodes in the network
    /// </summary>
    public class Server : IDisposable
    {
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
        /// Creates a new instance of a server
        /// </summary>
        /// <param name="passive">A value indicating whether the server channel will not broadcast its presence, but will listen for other nodes only</param>
        /// <param name="serverNodeIdentityFactory">A function that returns a tuple of a <see cref="ServerNodeIdentity"/> and the private key of that identity as a byte array</param>
        /// <param name="serverChannelFactory">A function that returns a <see cref="IServerChannel"/> for listening for incoming connections</param>
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
                if (!isoStore.FileExists("node.identity"))
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

                    using (var isoStream = new IsolatedStorageFileStream("node.identity", FileMode.CreateNew, isoStore))
                    using (var sw = new StreamWriter(isoStream))
                    {
                        sw.Write(JsonConvert.SerializeObject(this._serverNodeIdentityAndKey));
                        sw.Close();
                    }
                }

                // Store the newly-created identity in isolated storage so we can quickly retrieve it again
                using (var isoStream = new IsolatedStorageFileStream("node.identity", FileMode.Open, isoStore))
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
        /// Gets or sets a value indicating whether the server channel will not broadcast its presence, but will listen for other nodes only.
        /// </summary>
        public bool Passive { get; set; }

        /// <summary>
        /// Places the server into a state where it listens for new connections
        /// </summary>
        /// <returns>A task that can be awaited while the operation completes</returns>
        [NotNull]
        public async Task StartAsync()
        {
            if (this._disposed)
            {
                throw new ObjectDisposedException("Server");
            }

            this._serverChannel.Passive = this.Passive;
            await this._serverChannel.StartAsync(this._acceptCancellationTokenSource.Token);
        }

        /// <summary>
        /// Initiates a connection from this server to a peer node
        /// </summary>
        /// <param name="addressInformation">The address information for how to connect to the peer, as interpreted by the <see cref="IServerChannel"/> implementation</param>
        /// <returns>A task that can be awaited while the operation completes</returns>
        [NotNull]
        public async Task ConnectToPeerAsync([NotNull] object addressInformation)
        {
            if (addressInformation == null)
            {
                throw new ArgumentNullException(nameof(addressInformation));
            }

            if (this._disposed)
            {
                throw new ObjectDisposedException("Server");
            }

            await this._serverChannel.ConnectToPeerAsync(addressInformation, this._acceptCancellationTokenSource.Token);
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
    }
}
