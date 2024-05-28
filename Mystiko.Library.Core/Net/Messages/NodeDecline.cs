// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NodeDecline.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   A response to a NodeHello message a respondant node sents if it rejects the connection request
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
    public class NodeDecline : IMessage
    {
        /// <summary>
        /// The reason why the respondant rejected the initiator
        /// </summary>
        public enum NodeDeclineReasonCode : byte
        {
            /// <summary>
            /// The node refuses to clarify why it is declining the connection.
            /// This type of rejection indicates nothing about the likely outcome
            /// of future connection attempts.  The initiator may have sent a
            /// valid message that was parsed and trusted, but no qualification is
            /// implied by this decline reason.
            /// </summary>
            Unknown = 0,

            /// <summary>
            /// The node is unable to establish communications with the initiator,
            /// for any reason, including version incompatibility, resource
            /// capacity, or even if the node knows its host is shutting down.
            /// This type of rejection indicates the initiator may try again
            /// at some point in the future.
            /// </summary>
            Unable = 1,

            /// <summary>
            /// The node is able to establish communications with the initiator,
            /// but is unwilling to do so.  This could stem from reasons such as
            /// the respondant is shunning the initiator, perceives it to be
            /// untrustworthy (such as lying or providing corrupted or garbage
            /// content) or a leech of network resources.This type of rejection
            /// indicates the initiator should not try contacting this respondant
            /// again with the same node identity, and that doing so with a new
            /// identity may not resolve the issue.
            /// </summary>
            Unwilling = 2,

            /// <summary>
            /// The node is not able to parse or accept the identity of the
            /// initiator node.  For instance, the message is malformed, the
            /// keying material is invalid, or the date of the mined identity
            /// is in the future or too far in the past.  This type of rejection
            /// indicates the initiator should not try contacting this respondant
            /// again with the same node identity, and that doing so with a new
            /// identity or properly formatted issue may resolve the issue.
            /// </summary>
            Untrusted = 3
        }

        /// <summary>
        /// The suggested remeidiation by the respondant how the initiator can overcome the rejection
        /// </summary>
        public enum NodeDeclineRemediationCode : byte
        {
            /// <summary>
            /// The respondant has no remediation suggestion to make for the
            /// initiator to overcome the rejection
            /// </summary>
            Unknown = 0,

            /// <summary>
            /// Update to a newer version of the protocol
            /// </summary>
            Update = 1,

            /// <summary>
            /// Log the exchange of unique chunks between more unique nodes
            /// in the blockchain to improve network reputation
            /// </summary>
            Share = 2,

            /// <summary>
            /// Mine a new node identity
            /// </summary>
            Rekey = 3,

            /// <summary>
            /// Try another node
            /// </summary>
            Another = 4,

            /// <summary>
            /// Simply don't connect to this node again for a while
            /// </summary>
            Scram = 5
        }

        /// <inheritdoc />
        public MessageType MessageType => MessageType.NodeDecline;

        /// <summary>
        /// Gets or sets the reason why the respondant rejected the initiator
        /// </summary>
        public NodeDeclineReasonCode DeclineReason { get; set; }

        /// <summary>
        /// Gets or sets the suggested remeidiation by the respondant how the initiator can overcome the rejection
        /// </summary>
        public NodeDeclineRemediationCode Remediation { get; set; }

        /// <inheritdoc />
        public byte[] ToMessage()
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write(new byte[] { 0x4D, 0x59, 0x53, 0x54, 0x49, 0x4B, 0x4F, 0x0A }); // Header
            bw.Write(new byte[] { (byte)this.MessageType, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }); // Message type, length (1 more QWORDs)
            bw.Write((byte)this.DeclineReason); // 1 byte
            bw.Write((byte)this.Remediation); // 1 byte
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

            using (var ms = new MemoryStream(messageBytes))
            using (var br = new BinaryReader(ms))
            {
                var header = br.ReadUInt64();
                Debug.Assert(header == 5573577635319729930);

                var messageType = br.ReadByte();
                Debug.Assert(MessageType.NodeDecline.Equals(messageType), "Message is parsed as wrong type");

                var qWords = BitConverter.ToUInt64(br.ReadBytes(7).Append((byte)0).ToArray(), 0);
                Debug.Assert(qWords == 1);

                var payload = new byte[8 * qWords];
                Buffer.BlockCopy(messageBytes, (int)ms.Position, payload, 0, payload.Length);
                ms.Seek(payload.Length, SeekOrigin.Current);

                this.FromPayload(payload);

                var caboose = br.ReadUInt64();
                Debug.Assert(caboose == 912823757494550527);
            }
        }

        public void FromPayload(byte[] payload)
        {
            ArgumentNullException.ThrowIfNull(payload);

            if (payload.Length < 1)
            {
                throw new ArgumentException("Payload body less than one byte in length", nameof(payload));
            }

            using (var ms = new MemoryStream(payload))
            using (var br = new BinaryReader(ms))
            {
                this.DeclineReason = (NodeDeclineReasonCode)br.ReadByte();

                this.Remediation = (NodeDeclineRemediationCode)br.ReadByte();

                var zeroPadding = br.ReadBytes(6);
                Debug.Assert(BitConverter.ToUInt64(zeroPadding.Append((byte)0).Append((byte)0).ToArray(), 0) == 0);
            }
        }
    }
}
