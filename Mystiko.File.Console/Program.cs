namespace Mystiko.File.Console
{
    using System.IO;
    using System.Threading.Tasks;

    using IO;

    using Newtonsoft.Json;

    using File = System.IO.File;

    /// <summary>
    /// A basic command line utility for executing methods of the Mystiko library
    /// </summary>
    // ReSharper disable once StyleCop.SA1650
    public static class Program
    {
        public static void Main(string[] args)
        {
            var options = new CommandLineOptions();
            if (!CommandLine.Parser.Default.ParseArguments(args, options))
            {
                System.Console.WriteLine(options.GetUsage());
                if (options.Pause)
                    System.Console.ReadLine();
                return;
            }

            if (!string.IsNullOrEmpty(options.EncryptFile))
            {
                Encrypt(options);
                return;
            }

            if (!string.IsNullOrEmpty(options.DecryptFile))
            {
                Decrypt(options);
                return;
            }

            if (!string.IsNullOrEmpty(options.PrehashPath))
            {
                Prehash(options);
                return;
            }

            // No operation specified
            {
                System.Console.WriteLine("No operation specified.  Use the --encrypt or --decrypt operation parameter\r\n");
                System.Console.WriteLine(options.GetUsage());
                if (options.Pause)
                    System.Console.ReadLine();
            }
        }

        private static void Encrypt(CommandLineOptions options)
        {
            System.Console.WriteLine("Encrypting {0}... ", options.EncryptFile);

            if (!File.Exists(options.EncryptFile))
            {
                System.Console.WriteLine("Unable to locate file: {0}", options.EncryptFile);
                if (options.Pause)
                    System.Console.ReadLine();
                return;
            }

            var encryptFile = new FileInfo(options.EncryptFile);
            if (encryptFile.Directory == null || string.IsNullOrWhiteSpace(encryptFile.DirectoryName))
            {
                System.Console.WriteLine("Unable to locate parent directory of file: {0}", options.EncryptFile);
                if (options.Pause)
                    System.Console.ReadLine();
                return;
            }

            Task.Run(async () =>
            {
                var chunkResult = await FileUtility.ChunkFileViaOutputDirectory(encryptFile, encryptFile.Directory.FullName, options.Force, options.Verbose, options.Verify, options.Size);
                var manifestFile = new FileInfo(Path.Combine(encryptFile.Directory.FullName, encryptFile.Name + ".mystiko"));

                if (manifestFile.Exists)
                {
                    if (!options.Force)
                    {
                        System.Console.WriteLine("Manifest file already exists: {0}", manifestFile.FullName);
                        if (options.Pause)
                            System.Console.ReadLine();
                        return;
                    }

                    manifestFile.Delete();
                }

                using (var sw = new StreamWriter(manifestFile.FullName))
                {
                    await sw.WriteAsync(JsonConvert.SerializeObject(chunkResult));
                    sw.Close();
                }
            }).Wait();

            System.Console.WriteLine("Encryption complete.");
            if (options.Pause)
                System.Console.ReadLine();
        }

        private static void Decrypt(CommandLineOptions options)
        {
            var output = string.IsNullOrWhiteSpace(options.OutputFile) ? options.DecryptFile.Replace(".mystiko", string.Empty) + ".decrypted" : options.OutputFile;

            if (!File.Exists(options.DecryptFile))
            {
                System.Console.WriteLine("Unable to locate file: {0}", options.DecryptFile);
                if (options.Pause)
                    System.Console.ReadLine();
                return;
            }

            if (File.Exists(output))
            {
                if (!options.Force)
                {
                    System.Console.WriteLine("Ouput file already exists: {0}", output);
                    if (options.Pause)
                        System.Console.ReadLine();
                    return;
                }

                File.Delete(output);
                System.Console.WriteLine("Deleted output file that already existed: {0}", output);
            }

            var unchunkResult = false;
            Task.Run(
                async () =>
                {
                    unchunkResult = await FileUtility.UnchunkFileViaOutputDirectory(new FileInfo(options.DecryptFile), new FileInfo(output), options.Force);
                }).Wait();

            if (unchunkResult)
            {
                System.Console.WriteLine("Decryption complete: {0}", output);
                if (options.Pause)
                    System.Console.ReadLine();
            }
            else
            {
                System.Console.WriteLine("Decryption failed");
                if (options.Pause)
                    System.Console.ReadLine();
            }
        }

        /// <summary>
        /// Pre-hashes a directory without actually creating encrypted split files
        /// </summary>
        /// <param name="options">Command line options used for identifying and pre-hashing the directory</param>
        private static void Prehash(CommandLineOptions options)
        {
            System.Console.WriteLine("Prehash {0}... ", options.PrehashPath);

            if (!Directory.Exists(options.PrehashPath))
            {
                System.Console.WriteLine("Unable to locate directory: {0}", options.PrehashPath);
                if (options.Pause)
                    System.Console.ReadLine();
                return;
            }

            if (Directory.Exists(options.PrehashPath))
            {
                Task.Run(async () =>
                    {
                        var localManifest = await DirectoryUtility.PreHashDirectory(
                            options.PrehashPath,
                            s => System.Console.WriteLine($"Directory: {s}"),
                            s => System.Console.WriteLine($"\tFile: {s}"));
                        var manifestFile = new FileInfo(Path.Combine(options.PrehashPath, "directory.mystiko"));

                        if (manifestFile.Exists)
                        {
                            if (!options.Force)
                            {
                                System.Console.WriteLine("Directory manifest file already exists: {0}", manifestFile.FullName);
                                if (options.Pause)
                                    System.Console.ReadLine();
                                return;
                            }

                            manifestFile.Delete();
                        }

                        using (var sw = new StreamWriter(manifestFile.FullName))
                        {
                            await sw.WriteAsync(JsonConvert.SerializeObject(localManifest));
                            sw.Close();
                        }
                    }).Wait();

                System.Console.WriteLine("Directory hashing complete.");
                if (options.Pause)
                    System.Console.ReadLine();
            }
        }
    }
}