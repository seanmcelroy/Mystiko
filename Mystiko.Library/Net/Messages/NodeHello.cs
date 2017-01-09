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
    using System.IO;

    using JetBrains.Annotations;

    /// <summary>
    /// The first message a node sends to another when connecting to it
    /// </summary>
    public class NodeHello : IMessage
    {
        /// <summary>
        /// Gets or sets the date the node keys were created
        /// </summary>
        public uint DateEpoch { get; set; }

        /// <summary>
        /// Gets or sets the public key of the node
        /// </summary>
        [NotNull]
        public string PublicKeyXBase64 { get; set; }

        /// <summary>
        /// Gets or sets the public key of the node
        /// </summary>
        [NotNull]
        public string PublicKeyYBase64 { get; set; }

        /// <summary>
        /// Gets or sets the nonce applied to the epoch and public keys of the node, proving
        /// as a proof of work
        /// </summary>
        public long Nonce { get; set; }

        /// <inheritdoc />
        public string ToPayload()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(this.DateEpoch);
                bw.Write(this.PublicKeyXBase64);
                bw.Write(this.PublicKeyYBase64);
                bw.Write(this.Nonce);

                return Convert.ToBase64String(ms.ToArray());
            }
        }

        /// <inheritdoc />
        public void FromPayload(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                throw new ArgumentNullException(nameof(payload));
            }

            using (var ms = new MemoryStream(Convert.FromBase64String(payload)))
            using (var br = new BinaryReader(ms))
            {
                this.DateEpoch = br.ReadUInt32();
                this.PublicKeyXBase64 = br.ReadString();
                this.PublicKeyYBase64 = br.ReadString();
                this.Nonce = br.ReadInt64();
            }
        }
    }
}
