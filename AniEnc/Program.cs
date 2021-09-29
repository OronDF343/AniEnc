using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;
using ImageMagick;

namespace AniEnc
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length < 3)
            {
                PrintUsage();
                return;
            }
            
            var fileName = args[2];
            string zipFile;
            string jsFile;
            var ext = Path.GetExtension(fileName);
            if (string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase))
            {
                zipFile = fileName;
                jsFile = zipFile + ".js";
            }
            else if (string.Equals(ext, ".js", StringComparison.OrdinalIgnoreCase))
            {
                jsFile = fileName;
                zipFile = jsFile[..^3];
            }
            else
            {
                Console.Error.WriteLine("Invalid input file extension");
                return;
            }

            if (!File.Exists(zipFile))
            {
                Console.Error.WriteLine($"File not found: {zipFile}");
                return;
            }
            if (!File.Exists(jsFile))
            {
                Console.Error.WriteLine($"File not found: {jsFile}");
                return;
            }

            var outFilePath = Path.ChangeExtension(zipFile, "jxl");

            Ugoira ugoira;
            await using (var inFile = new FileStream(jsFile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
                                                     FileOptions.Asynchronous))
            {
                ugoira = await JsonSerializer.DeserializeAsync<Ugoira>(inFile);
            }

            using var outImg = new MagickImageCollection();

            await using (var inFile = new FileStream(zipFile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
                                                     FileOptions.Asynchronous))
            {
                using var zip = new ZipArchive(inFile, ZipArchiveMode.Read, true);
                foreach (var frame in ugoira.Frames)
                {
                    var entry = zip.GetEntry(frame.File);
                    var img = new MagickImage(entry.Open());
                    img.AnimationDelay = frame.Delay;
                    outImg.Add(img);
                }
            }

            await using var outFile = new FileStream(outFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None,
                                                     4096, FileOptions.Asynchronous);
            await outImg.WriteAsync(outFile, MagickFormat.Jxl);
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("AniEnc (c) 2021 Oron Feinerman");
            Console.Error.WriteLine("Usage: anienc <parameters> [infile] <outfile>");
            Console.Error.WriteLine("Implemented: anienc -i:uzj -o:jxl infile.zip");
        }
    }
}
