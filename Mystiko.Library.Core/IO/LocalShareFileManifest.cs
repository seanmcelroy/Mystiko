// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LocalShareFileManifest.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   A local share file manifest is an entry about a file that is locally available,
//   but may or may not be in the Mystiko format.  This metadata stub would be pre-calculated
//   ahead of time for the purposes of allowing network publication or discovery of this asset,
//   without needing to encrypt, split and store a copy of all data.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.IO
{
    /// <summary>
    /// A local share file manifest is an entry about a file that is locally available,
    /// but may or may not be in the Mystiko format.  This metadata stub would be pre-calculated
    /// ahead of time for the purposes of allowing network publication or discovery of this asset,
    /// without needing to encrypt, split and store a copy of all data.
    /// </summary>
    public class LocalShareFileManifest
    {
        /// <summary>
        /// Gets or sets the version of the manifest protocol
        /// </summary>
        public uint Version { get; set; } = FileUtility.FILE_LOCAL_SHARE_MANAGEMENT_PROTOCOL_VERSION;

        /// <summary>
        /// Gets or sets the file manifest shared with remote hosts
        /// </summary>
        public FileManifest FileManifest { get; set; }

        /// <summary>
        /// Gets or sets the path of the local file
        /// </summary>
        public string LocalPath { get; set; }

        /// <summary>
        /// Gets or sets the size of the local file in bytes
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the SHA512 hash of the content of the local file
        /// </summary>
        public string Hash { get; set; }

        /// <summary>
        /// Gets or sets the lengths of blocks when the file will be split and encrypted.
        /// These are not part of the <see cref="FileManifest"/>, since observers should
        /// not know sizes of chunks ahead of time so as not to fingerprint files
        /// </summary>
        public string[] BlockLengths { get; set; }
    }
}
