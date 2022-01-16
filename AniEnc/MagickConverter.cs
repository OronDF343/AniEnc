using System.IO.Compression;
using System.Text.Json;
using ImageMagick;

namespace AniEnc
{
    public static class MagickConverter
    {
        public static async Task<MagickImageCollection> OpenFile(string inputFile)
        {
            await using var inFile = new FileStream(inputFile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
                                                         FileOptions.Asynchronous);
            return new MagickImageCollection(inFile);
        }

        public static async Task<MagickImageCollection?> ConvertUgoira(string zipFile, string jsFile)
        {
            if (!File.Exists(zipFile))
            {
                Console.Error.WriteLine($"File not found: {zipFile}");
                return null;
            }
            if (!File.Exists(jsFile))
            {
                Console.Error.WriteLine($"File not found: {jsFile}");
                return null;
            }

            Ugoira? ugoira;
            await using (var inFile = new FileStream(jsFile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
                                                     FileOptions.Asynchronous))
            {
                ugoira = await JsonSerializer.DeserializeAsync<Ugoira>(inFile);
            }

            if (ugoira == null)
            {
                Console.Error.WriteLine($"Failed to parse JSON: {jsFile}");
                return null;
            }

            var outImg = new MagickImageCollection();

            await using (var inFile = new FileStream(zipFile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
                                                     FileOptions.Asynchronous))
            {
                using var zip = new ZipArchive(inFile, ZipArchiveMode.Read, true);
                foreach (var frame in ugoira.Frames)
                {
                    if (frame.File != null)
                    {
                        var entry = zip.GetEntry(frame.File);
                        if (entry != null)
                        {
                            var img = new MagickImage(entry.Open());
                            img.AnimationDelay = frame.Delay;
                            img.AnimationTicksPerSecond = 1000;
                            outImg.Add(img);
                        }
                    }
                }
            }

            return outImg;
        }

        public static async Task WriteOutputFile(MagickImageCollection images, string outputPath, MagickFormat format)
        {
            await using var outFile = new FileStream(outputPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None,
                                                     4096, FileOptions.Asynchronous);
            await images.WriteAsync(outFile, format);
        }
    }
}
