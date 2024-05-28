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
    using System.Linq;

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
        public ulong DateEpoch { get; private set; }

        /// <summary>
        /// Gets or sets the value of the public key X-value for this identity
        /// </summary>
        public byte[] PublicKeyX { get; private set; }

        /// <summary>
        /// Gets or sets the value of the public key Y-value for this identity
        /// </summary>
        public byte[] PublicKeyY { get; private set; }

        /// <summary>
        /// Gets or sets the nonce applied to the epoch and public keys of the node, proving
        /// as a proof of work
        /// </summary>
        public ulong Nonce { get; private set; }

        public NodeHello(ulong dateEpoch, byte[] publicKeyX, byte[] publicKeyY, ulong nonce)
        {
            if (dateEpoch < Convert.ToUInt64((new DateTime(2017, 05, 01, 0, 0, 0, DateTimeKind.Utc) - new DateTime(1970, 01, 01, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds))
                throw new ArgumentOutOfRangeException(nameof(dateEpoch), "Date epoch cannot be before 2017-05-01");

            if (dateEpoch > Convert.ToUInt64((DateTime.UtcNow.AddMinutes(10) - new DateTime(1970, 01, 01, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds))
                throw new ArgumentOutOfRangeException(nameof(dateEpoch), "Date epoch cannot be in the future");

            ArgumentNullException.ThrowIfNull(publicKeyX);

            if (publicKeyX.Length != 32)
                throw new ArgumentException("Public key X is not exactly 32 bytes long", nameof(publicKeyX));

            ArgumentNullException.ThrowIfNull(publicKeyY);

            if (publicKeyY.Length != 32)
                throw new ArgumentException("Public key Y is not exactly 32 bytes long", nameof(publicKeyY));

            DateEpoch = dateEpoch;
            PublicKeyX = publicKeyX;
            PublicKeyY = publicKeyY;
            Nonce = nonce;
        }

        private NodeHello()
        {
        }

        public static NodeHello CreateFromPayload(byte[] payload)
        {
            var ret = new NodeHello();
            ret.FromPayload(payload);
            return ret;
        }

        /// <inheritdoc />
        public byte[] ToMessage()
        {
            Debug.Assert(PublicKeyX != null);
            Debug.Assert(PublicKeyX.Length == 32);
            Debug.Assert(PublicKeyY != null);
            Debug.Assert(PublicKeyY.Length == 32);

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write(new byte[] { 0x4D, 0x59, 0x53, 0x54, 0x49, 0x4B, 0x4F, 0x0A }); // Header
            bw.Write(new byte[] { (byte)MessageType, 0x0b, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }); // Message type, length (11 more QWORDs)
            bw.Write(DateEpoch); // 1 QWORD
            bw.Write(PublicKeyX); // 3 QWORD's
            bw.Write(PublicKeyY); // 3 QWORD's
            bw.Write(Nonce); // 1 QWORD
            bw.Write(Server.PROTOCOL_VERSION);
            bw.Write(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
            bw.Write(new byte[] { 0x0C, 0xAB, 0x00, 0x5E, 0xFF, 0xFF, 0xFF, 0xFF }); // Caboose
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

            using var ms = new MemoryStream(messageBytes);
            using var br = new BinaryReader(ms);

            var header = br.ReadUInt64();
            Debug.Assert(header == 5573577635319729930);

            var messageType = br.ReadByte();
            Debug.Assert(MessageType.NodeHello.Equals(messageType), "Message is parsed as wrong type");

            var qWords = BitConverter.ToUInt64([.. br.ReadBytes(7), 0], 0);
            Debug.Assert(qWords == 11);

            var payload = new byte[8 * qWords];
            Buffer.BlockCopy(messageBytes, (int)ms.Position, payload, 0, payload.Length);
            ms.Seek(payload.Length, SeekOrigin.Current);

            FromPayload(payload);

            var caboose = br.ReadUInt64();
            Debug.Assert(caboose == 912823757494550527);
        }

        public void FromPayload(byte[] payload)
        {
            ArgumentNullException.ThrowIfNull(payload);

            if (payload.Length < 1)
            {
                throw new ArgumentException("Payload body less than one byte in length", nameof(payload));
            }

            using var ms = new MemoryStream(payload);
            using var br = new BinaryReader(ms);

            DateEpoch = br.ReadUInt64();
            PublicKeyX = br.ReadBytes(32) ?? throw new InvalidOperationException("Public Key X was provided as null");
            PublicKeyY = br.ReadBytes(32) ?? throw new InvalidOperationException("Public Key Y was provided as null");
            Nonce = br.ReadUInt64();

            var version = br.ReadUInt16();
            Debug.Assert(version == Server.PROTOCOL_VERSION);

            var zeroPadding = br.ReadBytes(6);
            Debug.Assert(BitConverter.ToUInt64([.. zeroPadding, 0, 0], 0) == 0);
        }
    }
}
