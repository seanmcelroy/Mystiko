// --------------------------------------------------------------------------------------------------------------------
// <copyright file="HashUtility.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   Convenience utilities for hashing data
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Cryptography
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Security.Cryptography;

    using JetBrains.Annotations;

    /// <summary>
    /// Convenience utilities for hashing data
    /// </summary>
    public static class HashUtility
    {
        /// <summary>
        /// An instance of a hashing algorithm
        /// </summary>
        [NotNull]
        private static readonly SHA512 Hasher = SHA512.Create();

        /// <summary>
        /// Computes a hash for this block for the given pre-calculated header and a given nonce value
        /// </summary>
        /// <param name="nonce">The value to which to apply to the last eight bytes of the <paramref name="preNonceArray"/> before calculating the hash</param>
        /// <param name="preNonceArray">The output from the byte array for the header of this block</param>
        /// <returns>The hash for given pre-calculated header and a given nonce value</returns>
        [NotNull, Pure]
        public static byte[] HashForNonce(long nonce, [NotNull] byte[] preNonceArray)
        {
            Array.Copy(BitConverter.GetBytes(nonce), 0, preNonceArray, preNonceArray.Length - 8, 8);
            return Hasher.ComputeHash(preNonceArray);
        }

        /// <summary>
        /// Finds the nonce value that would make this block have the <paramref name="zeroCount"/> number of zeros at the
        /// start of its hash value
        /// </summary>
        /// <param name="input">
        /// The input byte array to hash
        /// </param>
        /// <param name="zeroCount">
        /// The number of zeros for which to find the first nonce
        /// </param>
        /// <returns>
        /// The nonce value that makes this block's header have the required number of leading zeros
        /// </returns>
        [Pure]
        public static uint HashForZeroCount([NotNull] byte[] input, int zeroCount)
        {
            var best = 0;

            for (var l = 0U; l < uint.MaxValue; l++)
            {
                var candidateHash = HashForNonce(l, input);
                var success = false;
                var candidateHashString = BitConverter.ToString(candidateHash).Replace("-", string.Empty);

                var i = 0;
                foreach (var c in candidateHashString)
                {
                    if (c == '0')
                    {
                        i++;
                        if (i == zeroCount)
                        {
                            success = true;
                            best = Math.Max(best, i);
                            break;
                        }
                    }
                    else
                    {
                        best = Math.Max(best, i);
                        break;
                    }
                }

                if (l % 10000 == 0)
                {
                    Console.Write($"\rBest: {best} of {zeroCount} (#{l:N0})");
                }

                if (success)
                {
                    Console.Write($"\rBest: {best} of {zeroCount} (#{l:N0})");
                    return l;
                }
            }

            throw new InvalidOperationException($"Unable to find any hash that returns {zeroCount} zeros");
        }

        /// <summary>
        /// Validates the nonce of the identity matches the <paramref name="targetDifficulty"/> number of leading zeros required
        /// </summary>
        /// <param name="dateEpoch">The date the identity was created, expressed in the number of seconds since the epoch (January 1, 1970)</param>
        /// <param name="publicKeyX">The value of the public key X-value for this identity</param>
        /// <param name="publicKeyY">The value of the public key Y-value for this identity</param>
        /// <param name="nonce">The nonce value that when applied the nonce value that when applied </param>
        /// <param name="targetDifficulty">The number of leading zeros required for the nonce-derived combined hash</param>
        /// <returns>A value indicating whether or not the <see cref="nonce"/> value has the requisite number of leading zeros when hashed together with other fields of this identity</returns>
        [NotNull]
        public static ValidateIdentityResult ValidateIdentity(uint dateEpoch, [NotNull] byte[] publicKeyX, [NotNull] byte[] publicKeyY, ulong nonce, int targetDifficulty)
        {
            byte[] identityBytes;
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(dateEpoch);
                bw.Write(publicKeyX);
                bw.Write(publicKeyY);
                bw.Write(nonce);
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
                            return new ValidateIdentityResult(true, candidateHashString);
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return new ValidateIdentityResult(false, null);
        }
    }
}
