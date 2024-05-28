// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ServerNodeIdentityAndKey.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   An identity of a node consisting of a date of generation, key pair, and nonce proving the date and public key
//   meet a target difficulty requirement.  It also includes the private key for local serialization and storage.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Net
{
    using System;

    /// <summary>
    /// An identity of a node consisting of a date of generation, key pair, and nonce proving the date and public key
    /// meet a target difficulty requirement.  It also includes the private key for local serialization and storage.
    /// </summary>
    public sealed class ServerNodeIdentityAndKey : ServerNodeIdentity
    {
        /// <summary>
        /// Gets or sets the byte array of the private key
        /// </summary>
        public byte[] PrivateKey { get; }

        public ServerNodeIdentityAndKey(ulong dateEpoch, byte[] publicKeyX, byte[] publicKeyY, ulong nonce, byte[] privateKey)
            : base(dateEpoch, publicKeyX, publicKeyY, nonce)
        {
            this.PrivateKey = privateKey ?? throw new ArgumentNullException(nameof(privateKey));
        }
    }
}
