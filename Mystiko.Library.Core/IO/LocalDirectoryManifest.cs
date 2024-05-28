namespace Mystiko.IO
{
    using System.Collections.Generic;

    public class LocalDirectoryManifest
    {
        public List<LocalShareFileManifest> LocalFileManifests { get; set; } = [];
    }
}
