namespace Mystiko.IO
{
    using System.Collections.Generic;

    using JetBrains.Annotations;

    public class LocalDirectoryManifest
    {
        [NotNull]
        public List<LocalShareFileManifest> LocalFileManifests { get; set; } = new List<LocalShareFileManifest>();
    }
}
