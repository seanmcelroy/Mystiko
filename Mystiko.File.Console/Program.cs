namespace Mystiko.File.Console
{
    using System.IO;

    using IO;

    using Newtonsoft.Json;

    using File = System.IO.File;

    class Program
    {
        static void Main(string[] args)
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

                var chunkResult = FileUtility.ChunkFileViaOutputDirectory(encryptFile, encryptFile.Directory.FullName, options.Force, options.Verbose, options.Verify, options.Size);
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
                    sw.Write(JsonConvert.SerializeObject(chunkResult.Item1));
                    sw.Close();
                }

                System.Console.WriteLine("Encryption complete.");
                if (options.Pause)
                    System.Console.ReadLine();
                return;
            }

            if (!string.IsNullOrEmpty(options.DecryptFile))
            {
                var output = string.IsNullOrWhiteSpace(options.OutputFile) ? options.DecryptFile.Replace(".mystiko", "") + ".decrypted" : options.OutputFile;

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

                var unchunkResult = FileUtility.UnchunkFileViaOutputDirectory(new FileInfo(options.DecryptFile), new FileInfo(output), options.Force);
                if (unchunkResult)
                {
                    System.Console.WriteLine("Decryption complete: {0}", output);
                    if (options.Pause)
                        System.Console.ReadLine();
                    return;
                }
            }

            // No operation specified
            {
                System.Console.WriteLine("No operation specified.  Use the --encrypt or --decrypt operation parameter\r\n");
                System.Console.WriteLine(options.GetUsage());
                if (options.Pause)
                    System.Console.ReadLine();
            }
        }
    }
}