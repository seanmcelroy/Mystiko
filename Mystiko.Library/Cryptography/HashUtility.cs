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
    }
}
