// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NodeAnnounce.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   A message sent from nodes to announce the presence of other nodes on the network
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Net.Messages
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;

    using JetBrains.Annotations;

    /// <summary>
    /// A message sent from nodes to announce the presence of other nodes on the network
    /// </summary>
    public class NodeAnnounce : IMessage
    {
        /// <summary>
        /// Gets or sets the nodes announced
        /// </summary>
        [NotNull]
        public NodeManifest[] Nodes { get; set; }

        /// <inheritdoc />
        public MessageType MessageType => MessageType.NodeAnnounce;

        /// <inheritdoc />
        public byte[] ToPayload()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write((byte)this.MessageType);
                bw.Write(this.Nodes.Length);
                foreach (var node in this.Nodes)
                {
                    bw.Write(node.Address);
                    bw.Write(node.PublicKeyX);
                    bw.Write(node.PublicKeyY);
                }

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
                Debug.Assert(MessageType.NodeAnnounce.Equals((MessageType)messageType), $"Message is parsed as wrong type (expected: {MessageType.NodeAnnounce}, actual: {(MessageType)messageType})");
                var nodeCount = br.ReadInt32();
                var nodeManifests = new List<NodeManifest>();
                for (var i = 0; i < nodeCount; i++)
                {
                    nodeManifests.Add(new NodeManifest
                                          {
                                              Address = br.ReadString(),
                                              PublicKeyX = br.ReadBytes(32),
                                              PublicKeyY = br.ReadBytes(32)
                                          });
                }

                this.Nodes = nodeManifests.ToArray();
            }
        }
        
        public class NodeManifest
        {
            [NotNull]
            public string Address { get; set; }

            [NotNull]
            public byte[] PublicKeyX { get; set; }

            [NotNull]
            public byte[] PublicKeyY { get; set; }
        }
    }
}
