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

    public class Server : IDisposable
    {
        [NotNull]
        private readonly ServerNodeIdentityAndKey _serverNodeIdentityAndKey;

        [NotNull]
        private readonly IServerChannel _serverChannel;

        [NotNull]
        private readonly CancellationTokenSource _acceptCancellationTokenSource = new CancellationTokenSource();

        private bool _disposed;

        public Server(
            [CanBeNull] Func<Tuple<ServerNodeIdentity, byte[]>> serverNodeIdentityFactory = null,
            [CanBeNull] Func<IServerChannel> serverChannelFactory = null)
        {
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

                using (var isoStream = new IsolatedStorageFileStream("node.identity", FileMode.Open, isoStore))
                using (var sr = new StreamReader(isoStream))
                {
                    this._serverNodeIdentityAndKey = JsonConvert.DeserializeObject<ServerNodeIdentityAndKey>(sr.ReadToEnd());
                    isoStream.Close();
                }
            }

            // Create network channel
            var channel = (serverChannelFactory ?? (() => new TcpServerChannel(this._serverNodeIdentityAndKey, IPAddress.Any, 5091))).Invoke();
            if (channel == null)
            {
                throw new ArgumentException("Server channel factory returned null on invocation", nameof(serverChannelFactory));
            }

            Debug.Assert(channel != null, "channel != null");
            this._serverChannel = channel;
        }

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
                if (this._serverChannel is IDisposable)
                {
                    ((IDisposable)this._serverChannel).Dispose();
                }

                // There are no unmanaged resources to release, but
                // if we add them, they need to be released here.
            }

            this._disposed = true;
        }
    }
}
