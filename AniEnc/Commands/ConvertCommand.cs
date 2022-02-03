using CliFx;
using CliFx.Attributes;
using CliFx.Exceptions;
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

        [CommandOption("resize", 'r', Description = "Dimensions to resize to (width height). Units supported: px (default), %, auto. If only the width is specified, the height will be set to auto. Examples: -r 640 480, -r 200%, -r auto 768")]
        public string[]? Resize { get; set; } = null;

        [CommandOption("interpolate", 'i', Description = "Resize type (default nearest).")]
        public PixelInterpolateMethod PixelInterpolateMethod { get; set; } = PixelInterpolateMethod.Nearest;

        [CommandOption("optimize", 'p', Description = "Optimize GIF and similar formats. 0 = off (default), 1 = normal, 2 = plus, 3 = transparency, 4 = 1 + 3, 5 = 2 + 3.")]
        public int OptimizeLevel { get; set; }

        [CommandOption("overwrite", 'y', Description = "Set this flag to ignore prompts about overwriting existing files.")]
        public bool Overwrite { get; set; }

        public async ValueTask ExecuteAsync(IConsole console)
        {
            // NaN is auto
            double resizeWidth = double.NaN, resizeHeight = double.NaN;
            bool isWidthPercent = false, isHeightPercent = false;
            if (Resize != null)
            {
                if (Resize.Length != 1 && Resize.Length != 2)
                {
                    throw new CommandException("Invalid number of parameters for resize option, must be 2 parameters: width height.");
                }

                (resizeWidth, isWidthPercent) = ParseDimension(Resize[0]);
                if (Resize.Length > 1)
                    (resizeHeight, isHeightPercent) = ParseDimension(Resize[1]);

                if (double.IsNaN(resizeWidth) && double.IsNaN(resizeHeight))
                {
                    throw new CommandException("Invalid resize, at least one dimension must be non-auto.");
                }
            }

            if (!File.Exists(InputFile))
            {
                throw new CommandException($"File not found: {InputFile}");
            }

            OutputFile ??= Path.ChangeExtension(InputFile, OutputFormat.ToString().ToLowerInvariant());
            if (File.Exists(OutputFile))
            {
                await console.Error.WriteAsync($"File exists: {OutputFile}\nOverwrite? (Y/N) ");
#pragma warning disable CliFx_SystemConsoleShouldBeAvoided // Avoid calling `System.Console` where `CliFx.Infrastructure.IConsole` is available
                var key = Console.ReadKey();
#pragma warning restore CliFx_SystemConsoleShouldBeAvoided // Avoid calling `System.Console` where `CliFx.Infrastructure.IConsole` is available
                if (char.ToLowerInvariant(key.KeyChar) != 'y')
                {
                    return;
                }
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
                    throw new CommandException($"File not found: {InputFile}");
                }
                if (!File.Exists(jsFile))
                {
                    throw new CommandException($"File not found: {jsFile}");
                }
                frames = await MagickConverter.ConvertUgoira(InputFile, jsFile);
            }
            else
            {
                frames = await MagickConverter.OpenFile(InputFile);
            }
            if (frames == null)
            {
                throw new CommandException("Failed to parse input file (Ugoira JS)");
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
                    if (double.IsNaN(resizeWidth) && !double.IsNaN(resizeHeight))
                    {
                        var aspect = (double)frame.Width / frame.Height;
                        resizeWidth = resizeHeight * aspect;
                        isWidthPercent = isHeightPercent;
                    }
                    else if (!double.IsNaN(resizeWidth) && double.IsNaN(resizeHeight))
                    {
                        var aspect = (double)frame.Height / frame.Width;
                        resizeHeight = resizeWidth * aspect;
                        isHeightPercent = isWidthPercent;
                    }
                    var width = (int)Math.Round(resizeWidth);
                    var height = (int)Math.Round(resizeHeight);
                    if (isWidthPercent)
                    {
                        width = (int)Math.Round(frame.Width * resizeWidth / 100.0);
                    }
                    if (isHeightPercent)
                    {
                        height = (int)Math.Round(frame.Height * resizeHeight / 100.0);
                    }

                    frame.InterpolativeResize(width, height, PixelInterpolateMethod);
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

            await MagickConverter.WriteOutputFile(frames, OutputFile, OutputFormat);

            frames.Dispose();
        }

        private (double dimension, bool isPercent) ParseDimension(string v)
        {
            if (string.Equals(v, "auto", StringComparison.OrdinalIgnoreCase) || string.Equals(v, "a", StringComparison.OrdinalIgnoreCase))
                return (double.NaN, false);
            var isPercent = v.EndsWith("%", StringComparison.OrdinalIgnoreCase);
            if (isPercent)
                v = v[..^1];
            else if (v.EndsWith("px", StringComparison.OrdinalIgnoreCase))
                v = v[..^2];
            return (double.Parse(v), isPercent);
        }
    }
}
