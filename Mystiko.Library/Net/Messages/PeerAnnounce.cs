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

    using JetBrains.Annotations;

    /// <summary>
    /// A message broadcast over multicast networks to announce the presence of another peer
    /// </summary>
    [PublicAPI]
    public class PeerAnnounce : IMessage
    {
        /// <inheritdoc />
        public MessageType MessageType => MessageType.PeerAnnounce;

        /// <summary>
        /// Gets or sets the version of the peer network protocol used by the node
        /// </summary>
        public int? PeerNetworkingProtocolVersion { get; set; }

        /// <summary>
        /// Gets or sets the public IP address of this peer node
        /// </summary>
        public IPAddress PublicIPAddress { get; set; }

        /// <summary>
        /// Gets or sets the date the node keys were created
        /// </summary>
        public uint? DateEpoch { get; set; }

        /// <summary>
        /// Gets or sets the value of the public key X-value for this identity
        /// </summary>
        [CanBeNull]
        public byte[] PublicKeyX { get; set; }

        /// <summary>
        /// Gets or sets the value of the public key Y-value for this identity
        /// </summary>
        [CanBeNull]
        public byte[] PublicKeyY { get; set; }

        /// <summary>
        /// Gets or sets the nonce applied to the epoch and public keys of the node, proving
        /// as a proof of work
        /// </summary>
        public ulong? Nonce { get; set; }

        /// <inheritdoc />
        public byte[] ToPayload()
        {
            if (!this.PeerNetworkingProtocolVersion.HasValue || this.PeerNetworkingProtocolVersion < 1)
            {
                throw new InvalidOperationException("PeerNetworkingProtocolVersion is less than 0");
            }

            if (this.PublicIPAddress == null)
            {
                throw new InvalidOperationException("PublicIPAddress is not set");
            }

            if (!this.DateEpoch.HasValue || this.DateEpoch < 1)
            {
                throw new InvalidOperationException("DateEpoch is not set");
            }

            if (this.PublicKeyX == null)
            {
                throw new InvalidOperationException("PublicKeyX is not set");
            }

            if (this.PublicKeyY == null)
            {
                throw new InvalidOperationException("PublicKeyY is not set");
            }

            if (this.Nonce == null)
            {
                throw new InvalidOperationException("Nonce is not set");
            }

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write((byte)this.MessageType);
                bw.Write(this.PeerNetworkingProtocolVersion.Value);
                bw.Write(this.PublicIPAddress == null ? 0 : BitConverter.ToInt32(this.PublicIPAddress.GetAddressBytes(), 0));
                bw.Write(this.DateEpoch.Value);
                bw.Write(this.PublicKeyX, 0, 32);
                bw.Write(this.PublicKeyY, 0, 32);
                bw.Write(this.Nonce.Value);
                return ms.ToArray();
            }
        }

        /// <inheritdoc />
        public void FromPayload(byte[] payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            if (payload.Length < 1)
            {
                throw new ArgumentException("Payload less than one byte in length", nameof(payload));
            }

            using (var ms = new MemoryStream(payload))
            using (var br = new BinaryReader(ms))
            {
                var messageType = br.ReadByte();
                Debug.Assert((byte)MessageType.PeerAnnounce == messageType, "Message is parsed as wrong type");
                this.PeerNetworkingProtocolVersion = br.ReadInt32();
                this.PublicIPAddress = new IPAddress(BitConverter.GetBytes(br.ReadInt32()));
                this.DateEpoch = br.ReadUInt32();
                this.PublicKeyX = br.ReadBytes(32);
                this.PublicKeyY = br.ReadBytes(32);
                this.Nonce = br.ReadUInt64();
            }
        }
    }
}
