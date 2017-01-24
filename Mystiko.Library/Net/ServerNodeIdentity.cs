// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ServerNodeIdentity.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   An identity of a node consisting of a date of generation, key pair, and nonce proving the date and public key
//   meet a target difficulty requirement
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Net
{
    using System;
    using System.Diagnostics;
    using System.IO;

    using Cryptography;

    using JetBrains.Annotations;

    using Org.BouncyCastle.Asn1.Sec;
    using Org.BouncyCastle.Crypto.Generators;
    using Org.BouncyCastle.Crypto.Parameters;
    using Org.BouncyCastle.Security;

    /// <summary>
    /// An identity of a node consisting of a date of generation, key pair, and nonce proving the date and public key
    /// meet a target difficulty requirement
    /// </summary>
    public class ServerNodeIdentity
    {
        /// <summary>
        /// Gets or sets the date the node keys were created
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
        /// Gets or sets the nonce applied to the epoch and public keys of the node, proving
        /// as a proof of work
        /// </summary>
        public long Nonce { get; set; }

        /// <summary>
        /// Generates a new <see cref="ServerNodeIdentity"/> record and its private key
        /// </summary>
        /// <param name="targetDifficulty">The number of leading zeros required for the nonce-derived combined hash</param>
        /// <returns>
        /// A tuple that contains the <see cref="ServerNodeIdentity"/> record as well as a byte array representing its private key
        /// </returns>
        /// <remarks>
        /// This key pair is an ecliptic-curve (secp256k1) key pair.
        /// </remarks>
        // ReSharper disable once StyleCop.SA1650
        [NotNull, Pure]
        public static Tuple<ServerNodeIdentity, byte[]> Generate(int targetDifficulty)
        {
            var ret = new ServerNodeIdentity
            {
                DateEpoch = Convert.ToUInt32((DateTime.UtcNow - new DateTime(1970, 01, 01, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds)
            };

            // Elliptic Curve
            var ec = SecNamedCurves.GetByName("secp256k1");
            Debug.Assert(ec != null, "ec != null");
            var domainParams = new ECDomainParameters(ec.Curve, ec.G, ec.N, ec.H);
            var random = new SecureRandom();

            var keyGen = new ECKeyPairGenerator();
            var keyParams = new ECKeyGenerationParameters(domainParams, random);
            keyGen.Init(keyParams);
            var keyPair = keyGen.GenerateKeyPair();

            Debug.Assert(keyPair != null, "keyPair != null");
            var privateKeyParams = keyPair.Private as ECPrivateKeyParameters;
            var publicKeyParams = keyPair.Public as ECPublicKeyParameters;

            // Get Private Key
            Debug.Assert(privateKeyParams != null, "privateKeyParams != null");
            var privD = privateKeyParams.D;
            Debug.Assert(privD != null, "privD != null");

            Debug.Assert(publicKeyParams != null, "publicKeyParams != null");
            var qa = ec.G.Multiply(privD);
            Debug.Assert(qa != null, "qa != null");
            Debug.Assert(qa.X != null, "qa.X != null");
            ret.PublicKeyX = qa.X.ToBigInteger().ToByteArrayUnsigned();
            Debug.Assert(qa.Y != null, "qa.Y != null");
            ret.PublicKeyY = qa.Y.ToBigInteger().ToByteArrayUnsigned();

            // Calculate nonce for public key
            byte[] identityBytes;
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(ret.DateEpoch);
                bw.Write(ret.PublicKeyX);
                bw.Write(ret.PublicKeyY);
                bw.Write(0L); // Placeholder 8 bytes
                identityBytes = ms.ToArray();
            }
            ret.Nonce = HashUtility.HashForZeroCount(identityBytes, targetDifficulty);

            return new Tuple<ServerNodeIdentity, byte[]>(ret, privD.ToByteArray());
        }
    }
}
