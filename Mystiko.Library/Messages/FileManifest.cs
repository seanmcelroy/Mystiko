﻿namespace Mystiko.Messages
{
    using System;
    using System.IO;

    public class FileManifest
    {
        public FileInfo FileInformation { get; set; }

        public static FileManifest Parse(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException("Unable to find file in parameter 'path'", path);

            var ret = new FileManifest
                   {
                       FileInformation = FileInfo.Parse(path)
                   };

            return ret;
        }
    }
}
