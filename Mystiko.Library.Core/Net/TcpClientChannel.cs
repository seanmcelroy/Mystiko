// ---------'-----------------------------------------------------------------------------------------------------------
// <copyright file="TcpClientChannel.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   A network channel for communicating with clients over TCP/IP
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Net
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using log4net;
    using log4net.Repository.Hierarchy;

    using Messages;

    /// <summary>
    /// A network channel for communicating with clients over TCP/IP
    /// </summary>
    public class TcpClientChannel : IClientChannel
    {
        /// <summary>
        /// The logging implementation for recording the activities that occur in the methods of this class
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger(typeof(TcpClientChannel));

        /// <summary>
        /// The size of the stream receive buffer
        /// </summary>
        private const int BufferSize = 1048576;

        [NotNull]
        private readonly TcpClient _client;

        private Task _receiveTask;

        [NotNull]
        private readonly byte[] _rawInputBuffer = new byte[BufferSize];

        [NotNull]
        private readonly StringBuilder _builder = new StringBuilder();

        /// <summary>
        /// The message handlers for this channel
        /// </summary>
        [NotNull]
        private readonly List<Action<IMessage>> messageHandlers = new List<Action<IMessage>>();

        public TcpClientChannel([NotNull] ServerNodeIdentity serverIdentity, [NotNull] TcpClient client, CancellationToken serverCancellationToken = default(CancellationToken))
        {
            if (serverIdentity == null)
            {
                throw new ArgumentNullException(nameof(serverIdentity));
            }

            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            this._client = client;

            // Setup receiver task
            this._receiveTask = new Task(async () =>
            {
                Logger.Verbose($"Starting receiver loop for {((IPEndPoint)this._client.Client.LocalEndPoint).Address}:{((IPEndPoint)this._client.Client.LocalEndPoint).Port}");
                var stream = this._client.GetStream();
                while (!serverCancellationToken.IsCancellationRequested)
                {
                    var bytesRead = await stream.ReadAsync(this._rawInputBuffer, 0, 32767, serverCancellationToken);

                    // There might be more data, so store the data received so far.
                    this._builder.Append(Encoding.UTF8.GetString(this._rawInputBuffer, 0, bytesRead));

                    // Not all data received OR no more but not yet ending with the delimiter. Get more.
                    var content = this._builder.ToString();
                    if (bytesRead == BufferSize || !content.EndsWith("\r\n", StringComparison.Ordinal))
                    {
                        // Read some more.
                        continue;
                    }

                    var message = content.Split('\r').FirstOrDefault()?.TrimEnd('\r', '\n');
                    Console.WriteLine($"Message received: {message}");
                }
            });

            this._receiveTask.Start();

            // We just started.  Send the hello announcement
            this.Send(new NodeHello
                          {
                              DateEpoch = serverIdentity.DateEpoch,
                              Nonce = serverIdentity.Nonce,
                              PublicKeyXBase64 = serverIdentity.PublicKeyXBase64,
                              PublicKeyYBase64 = serverIdentity.PublicKeyYBase64
                          });
        }

        /// <inheritdoc />
        public void RegisterHandler(Action<IMessage> messageHandler)
        {
            if (messageHandler == null)
            {
                throw new ArgumentNullException(nameof(messageHandler));
            }

            this.messageHandlers.Add(messageHandler);
        }

        /// <inheritdoc />
        public void Send(IMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            this._client.Client.Send(message.ToPayload());
        }
    }
}
