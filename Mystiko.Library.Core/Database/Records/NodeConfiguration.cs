using System.Collections.Generic;

namespace Mystiko.Database.Records
{
    using System.Diagnostics;

    using JetBrains.Annotations;

    using Mystiko.Net;

    public class NodeConfiguration
    {
        [CanBeNull]
        private ServerNodeIdentityAndKey _identity;

        /// <summary>
        /// Gets or sets the identifying information about a node's identity, as determined by its identity keying material
        /// </summary>
        [CanBeNull]
        public ServerNodeIdentityAndKey Identity
        {
            get
            {
                return this._identity;
            }

            set
            {
                Debug.Assert(value != null);
                Debug.Assert(value.PrivateKey != null, "value.PrivateKey != null");
                Debug.Assert(value.PublicKeyX != null, "value.PublicKeyX != null");
                Debug.Assert(value.PublicKeyX.Length == 32, "value.PublicKeyX.Length == 32");
                Debug.Assert(value.PublicKeyY != null, "value.PublicKeyY != null");
                Debug.Assert(value.PublicKeyY.Length == 32, "value.PublicKeyY.Length == 32");
                this._identity = value;
            }
        }

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
