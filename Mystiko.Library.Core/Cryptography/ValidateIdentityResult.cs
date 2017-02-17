// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ValidateIdentityResult.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   The result of a call to <see cref="HashUtility.ValidateIdentity(<see cref="System.UInt32"/>, byte[], byte[], <see cref="System.UInt64"/>, int)" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Cryptography
{
    using JetBrains.Annotations;

    /// <summary>
    /// The result of a call to <see cref="HashUtility.ValidateIdentity(uint, byte[], byte[], ulong, int)"/>
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
        /// <param name="compositeHashString">
        /// The hash of the composite values of the <see cref="Mystiko.Net.ServerNodeIdentity"/> components,
        /// if the <paramref name="difficultyValidated"/> is true and the hash was validated with the nonce
        /// </param>
        public ValidateIdentityResult(bool difficultyValidated, [CanBeNull] string compositeHashString)
        {
            this.DifficultyValidated = difficultyValidated;
            this.CompositeHash = compositeHashString;
        }

        /// <summary>
        /// Gets the hash of the composite values of the <see cref="Mystiko.Net.ServerNodeIdentity"/> components,
        /// if the <see cref="DifficultyValidated"/> is true and the hash was validated with the nonce
        /// </summary>
        [CanBeNull]
        public string CompositeHash { get; private set; }

        /// <summary>
        /// Gets a value indicating whether or not the nonce value has the requisite number of
        /// leading zeros when hashed together with other fields of this identity
        /// </summary>
        public bool DifficultyValidated { get; private set; }
    }
}
