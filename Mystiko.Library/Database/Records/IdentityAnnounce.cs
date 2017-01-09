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
    using System.Diagnostics;
    using System.IO;
    using System.Security.Cryptography;

    using JetBrains.Annotations;

    using Mystiko.Cryptography;

    using Org.BouncyCastle.Asn1.Sec;
    using Org.BouncyCastle.Crypto.Generators;
    using Org.BouncyCastle.Crypto.Parameters;
    using Org.BouncyCastle.Security;

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
        /// Gets or sets the base-64 encoded value of the public key X-value for this identity
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
        /// Gets or sets the base-64 encoded value of the public key Y-value for this identity
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
        /// Gets or sets the nonce value that when applied 
        /// </summary>
        public long Nonce { get; set; }

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
            var ret = new IdentityAnnounce
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

            return new Tuple<IdentityAnnounce, byte[]>(ret, privD.ToByteArray());
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
                this.Nonce = br.ReadInt64();
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
            byte[] identityBytes;
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(this.DateEpoch);
                bw.Write(this.PublicKeyX);
                bw.Write(this.PublicKeyY);
                bw.Write(this.Nonce);
                identityBytes = ms.ToArray();
            }

            using (var sha = SHA512.Create())
            {
                Debug.Assert(sha != null, "sha != null");
                var candidateHash = sha.ComputeHash(identityBytes);
                var candidateHashString = BitConverter.ToString(candidateHash).Replace("-", string.Empty);

                var i = 0;
                foreach (var c in candidateHashString)
                {
                    if (c == '0')
                    {
                        i++;
                        if (i == targetDifficulty)
                        {
                            return true;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return false;
        }
    }
}
