namespace Mystiko.IO
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;

    using JetBrains.Annotations;

    public class FileManifest
    {
        /// <summary>
        /// Gets or sets the version of the manifest protocol
        /// </summary>
        [UsedImplicitly]
        public uint Version { get; set; } = FileUtility.FILE_PACKAGING_PROTOCOL_VERSION;

        [Newtonsoft.Json.JsonIgnore]
        public IEnumerable<Block> Blocks
        {
            set
            {
                if (value != null)
                    this.BlockHashes = value.Select(b =>
                    {
                        Debug.Assert(b != null, "b != null");
                        return FileUtility.ByteArrayToString(b.FullHash);
                    }).ToArray();
            }
        }

        public string[] BlockHashes { get; [UsedImplicitly] set; }

        public string Name { get; [UsedImplicitly] set; }

        [Newtonsoft.Json.JsonIgnore]
        public FileInfo File
        {
            set
            {
                if (value != null)
                {
                    this.Name = value.Name;
                    this.CreatedUtc = value.CreationTimeUtc;
                }
            }
        }

        [Newtonsoft.Json.JsonIgnore]
        public byte[] UnlockBytes { get; set; }

        [CanBeNull]
        public string Unlock
        {
            get
            {
                return FileUtility.ByteArrayToString(this.UnlockBytes);
            }

            set
            {
                this.UnlockBytes = FileUtility.StringToByteArray(value);
            }
        }

        [Newtonsoft.Json.JsonIgnore]
        public DateTime PackedUtc { get; set; } = DateTime.UtcNow;

        [UsedImplicitly]
        public long? PackedDateEpoch
        {
            get
            {
                return Convert.ToInt64((this.PackedUtc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds);
            }

            set
            {
                if (value != null)
                    this.PackedUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(value.Value);
            }
        }

        [Newtonsoft.Json.JsonIgnore]
        public DateTime? CreatedUtc { get; set; }

        [UsedImplicitly]
        public long? CreatedDateEpoch
        {
            get
            {
                if (this.CreatedUtc != null)
                    return Convert.ToInt64((this.CreatedUtc.Value - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds);
                return null;
            }

            set
            {
                if (value != null)
                    this.CreatedUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(value.Value);
            }
        }
    }
}
