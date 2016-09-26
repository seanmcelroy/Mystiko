using System;

namespace Mystiko.IO
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using JetBrains.Annotations;

    public class FileManifest
    {
        public uint Version { get; set; } = IO.FileUtility.FILE_PACKAGING_PROTOCOL_VERSION;

        [Newtonsoft.Json.JsonIgnore]
        public IEnumerable<Block> Blocks
        {
            set
            {
                if (value != null)
                    this.BlockHashes = value.Select(b => IO.FileUtility.ByteArrayToString(b.FullHash)).ToArray();
            }
        }

        public string[] BlockHashes { get; set; }

        public string Name { get; set; }

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
                return IO.FileUtility.ByteArrayToString(this.UnlockBytes);
            }
            set
            {
                this.UnlockBytes = IO.FileUtility.StringToByteArray(value);
            }
        }

        [Newtonsoft.Json.JsonIgnore]
        public DateTime PackedUtc { get; set; } = DateTime.UtcNow;

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

        public string ToJson([NotNull] FileInfo fileInfo)
        {
            if (fileInfo == null)
                throw new ArgumentNullException(nameof(fileInfo));
            if (!fileInfo.Exists)
                throw new FileNotFoundException("File not found when generating manifest.", fileInfo.FullName);

            return Newtonsoft.Json.JsonConvert.SerializeObject(this);
        }
    }
}
