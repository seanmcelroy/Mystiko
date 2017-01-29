// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IdentityAnnounce.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   A record that announces a new identity on the network.  Identities may or may not ever
//   be associated to a single or identifyable group of humans, organizations, or nodes;
//   they simply represent a proof-of-work that was generated to link together transactions
//   that were requested together.  Identities can be 'throw-away', and any given actor
//   on the network may use multiple identities, or even share an identity with another
//   for repudiation.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Database.Records
{
    using System;
    using System.IO;
    
    using JetBrains.Annotations;

    using Cryptography;

    /// <summary>
    /// A record that announces a new identity on the network.  Identities may or may not ever
    /// be associated to a single or identifyable group of humans, organizations, or nodes;
    /// they simply represent a proof-of-work that was generated to link together transactions
    /// that were requested together.  Identities can be 'throw-away', and any given actor
    /// on the network may use multiple identities, or even share an identity with another
    /// for repudiation.
    /// </summary>
    public class IdentityAnnounce : IRecord
    {
        /// <summary>
        /// Gets or sets the date the identity was created, expressed in the number of seconds since the epoch (January 1, 1970)
        /// </summary>
        public uint DateEpoch { get; set; }

        /// <summary>
        /// Gets or sets the value of the public key X-value for this identity
        /// </summary>
        [NotNull]
        public byte[] PublicKeyX { get; set; }

        /// <summary>
        /// Gets or sets the base-64 encoded value of the public key X-value for this identity
        /// </summary>
        [NotNull]
        public string PublicKeyXBase64
        {
            get
            {
                return Convert.ToBase64String(this.PublicKeyX);
            }

            set
            {
                this.PublicKeyX = Convert.FromBase64String(value);
            }
        }

        /// <summary>
        /// Gets or sets the value of the public key Y-value for this identity
        /// </summary>
        [NotNull]
        public byte[] PublicKeyY { get; set; }

        /// <summary>
        /// Gets or sets the base-64 encoded value of the public key Y-value for this identity
        /// </summary>
        [NotNull]
        public string PublicKeyYBase64
        {
            get
            {
                return Convert.ToBase64String(this.PublicKeyY);
            }

            set
            {
                this.PublicKeyY = Convert.FromBase64String(value);
            }
        }

        /// <summary>
        /// Gets or sets the nonce value that when applied to the composite value that is hashed has a certain number of leading zeros in the hash
        /// </summary>
        public ulong Nonce { get; set; }

        /// <summary>
        /// Generates a new <see cref="IdentityAnnounce"/> record and its private key
        /// </summary>
        /// <param name="targetDifficulty">The number of leading zeros required for the nonce-derived combined hash</param>
        /// <returns>
        /// A tuple that contains the <see cref="IdentityAnnounce"/> record as well as a byte array representing its private key
        /// </returns>
        /// <remarks>
        /// This key pair is an ecliptic-curve (secp256k1) key pair.
        /// </remarks>
        // ReSharper disable once StyleCop.SA1650
        public static Tuple<IdentityAnnounce, byte[]> Generate(int targetDifficulty)
        {
            var identity = Net.ServerNodeIdentity.Generate(targetDifficulty);
            return new Tuple<IdentityAnnounce, byte[]>(
                new IdentityAnnounce
                {
                    DateEpoch = identity.Item1.DateEpoch,
                    Nonce = identity.Item1.Nonce,
                    PublicKeyX = identity.Item1.PublicKeyX,
                    PublicKeyY = identity.Item1.PublicKeyY
                }, 
                identity.Item2);
        }

        /// <inheritdoc />
        public string ToPayload()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(this.DateEpoch);
                bw.Write(this.PublicKeyXBase64);
                bw.Write(this.PublicKeyYBase64);
                bw.Write(this.Nonce);

                return Convert.ToBase64String(ms.ToArray());
            }
        }

        /// <inheritdoc />
        public void FromPayload(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                throw new ArgumentNullException(nameof(payload));
            }
            
            using (var ms = new MemoryStream(Convert.FromBase64String(payload)))
            using (var br = new BinaryReader(ms))
            {
                this.DateEpoch = br.ReadUInt32();
                this.PublicKeyXBase64 = br.ReadString();
                this.PublicKeyYBase64 = br.ReadString();
                this.Nonce = br.ReadUInt64();
            }
        }

        /// <summary>
        /// Validates the nonce of the identity matches the <paramref name="targetDifficulty"/> number of leading zeros required
        /// </summary>
        /// <param name="targetDifficulty">
        /// The number of leading zeros required for the nonce-derived combined hash
        /// </param>
        /// <returns>A value indicating whether or not the <see cref="Nonce"/> value has the requisite number of leading zeros when hashed together with other fields of this identity</returns>
        public bool Verify(int targetDifficulty)
        {
            return HashUtility.ValidateIdentity(this.DateEpoch, this.PublicKeyX, this.PublicKeyY, this.Nonce, targetDifficulty);
        }
    }
}
