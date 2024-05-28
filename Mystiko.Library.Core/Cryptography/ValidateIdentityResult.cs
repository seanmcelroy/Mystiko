// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ValidateIdentityResult.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   The result of a call to ValidateIdentity
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Cryptography
{
    /// <summary>
#pragma warning disable 1574
    /// The result of a call to <see cref="HashUtility.ValidateIdentity(uint, byte[], byte[], ulong, int)"/>
#pragma warning restore 1574
    /// </summary>
    public class ValidateIdentityResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ValidateIdentityResult"/> class.
        /// </summary>
        /// <param name="difficultyValidated">
        /// A value indicating whether or not the nonce value has the requisite number of
        /// leading zeros when hashed together with other fields of this identity
        /// </param>
        /// <param name="difficultyProvided">
        /// Gets a value indicating how much of the start of the hash was a '0', whether or not the target difficulty
        /// was validated
        /// </param>
        /// <param name="compositeHashString">
        /// The hash of the composite values of the <see cref="Mystiko.Net.ServerNodeIdentity"/> components,
        /// if the <paramref name="difficultyValidated"/> is true and the hash was validated with the nonce
        /// </param>
        public ValidateIdentityResult(bool difficultyValidated, byte difficultyProvided, string? compositeHashString)
        {
            this.DifficultyValidated = difficultyValidated;
            this.DifficultyProvided = difficultyProvided;
            this.CompositeHash = compositeHashString;
        }

        /// <summary>
        /// Gets the hash of the composite values of the <see cref="Mystiko.Net.ServerNodeIdentity"/> components,
        /// if the <see cref="DifficultyValidated"/> is true and the hash was validated with the nonce
        /// </summary>
        public string? CompositeHash { get; private set; }

        /// <summary>
        /// Gets a value indicating how much of the start of the hash was a '0', whether or not the target difficulty
        /// was validated
        /// </summary>
        public byte DifficultyProvided { get; private set; }

        /// <summary>
        /// Gets a value indicating whether or not the nonce value has the requisite number of
        /// leading zeros when hashed together with other fields of this identity
        /// </summary>
        public bool DifficultyValidated { get; private set; }
    }
}
