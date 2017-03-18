namespace Mystiko.IO
{
    using JetBrains.Annotations;

    /// <summary>
    /// A resource record is the key that allows a client to know:
    ///  1. It has all the parts of a given file
    ///  2. The keying material used to unlock the manifest and decrypt the content
    /// </summary>
    public class ResourceRecord
    {
        /// <summary>
        /// Gets or sets the temporal file ID used to refer to an encrypted set of file blocks and an encrypted file manifest
        /// </summary>
        public string TemporalFileID { get; [UsedImplicitly] set; }

        /// <summary>
        /// Gets or sets the timestamp used to create the <see cref="TemporalFileID"/>
        /// </summary>
        public ulong EntropyTimestamp { get; [UsedImplicitly] set; }

        /// <summary>
        /// Gets or sets the list of block hashes, in an intentionally random order
        /// </summary>
        public string[] BlockHashes { get; [UsedImplicitly] set; }
    }
}
