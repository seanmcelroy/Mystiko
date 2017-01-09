namespace Mystiko.Net.Messages
{
    /// <summary>
    /// A message sent from nodes to announce the presence of other nodes on the network
    /// </summary>
    public class NodeAnnounce : IMessage
    {
        public class NodeManifest
        {
            public string Address { get; set; }

            public byte[] PublicKeyX { get; set; }

            public byte[] PublicKeyY { get; set; }
        }

        /// <summary>
        /// Gets or sets the nodes announced
        /// </summary>
        public NodeManifest[] Nodes { get; set; }
    }
}
