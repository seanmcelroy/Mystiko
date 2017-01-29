// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NodeHello.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   The first message a node sends to another when connecting to it
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Net.Messages
{
    using System;
    using System.Diagnostics;
    using System.IO;

    using JetBrains.Annotations;

    /// <summary>
    /// The first message a node sends to another when connecting to it
    /// </summary>
    public class NodeHello : IMessage
    {
        /// <inheritdoc />
        public MessageType MessageType => MessageType.NodeHello;

        /// <summary>
        /// Gets or sets the date the node keys were created
        /// </summary>
        public uint DateEpoch { get; set; }

        /// <summary>
        /// Gets or sets the value of the public key X-value for this identity
        /// </summary>
        [NotNull]
        public byte[] PublicKeyX { get; set; }

        /// <summary>
        /// Gets or sets the base-64 encoded value of the public key X-value for this identity
        /// </summary>
        [NotNull]
        public string PublicKeyXBase64
        {
            get
            {
                return Convert.ToBase64String(this.PublicKeyX);
            }

            set
            {
                this.PublicKeyX = Convert.FromBase64String(value);
            }
        }

        /// <summary>
        /// Gets or sets the value of the public key Y-value for this identity
        /// </summary>
        [NotNull]
        public byte[] PublicKeyY { get; set; }

        /// <summary>
        /// Gets or sets the base-64 encoded value of the public key Y-value for this identity
        /// </summary>
        [NotNull]
        public string PublicKeyYBase64
        {
            get
            {
                return Convert.ToBase64String(this.PublicKeyY);
            }

            set
            {
                this.PublicKeyY = Convert.FromBase64String(value);
            }
        }

        /// <summary>
        /// Gets or sets the nonce applied to the epoch and public keys of the node, proving
        /// as a proof of work
        /// </summary>
        public ulong Nonce { get; set; }

        /// <inheritdoc />
        public byte[] ToPayload()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write((byte)this.MessageType);
                bw.Write(this.DateEpoch);
                bw.Write(this.PublicKeyXBase64);
                bw.Write(this.PublicKeyYBase64);
                bw.Write(this.Nonce);

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
                Debug.Assert(MessageType.NodeHello.Equals(messageType), "Message is parsed as wrong type");
                this.DateEpoch = br.ReadUInt32();
                this.PublicKeyXBase64 = br.ReadString();
                this.PublicKeyYBase64 = br.ReadString();
                this.Nonce = br.ReadUInt64();
            }
        }
    }
}
