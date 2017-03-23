// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MessageType.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   The unique type number of a message
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Net.Messages
{
    /// <summary>
    /// The unique type number of a message
    /// </summary>
    public enum MessageType : byte
    {
        /// <summary>
        /// An unknown message type
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Indicates the message is a <see cref="NodeHello"/>
        /// </summary>
        NodeHello = 1,

        /// <summary>
        /// Indicates the message is a <see cref="NodeDecline"/>
        /// </summary>
        NodeDecline = 2,

        /// <summary>
        /// Indicates the message is a <see cref="PeerAnnounce"/>
        /// </summary>
        PeerAnnounce = 111,

        /// <summary>
        /// Indicates the message is a <see cref="NodeAnnounce"/>
        /// </summary>
        NodeAnnounce = 100
    }
}
