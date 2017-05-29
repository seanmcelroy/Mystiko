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
    using System.Security.Cryptography;

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
        public ulong DateEpoch { get; }

        /// <summary>
        /// Gets or sets the value of the public key X-value for this identity
        /// </summary>
        [NotNull]
        public byte[] PublicKeyX { get; }

        /// <summary>
        /// Gets or sets the base-64 encoded value of the public key X-value for this identity
        /// </summary>
        [NotNull]
        public string PublicKeyXBase64 => Convert.ToBase64String(this.PublicKeyX);

        /// <summary>
        /// Gets or sets the value of the public key Y-value for this identity
        /// </summary>
        [NotNull]
        public byte[] PublicKeyY { get; }

        /// <summary>
        /// Gets or sets the base-64 encoded value of the public key Y-value for this identity
        /// </summary>
        [NotNull]
        public string PublicKeyYBase64 => Convert.ToBase64String(this.PublicKeyY);

        /// <summary>
        /// Gets or sets the nonce applied to the epoch and public keys of the node, proving
        /// as a proof of work
        /// </summary>
        public ulong Nonce { get; }

        public ServerNodeIdentity(ulong dateEpoch, [NotNull] byte[] publicKeyX, [NotNull] byte[] publicKeyY, ulong nonce)
        {
            if (dateEpoch < Convert.ToUInt64((new DateTime(2017, 05, 01, 0, 0, 0, DateTimeKind.Utc) - new DateTime(1970, 01, 01, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds))
                throw new ArgumentOutOfRangeException(nameof(dateEpoch), "Date epoch cannot be before 2017-05-01");

            if (dateEpoch > Convert.ToUInt64((DateTime.UtcNow.AddMinutes(10) - new DateTime(1970, 01, 01, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds))
                throw new ArgumentOutOfRangeException(nameof(dateEpoch), "Date epoch cannot be in the future");

            if (publicKeyX == null)
                throw new ArgumentNullException(nameof(publicKeyX));

            if (publicKeyX.Length != 32)
                throw new ArgumentException("Public key X is not exactly 32 bytes long", nameof(publicKeyX));

            if (publicKeyY == null)
                throw new ArgumentNullException(nameof(publicKeyY));

            if (publicKeyY.Length != 32)
                throw new ArgumentException("Public key Y is not exactly 32 bytes long", nameof(publicKeyY));

            this.DateEpoch = dateEpoch;
            this.PublicKeyX = publicKeyX;
            this.PublicKeyY = publicKeyY;
            this.Nonce = nonce;
        }

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
        public static Tuple<ServerNodeIdentity, byte[]> Generate(byte targetDifficulty)
        {
            if (targetDifficulty <= 0)
                throw new ArgumentOutOfRangeException(nameof(targetDifficulty));

            Random insecureRandom;
            using (var rng = RandomNumberGenerator.Create())
            {
                var randomBytes = new byte[4];
                Debug.Assert(rng != null, "rng != null");
                rng.GetBytes(randomBytes);
                var seed = BitConverter.ToInt32(randomBytes, 0);
                insecureRandom = new Random(seed);
            }

            var dateEpoch = Convert.ToUInt64((DateTime.UtcNow - new DateTime(1970, 01, 01, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds - insecureRandom.Next(0, 3600));

            // Elliptic Curve
            // ReSharper disable once StringLiteralTypo
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
            Debug.Assert(qa.Normalize().XCoord != null, "qa.X != null");
            var publicKeyX = qa.Normalize().XCoord.ToBigInteger().ToByteArrayUnsigned();
            if (publicKeyX == null || publicKeyX.Length != 32)
                throw new InvalidOperationException("Failure to create 32-byte public key X");

            Debug.Assert(qa.Normalize().YCoord != null, "qa.Y != null");
            var publicKeyY = qa.Normalize().YCoord.ToBigInteger().ToByteArrayUnsigned();
            if (publicKeyY == null || publicKeyY.Length != 32)
                throw new InvalidOperationException("Failure to create 32-byte public key Y");

            // Calculate nonce for public key
            byte[] identityBytes;
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(dateEpoch);
                bw.Write(publicKeyX);
                bw.Write(publicKeyY);
                bw.Write(0L); // Placeholder 8 bytes
                identityBytes = ms.ToArray();
            }

            Debug.Assert(identityBytes != null, "identityBytes != null");
            var nonce = HashUtility.HashForZeroCount(identityBytes, targetDifficulty);

            return new Tuple<ServerNodeIdentity, byte[]>(new ServerNodeIdentity(dateEpoch, publicKeyX, publicKeyY, nonce), privD.ToByteArray());
        }

        /// <summary>
        /// Returns the composite hash of the components of this identity
        /// </summary>
        /// <returns>The composite hash of the components of this identity</returns>
        [NotNull, Pure]
        public string GetCompositeHash(byte targetDifficulty = 1)
        {
            var validatedIdentity = HashUtility.ValidateIdentity(this, targetDifficulty);
            if (!validatedIdentity.DifficultyValidated)
                throw new InvalidOperationException("This is not a valid identity");

            Debug.Assert(validatedIdentity.CompositeHash != null, "validatedIdentity.CompositeHash != null");
            return validatedIdentity.CompositeHash;
        }
    }
}
