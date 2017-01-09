// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IClientChannel.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   An interface for a server to interact with its peer neighbors
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Net
{
    using System;

    using JetBrains.Annotations;

    using Messages;

    /// <summary>
    /// An interface for a server to interact with its peer neighbors
    /// </summary>
    public interface IClientChannel
    {
        /// <summary>
        /// Registers a handler for the client channel
        /// </summary>
        /// <param name="messageHandler">An action that receives messages from the peer neighbor</param>
        void RegisterHandler([NotNull] Action<IMessage> messageHandler);

        /// <summary>
        /// Sends a message to a client
        /// </summary>
        /// <param name="message">The message to send to the client</param>
        void Send([NotNull] IMessage message);
    }
}