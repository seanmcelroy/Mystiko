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
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using log4net;

    using Messages;

    using Mystiko.Cryptography;

    /// <summary>
    /// A network channel for communicating with clients over TCP/IP
    /// </summary>
    public class TcpClientChannel : IClientChannel
    {
        private static readonly byte[] MessageHeader = { 0x4D, 0x59, 0x53, 0x54, 0x49, 0x4B, 0x4F, 0x0A };
        private static readonly byte[] MessageCaboose = { 0x0C, 0xAB, 0x00, 0x5E, 0xFF, 0xFF, 0xFF, 0xFF };

        /// <summary>
        /// The logging implementation for recording the activities that occur in the methods of this class
        /// </summary>
        [NotNull]
        // ReSharper disable once AssignNullToNotNullAttribute
        private static readonly ILog Logger = LogManager.GetLogger(typeof(TcpClientChannel));
        
        [NotNull]
        private readonly TcpClient _client;

        [NotNull]
        private readonly Task _receiveTask;

        [NotNull]
        private readonly Task _parseTask;

        [NotNull]
        private byte[] _buffer = new byte[0];

        [NotNull]
        private readonly object _bufferLock = new object();

        private int _readPosition;
        
        /// <summary>
        /// The message handlers for this channel
        /// </summary>
        [NotNull]
        private readonly List<Action<IMessage>> _messageHandlers = new List<Action<IMessage>>();

        /// <inheritdoc />
        public IPEndPoint RemoteEndpoint => this._client.Connected ? (IPEndPoint)this._client.Client?.RemoteEndPoint : null;

        /// <summary>
        /// Creates a new client channel
        /// </summary>
        /// <param name="serverIdentity">The identity of our server node</param>
        /// <param name="client">The client to which we are connecting</param>
        /// <param name="serverCancellationToken"></param>
        public TcpClientChannel([NotNull] ServerNodeIdentity serverIdentity, [NotNull] TcpClient client, CancellationToken serverCancellationToken = default(CancellationToken))
        {
            if (serverIdentity == null)
            {
                throw new ArgumentNullException(nameof(serverIdentity));
            }

            this._client = client ?? throw new ArgumentNullException(nameof(client));

            // Setup receiver task
            this._receiveTask = new Task(async () =>
            {
                var localEndpoint = (IPEndPoint)this._client.Client.LocalEndPoint;
                Debug.Assert(localEndpoint != null, "localEndpoint != null");
                Logger.Verbose($"{serverIdentity.GetCompositeHash().Substring(3, 8)}: Starting receiver loop for {localEndpoint.Address}:{localEndpoint.Port}");
                try
                {
                    var stream = this._client.GetStream();
                    while (!serverCancellationToken.IsCancellationRequested && stream.CanRead)
                    {
                        try
                        {
                            Debug.Assert(stream != null, "stream != null");
                            var readBytes = new byte[32768];
                            var bytesRead = await stream.ReadAsync(readBytes, 0, readBytes.Length, serverCancellationToken);

                            // There might be more data, so store the data received so far.
                            if (bytesRead > 0)
                            {
                                lock (this._bufferLock)
                                {
                                    var rpos = this._readPosition;
                                    var blen = this._buffer.Length;
                                    var newBuffer = new byte[blen + bytesRead - rpos];
                                    Buffer.BlockCopy(this._buffer, rpos, newBuffer, 0, blen - rpos);
                                    Buffer.BlockCopy(readBytes, 0, newBuffer, blen, bytesRead);
                                    Debug.Assert(this._buffer.Length == 0 || newBuffer[0] == this._buffer[rpos]);
                                    Debug.Assert(newBuffer[newBuffer.Length - 1] == readBytes[bytesRead - 1]);
                                    this._buffer = newBuffer;
                                    this._readPosition = 0;
                                }
                            }
                            else if (this._readPosition == this._buffer.Length - 1)
                            {
                                lock (this._bufferLock)
                                {
                                    if (this._readPosition == this._buffer.Length - 1)
                                    {
                                        this._buffer = new byte[0];
                                        this._readPosition = 0;
                                    }
                                }
                            }
                        }
                        catch (ObjectDisposedException)
                        {
                            return;
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"{serverIdentity.GetCompositeHash().Substring(3, 8)}: Exception caught in receiver task: {ex.Message}", ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"{serverIdentity.GetCompositeHash().Substring(3, 8)}: Exception caught in receiver task: {ex.Message}", ex);
                }
            });

            // Setup parser task
            this._parseTask = new Task(() =>
            {
                while (!serverCancellationToken.IsCancellationRequested)
                {
                    // The minimum message is 24 bytes (header, message type, qwordcount, caboose)
                    MessageType messageType;
                    ulong qwordCount;
                    if (this._buffer.Length - this._readPosition < 24)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    lock (this._bufferLock)
                    {
                        // Find header
                        var potentialHeader = new byte[8];
                        Buffer.BlockCopy(this._buffer, this._readPosition, potentialHeader, 0, 8);
                        this._readPosition += 8;
                        if (!potentialHeader.SequenceEqual(MessageHeader))
                        {
                            Logger.Warn("{serverIdentity.GetCompositeHash().Substring(3, 8)}: Expected header, but magic value not found");
                            continue;
                        }

                        // Find message type
                        var messageTypeByte = this._buffer[this._readPosition];
                        this._readPosition++;
                        messageType = MessageType.Unknown;
                        if (!Enum.IsDefined(typeof(MessageType), messageTypeByte))
                        {
                            Logger.Warn($"{serverIdentity.GetCompositeHash().Substring(3, 8)}: Received unknown message type {messageTypeByte}");
                        }
                        else
                        {
                            messageType = (MessageType)messageTypeByte;
                        }

                        // Get message length
                        var qwordCountBytes = new byte[7];
                        Buffer.BlockCopy(this._buffer, this._readPosition, qwordCountBytes, 0, 7);
                        this._readPosition += 7;
                        qwordCount = BitConverter.ToUInt64(qwordCountBytes.Append((byte)0).ToArray(), 0);
                    }

                    // Do we have the complete message?
                    var payloadByteCount = qwordCount * 8;
                    while (!serverCancellationToken.IsCancellationRequested && (ulong)(this._buffer.Length - this._readPosition + 1) < payloadByteCount)
                    {
                        // Nope, wait a bit.
                        Thread.Sleep(1000);
                    }

                    byte[] payloadBytes;
                    lock (this._bufferLock)
                    {
                        // Get payload
                        payloadBytes = new byte[qwordCount * 8];
                        Buffer.BlockCopy(this._buffer, this._readPosition, payloadBytes, 0, payloadBytes.Length);
                        this._readPosition += payloadBytes.Length;

                        // Find header
                        var potentialCaboose = new byte[8];
                        Buffer.BlockCopy(this._buffer, this._readPosition, potentialCaboose, 0, 8);
                        this._readPosition += 8;
                        if (!potentialCaboose.SequenceEqual(MessageCaboose))
                        {
                            Logger.Warn($"{serverIdentity.GetCompositeHash().Substring(3, 8)}: Expected caboose, but magic value not found");
                            continue;
                        }
                    }

                    Logger.Verbose($"{serverIdentity.GetCompositeHash().Substring(3, 8)}: Received {messageType}");

                    switch (messageType)
                    {
                        case MessageType.NodeHello:
                            var recvHello = NodeHello.CreateFromPayload(payloadBytes);

                            // Validate proof of work
                            byte targetDifficulty = 3;
                            var validatedIdentityResult = HashUtility.ValidateIdentity(recvHello.DateEpoch, recvHello.PublicKeyX, recvHello.PublicKeyY, recvHello.Nonce, targetDifficulty);
                            if (!validatedIdentityResult.DifficultyValidated)
                            {
                                Logger.Warn($"{serverIdentity.GetCompositeHash().Substring(3, 8)}: Does not meet target of {targetDifficulty}, only met {validatedIdentityResult.DifficultyProvided}.  Declining.");
                                this.Send(new NodeDecline
                                {
                                    DeclineReason = NodeDecline.NodeDeclineReasonCode.Untrusted,
                                    Remediation = NodeDecline.NodeDeclineRemediationCode.Rekey
                                });
                            }
                            
                            Logger.Warn($"{serverIdentity.GetCompositeHash().Substring(3, 8)}: Nothing is wrong with the node, but I'm telling them to scram anyway.");
                            this.Send(new NodeDecline
                                      {
                                          DeclineReason = NodeDecline.NodeDeclineReasonCode.Unwilling,
                                          Remediation = NodeDecline.NodeDeclineRemediationCode.Scram
                                      });
                            this._receiveTask.Wait(1);
                            this._parseTask.Wait(1);
                            this._client.Dispose();
                            break;
                        case MessageType.NodeDecline:
                            var recvNodeDecline = new NodeDecline();
                            recvNodeDecline.FromPayload(payloadBytes);
                            Logger.Warn($"{serverIdentity.GetCompositeHash().Substring(3, 8)}: Received NodeDecline: Reason={Enum.GetName(typeof(NodeDecline.NodeDeclineReasonCode), recvNodeDecline.DeclineReason)}, Remediation={Enum.GetName(typeof(NodeDecline.NodeDeclineRemediationCode), recvNodeDecline.Remediation)}");
                            break;
                        default:
                            Logger.Warn($"{serverIdentity.GetCompositeHash().Substring(3, 8)}: Received unhandled message type {messageType}");
                            continue;
                    }
                }
            });

            this._receiveTask.Start();
            this._parseTask.Start();
        }

        /// <inheritdoc />
        public void RegisterHandler(Action<IMessage> messageHandler)
        {
            if (messageHandler == null)
            {
                throw new ArgumentNullException(nameof(messageHandler));
            }

            this._messageHandlers.Add(messageHandler);
        }

        /// <inheritdoc />
        public void Send(IMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            this._client.Client?.Send(message.ToMessage());
        }
    }
}
