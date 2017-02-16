namespace Mystiko.Messages
{
    using System;
    using System.IO;

    using JetBrains.Annotations;

    public class FileInfo
    {
        [NotNull]
        public string FileName { get; set; }

        public long SizeBytes { get; set; }

        [NotNull]
        public byte[] SHA512 { get; set; }

        public static FileInfo Parse(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Unable to find file in parameter 'path'", path);
            }

            var fsi = new System.IO.FileInfo(path);

            var ret = new FileInfo
            {
                FileName = Path.GetFileName(path),
                SizeBytes = fsi.Length
            };

            using (var stream = File.OpenRead(path))
            using (var sha = System.Security.Cryptography.SHA512.Create())
            {
                var hash = sha.ComputeHash(stream);
                ret.SHA512 = hash;
            }

            return ret;
        }
    }
}
