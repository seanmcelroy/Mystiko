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
        /// Indicates the message is a <see cref="PeerAnnounce"/>
        /// </summary>
        PeerAnnounce = 1,

        /// <summary>
        /// Indicates the message is a <see cref="NodeAnnounce"/>
        /// </summary>
        NodeAnnounce = 100,

        /// <summary>
        /// Indicates the message is a <see cref="NodeHello"/>
        /// </summary>
        NodeHello = 101,
    }
}
