using System.Collections.Generic;

namespace Mystiko.Database.Records
{
    using Mystiko.Net;

    public class NodeConfiguration
    {
        /// <summary>
        /// Gets or sets the identifying information about a node's identity, as determined by its identity keying material
        /// </summary>
        public ServerNodeIdentityAndKey Identity { get; set; }

        /// <summary>
        /// Gets or sets the resource records known to this node
        /// </summary>
        public List<ResourceRecord> ResourceRecords { get; set; } = new List<ResourceRecord>();

        /// <summary>
        /// Gets or sets a value indicating whether the server channel will not broadcast its presence, but will listen for other nodes only
        /// </summary>
        public bool Passive { get; set; } = false;

        /// <summary>
        /// Gets or sets the port on which to listen for peer client connections.  By default, this is 5109
        /// </summary>
        public int ListenerPort { get; set; } = 5109;
    }
}
