// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PeerAnnounce.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   A message broadcast over multicast networks to announce the presence of another peer
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Net.Messages
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net;

    /// <summary>
    /// A message broadcast over multicast networks to announce the presence of another peer
    /// </summary>
    public class PeerAnnounce : IMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PeerAnnounce"/> class.
        /// </summary>
        /// <param name="peerNetworkingProtocolVersion">The peer networking protocol version.</param>
        /// <param name="publicIpAddress">The public IP address of this peer node</param>
        /// <param name="publicPort">The public TCP port number of this peer node</param>
        /// <param name="dateEpoch">The date the node keys were created</param>
        /// <param name="publicKeyX">The value of the public key X-value for this identity</param>
        /// <param name="publicKeyY">The value of the public key Y-value for this identity</param>
        /// <param name="nonce">The nonce applied to the epoch and public keys of the node, proving
        /// as a proof of work</param>
        public PeerAnnounce(
            int peerNetworkingProtocolVersion,
            IPAddress publicIpAddress,
            ushort publicPort,
            ulong dateEpoch,
            byte[] publicKeyX,
            byte[] publicKeyY,
            ulong nonce)
        {
            ArgumentNullException.ThrowIfNull(publicKeyX);

            if (publicKeyX.Length != 32)
            {
                throw new ArgumentOutOfRangeException(nameof(publicKeyX), "Key not 32 bytes in length");    
            }

            ArgumentNullException.ThrowIfNull(publicKeyY);

            if (publicKeyY.Length != 32)
            {
                throw new ArgumentOutOfRangeException(nameof(publicKeyX), "Key not 32 bytes in length");
            }

            PeerNetworkingProtocolVersion = peerNetworkingProtocolVersion;
            PublicIPAddress = publicIpAddress ?? throw new ArgumentNullException(nameof(publicIpAddress));
            PublicPort = publicPort;
            DateEpoch = dateEpoch;
            PublicKeyX = publicKeyX;
            PublicKeyY = publicKeyY;
            Nonce = nonce;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PeerAnnounce"/> class.
        /// </summary>
        /// <param name="payload">The serialized payload of the record</param>
        /// <exception cref="ArgumentNullException">
        /// Throw if the <paramref name="payload"/> is null.
        /// </exception>
        public PeerAnnounce(byte[] payload)
        {
            ArgumentNullException.ThrowIfNull(payload);

            if (payload.Length < 1)
            {
                throw new ArgumentException("Payload less than one byte in length", nameof(payload));
            }

            using (var ms = new MemoryStream(payload))
            using (var br = new BinaryReader(ms))
            {
                var messageType = br.ReadByte();
                Debug.Assert((byte)MessageType.PeerAnnounce == messageType, "Message is parsed as wrong type");
                PeerNetworkingProtocolVersion = br.ReadInt32();
                PublicIPAddress = new IPAddress(BitConverter.GetBytes(br.ReadInt32()));
                PublicPort = br.ReadUInt16();
                DateEpoch = br.ReadUInt64();
                PublicKeyX = br.ReadBytes(32);
                PublicKeyY = br.ReadBytes(32);
                Nonce = br.ReadUInt64();
            }
        }

        /// <inheritdoc />
        public MessageType MessageType => MessageType.PeerAnnounce;

        /// <summary>
        /// Gets the version of the peer network protocol used by the node
        /// </summary>
        public int? PeerNetworkingProtocolVersion { get; private set; }

        /// <summary>
        /// Gets the public IP address of this peer node
        /// </summary>
        public IPAddress PublicIPAddress { get; private set; }

        /// <summary>
        /// Gets the public TCP port number of this peer node
        /// </summary>
        public ushort PublicPort { get; private set; }

        /// <summary>
        /// Gets or sets the date the node keys were created
        /// </summary>
        public ulong? DateEpoch { get; set; }

        /// <summary>
        /// Gets or sets the value of the public key X-value for this identity
        /// </summary>
        public byte[]? PublicKeyX { get; set; }

        /// <summary>
        /// Gets or sets the value of the public key Y-value for this identity
        /// </summary>
        public byte[]? PublicKeyY { get; set; }

        /// <summary>
        /// Gets or sets the nonce applied to the epoch and public keys of the node, proving
        /// as a proof of work
        /// </summary>
        public ulong? Nonce { get; set; }

        /// <inheritdoc />
        public byte[] ToMessage()
        {
            if (!PeerNetworkingProtocolVersion.HasValue || PeerNetworkingProtocolVersion < 1)
            {
                throw new InvalidOperationException("PeerNetworkingProtocolVersion is less than 0");
            }

            if (PublicIPAddress == null)
            {
                throw new InvalidOperationException("PublicIPAddress is not set");
            }

            if (!DateEpoch.HasValue || DateEpoch < 1)
            {
                throw new InvalidOperationException("DateEpoch is not set");
            }

            if (PublicKeyX == null)
            {
                throw new InvalidOperationException("PublicKeyX is not set");
            }

            if (PublicKeyY == null)
            {
                throw new InvalidOperationException("PublicKeyY is not set");
            }

            if (Nonce == null)
            {
                throw new InvalidOperationException("Nonce is not set");
            }

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write((byte)MessageType);
            bw.Write(PeerNetworkingProtocolVersion.Value);
            bw.Write(BitConverter.ToInt32(PublicIPAddress.GetAddressBytes(), 0));
            bw.Write(PublicPort);
            bw.Write(DateEpoch.Value);
            bw.Write(PublicKeyX, 0, 32);
            bw.Write(PublicKeyY, 0, 32);
            bw.Write(Nonce.Value);
            return ms.ToArray();
        }

        /// <inheritdoc />
        public void FromMessage(byte[] messageBytes)
        {
            ArgumentNullException.ThrowIfNull(messageBytes);

            if (messageBytes.Length < 1)
            {
                throw new ArgumentException("Payload less than one byte in length", nameof(messageBytes));
            }

            using (var ms = new MemoryStream(messageBytes))
            using (var br = new BinaryReader(ms))
            {
                var messageType = br.ReadByte();
                Debug.Assert((byte)MessageType.PeerAnnounce == messageType, "Message is parsed as wrong type");
                PeerNetworkingProtocolVersion = br.ReadInt32();
                PublicIPAddress = new IPAddress(BitConverter.GetBytes(br.ReadInt32()));
                PublicPort = br.ReadUInt16();
                DateEpoch = br.ReadUInt64();
                PublicKeyX = br.ReadBytes(32);
                PublicKeyY = br.ReadBytes(32);
                Nonce = br.ReadUInt64();
            }
        }

        /// <inheritdoc />
        public void FromPayload(byte[] payload)
        {
            // TODO
            throw new NotImplementedException();
        }
    }
}
