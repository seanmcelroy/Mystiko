// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RecordType.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   The type of a payload in a record in the block chain
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Database.Records
{
    /// <summary>
    /// The type of a payload in a record in the block chain
    /// </summary>
    public enum RecordType : byte
    {
        /// <summary>
        /// A record that announces a new identity on the network
        /// </summary>
        IdentityAnnounce = 1
    }
}
