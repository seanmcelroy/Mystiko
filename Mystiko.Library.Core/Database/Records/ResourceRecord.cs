namespace Mystiko.Database.Records
{
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
        public required string TemporalFileID { get; init; }

        /// <summary>
        /// Gets or sets the timestamp used to create the <see cref="TemporalFileID"/>
        /// </summary>
        public required ulong EntropyTimestamp { get; init; }

        /// <summary>
        /// Gets or sets the list of block hashes, in an intentionally random order
        /// </summary>
        public required string[] BlockHashes { get; init; }
    }
}
