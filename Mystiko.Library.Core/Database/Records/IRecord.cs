// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IRecord.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   An interface for any record in the block chain
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Database.Records
{
    using JetBrains.Annotations;

    /// <summary>
    /// An interface for any record in the block chain
    /// </summary>
    public interface IRecord
    {
        /// <summary>
        /// Converts the record to a block chain payload
        /// </summary>
        /// <returns>A serialized string representation of the record</returns>
        [Pure, NotNull]
        string ToPayload();

        /// <summary>
        /// Hydrates the record from a block chain payload
        /// </summary>
        /// <param name="payload">The serialized payload of the record</param>
        void FromPayload([NotNull] string payload);
    }
}
