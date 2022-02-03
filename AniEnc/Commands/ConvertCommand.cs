using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using ImageMagick;

namespace AniEnc.Commands
{
    [Command(Description = "Converts an animation to another format")]
    public class ConvertCommand : ICommand
    {
        [CommandParameter(0, Name = "input", IsRequired = true, Description = "The input file.")]
        public string InputFile { get; set; } = "";

        [CommandOption("format", 'f', IsRequired = true, Description = "The output format (jxl/gif/mng/apng).")]
        public MagickFormat OutputFormat { get; set; }

        [CommandOption("output", 'o', Description = "The output file name (default the is same as the input file name).")]
        public string? OutputFile { get; set; }

        [CommandOption("coalesce", 'c', Description = "Coalesces the animation before processing. Disabled by default, but always enabled if resizing.")]
        public bool Coalesce { get; set; }

        [CommandOption("noise", 'n', Description = "Noise reduction.")]
        public bool ReduceNoise { get; set; }

        [CommandOption("resize", 'r', Description = "Dimensions to resize to.")]
        public int[]? Resize { get; set; }

        [CommandOption("interpolate", 'i', Description = "Resize type (default nearest).")]
        public PixelInterpolateMethod PixelInterpolateMethod { get; set; } = ImageMagick.PixelInterpolateMethod.Nearest;

        [CommandOption("optimize", 'p', Description = "Optimize GIF and similar formats. 0 = off (default), 1 = normal, 2 = plus, 3 = transparency, 4 = 1 + 3, 5 = 2 + 3.")]
        public int OptimizeLevel { get; set; }

        public async ValueTask ExecuteAsync(IConsole console)
        {
            if (Resize != null && Resize.Length != 2)
            {
                await console.Error.WriteLineAsync("Invalid number of parameters for resize option, must be 2 parameters: width, height");
                return;
            }

            if (!File.Exists(InputFile))
            {
                await console.Error.WriteLineAsync($"File not found: {InputFile}");
                return;
            }

            var inputFormat = Path.GetExtension(InputFile).TrimStart('.').ToLowerInvariant();

            MagickImageCollection? frames;
            if (inputFormat == "zip" || inputFormat == "js")
            {
                if (inputFormat == "js")
                {
                    InputFile = InputFile[..^3];
                }
                var jsFile = InputFile + ".js";

                if (!File.Exists(InputFile))
                {
                    await console.Error.WriteLineAsync($"File not found: {InputFile}");
                    return;
                }
                if (!File.Exists(jsFile))
                {
                    await console.Error.WriteLineAsync($"File not found: {jsFile}");
                    return;
                }
                frames = await MagickConverter.ConvertUgoira(InputFile, jsFile);
            }
            else
            {
                frames = await MagickConverter.OpenFile(InputFile);
            }
            if (frames == null)
            {
                await console.Error.WriteLineAsync("Failed to parse input file (Ugoira JS)");
                return;
            }

            await console.Output.WriteLineAsync($"Count:{frames.Count}");
            await console.Output.WriteLineAsync($"Size0:{frames[0].Width}x{frames[0].Height}");
            await console.Output.WriteLineAsync($"Delay0:{frames[0].AnimationDelay}");

            if (Coalesce || Resize != null)
            {
                frames.Coalesce();
            }

            if (ReduceNoise)
            {
                foreach (var frame in frames)
                {
                    frame.ReduceNoise();
                }
            }

            if (Resize != null)
            {
                foreach (var frame in frames)
                {
                    frame.InterpolativeResize(Resize[0], Resize[1], PixelInterpolateMethod);
                }
            }

            if (OptimizeLevel > 0)
            {
                if (OptimizeLevel == 1)
                {
                    frames.Optimize();
                }
                else if (OptimizeLevel == 2)
                {
                    frames.OptimizePlus();
                }
                else if (OptimizeLevel == 3)
                {
                    frames.OptimizeTransparency();
                }
                else if (OptimizeLevel == 4)
                {
                    frames.Optimize();
                    frames.OptimizeTransparency();
                }
                else if (OptimizeLevel == 5)
                {
                    frames.OptimizePlus();
                    frames.OptimizeTransparency();
                }
            }

            await MagickConverter.WriteOutputFile(frames, OutputFile ?? Path.ChangeExtension(InputFile, OutputFormat.ToString().ToLowerInvariant()), OutputFormat);

            frames.Dispose();
        }
    }
}
