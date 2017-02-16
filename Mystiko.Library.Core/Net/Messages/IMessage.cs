// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IMessage.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   A message that can be sent over a communications channel to other nodes
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Net.Messages
{
    using JetBrains.Annotations;

    /// <summary>
    /// A message that can be sent over a communications channel to other nodes
    /// </summary>
    public interface IMessage
    {
        /// <summary>
        /// Gets the type of the message
        /// </summary>
        MessageType MessageType { get; }

        /// <summary>
        /// Converts the record to a block chain payload
        /// </summary>
        /// <returns>A serialized string representation of the record</returns>
        [Pure, NotNull]
        byte[] ToPayload();

        /// <summary>
        /// Hydrates the record from a block chain payload
        /// </summary>
        /// <param name="payload">The serialized payload of the record</param>
        void FromPayload([NotNull] byte[] payload);
    }
}
