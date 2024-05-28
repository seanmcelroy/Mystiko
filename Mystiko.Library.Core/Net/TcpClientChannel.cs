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
        private static readonly ILog Logger = LogManager.GetLogger(typeof(TcpClientChannel));
        
        private readonly TcpClient _client;

        private readonly Task _receiveTask;

        private readonly Task _parseTask;

        private byte[] _buffer = new byte[0];

        private readonly object _bufferLock = new();

        private int _readPosition;
        
        /// <summary>
        /// The message handlers for this channel
        /// </summary>
        private readonly List<Action<IMessage>> _messageHandlers = [];

        /// <inheritdoc />
        public IPEndPoint? RemoteEndpoint => _client.Connected ? (IPEndPoint?)_client.Client.RemoteEndPoint : null;

        /// <summary>
        /// Creates a new client channel
        /// </summary>
        /// <param name="serverIdentity">The identity of our server node</param>
        /// <param name="client">The client to which we are connecting</param>
        /// <param name="serverCancellationToken"></param>
        public TcpClientChannel(ServerNodeIdentity serverIdentity, TcpClient client, CancellationToken serverCancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(serverIdentity);

            _client = client ?? throw new ArgumentNullException(nameof(client));

            // Setup receiver task
            _receiveTask = new Task(async () =>
            {
                var localEndpoint = (IPEndPoint?)_client.Client.LocalEndPoint;
                Debug.Assert(localEndpoint != null, "localEndpoint != null");
                Logger.Verbose($"{serverIdentity.GetCompositeHash().Substring(3, 8)}: Starting receiver loop for {localEndpoint.Address}:{localEndpoint.Port}");
                try
                {
                    var stream = _client.GetStream();
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
                                lock (_bufferLock)
                                {
                                    var rpos = _readPosition;
                                    var blen = _buffer.Length;
                                    var newBuffer = new byte[blen + bytesRead - rpos];
                                    Buffer.BlockCopy(_buffer, rpos, newBuffer, 0, blen - rpos);
                                    Buffer.BlockCopy(readBytes, 0, newBuffer, blen, bytesRead);
                                    Debug.Assert(_buffer.Length == 0 || newBuffer[0] == _buffer[rpos]);
                                    Debug.Assert(newBuffer[newBuffer.Length - 1] == readBytes[bytesRead - 1]);
                                    _buffer = newBuffer;
                                    _readPosition = 0;
                                }
                            }
                            else if (_readPosition == _buffer.Length - 1)
                            {
                                lock (_bufferLock)
                                {
                                    if (_readPosition == _buffer.Length - 1)
                                    {
                                        _buffer = new byte[0];
                                        _readPosition = 0;
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
            _parseTask = new Task(() =>
            {
                while (!serverCancellationToken.IsCancellationRequested)
                {
                    // The minimum message is 24 bytes (header, message type, qwordcount, caboose)
                    MessageType messageType;
                    ulong qwordCount;
                    if (_buffer.Length - _readPosition < 24)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    lock (_bufferLock)
                    {
                        // Find header
                        var potentialHeader = new byte[8];
                        Buffer.BlockCopy(_buffer, _readPosition, potentialHeader, 0, 8);
                        _readPosition += 8;
                        if (!potentialHeader.SequenceEqual(MessageHeader))
                        {
                            Logger.Warn("{serverIdentity.GetCompositeHash().Substring(3, 8)}: Expected header, but magic value not found");
                            continue;
                        }

                        // Find message type
                        var messageTypeByte = _buffer[_readPosition];
                        _readPosition++;
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
                        Buffer.BlockCopy(_buffer, _readPosition, qwordCountBytes, 0, 7);
                        _readPosition += 7;
                        qwordCount = BitConverter.ToUInt64(qwordCountBytes.Append((byte)0).ToArray(), 0);
                    }

                    // Do we have the complete message?
                    var payloadByteCount = qwordCount * 8;
                    while (!serverCancellationToken.IsCancellationRequested && (ulong)(_buffer.Length - _readPosition + 1) < payloadByteCount)
                    {
                        // Nope, wait a bit.
                        Thread.Sleep(1000);
                    }

                    byte[] payloadBytes;
                    lock (_bufferLock)
                    {
                        // Get payload
                        payloadBytes = new byte[qwordCount * 8];
                        Buffer.BlockCopy(_buffer, _readPosition, payloadBytes, 0, payloadBytes.Length);
                        _readPosition += payloadBytes.Length;

                        // Find header
                        var potentialCaboose = new byte[8];
                        Buffer.BlockCopy(_buffer, _readPosition, potentialCaboose, 0, 8);
                        _readPosition += 8;
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
                                Send(new NodeDecline
                                {
                                    DeclineReason = NodeDecline.NodeDeclineReasonCode.Untrusted,
                                    Remediation = NodeDecline.NodeDeclineRemediationCode.Rekey
                                });
                            }
                            
                            Logger.Warn($"{serverIdentity.GetCompositeHash().Substring(3, 8)}: Nothing is wrong with the node, but I'm telling them to scram anyway.");
                            Send(new NodeDecline
                                      {
                                          DeclineReason = NodeDecline.NodeDeclineReasonCode.Unwilling,
                                          Remediation = NodeDecline.NodeDeclineRemediationCode.Scram
                                      });
                            _receiveTask.Wait(1);
                            _parseTask.Wait(1);
                            _client.Dispose();
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

            _receiveTask.Start();
            _parseTask.Start();
        }

        /// <inheritdoc />
        public void RegisterHandler(Action<IMessage> messageHandler)
        {
            ArgumentNullException.ThrowIfNull(messageHandler);

            _messageHandlers.Add(messageHandler);
        }

        /// <inheritdoc />
        public void Send(IMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);

            _client.Client?.Send(message.ToMessage());
        }
    }
}
