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

        [CommandOption("verbose", 'v', Description = "Print verbose information.")]
        public bool Verbose { get; set; }

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
                if (Verbose)
                    await console.Error.WriteLineAsync($"Parsed width string {Resize[0]} as (Value: {resizeWidth}, IsPercent: {isWidthPercent})");
                if (Resize.Length > 1)
                {
                    (resizeHeight, isHeightPercent) = ParseDimension(Resize[1]);
                    if (Verbose)
                        await console.Error.WriteLineAsync($"Parsed height string {Resize[1]} as (Value: {resizeHeight}, IsPercent: {isHeightPercent})");
                }

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

            if (Verbose)
                await console.Error.WriteLineAsync($"Input file: {InputFile}\nOutput file: {OutputFile}");

            var inputFormat = Path.GetExtension(InputFile).TrimStart('.').ToLowerInvariant();

            MagickImageCollection? frames;
            if (inputFormat == "zip" || inputFormat == "js")
            {
                if (Verbose)
                    await console.Error.WriteLineAsync("Detected Ugoira file");

                if (inputFormat == "js")
                {
                    InputFile = InputFile[..^3];
                }
                var jsFile = InputFile + ".js";

                if (Verbose)
                    await console.Error.WriteLineAsync($"Ugoira ZIP: {InputFile}\nUgoira JS: {jsFile}");

                if (!File.Exists(InputFile))
                {
                    throw new CommandException($"File not found: {InputFile}");
                }
                if (!File.Exists(jsFile))
                {
                    throw new CommandException($"File not found: {jsFile}");
                }
                frames = await MagickConverter.ConvertUgoira(InputFile, jsFile);

                if (frames == null)
                {
                    throw new CommandException("Failed to parse input file: Ugoira JS is invalid");
                }
            }
            else
            {
                if (Verbose)
                    await console.Error.WriteLineAsync("Opening input file as image");

                frames = await MagickConverter.OpenFile(InputFile);
            }

            if (frames == null)
            {
                throw new CommandException("Failed to parse input file: Unknown error");
            }

            if (Verbose)
            {
                await console.Output.WriteLineAsync($"Frame count: {frames.Count}");
                await console.Output.WriteLineAsync($"Maximum frame size: {frames.Select(i => (int?)i.Width).Max()}x{frames.Select(i => (int?)i.Height).Max()}");
                var delayEnumerable = frames.Select(i => (int?)i.AnimationDelay);
                var tpsEnumerable = frames.Select(i => (int?)i.AnimationTicksPerSecond);
                await console.Output.WriteLineAsync($"Animation delay: {delayEnumerable.Min()} to {delayEnumerable.Max()} ({tpsEnumerable.Min()} to {tpsEnumerable.Max()} ticks per second)");
            }

            if (Coalesce || Resize != null)
            {
                if (Verbose)
                    await console.Error.WriteLineAsync("Coalesce");

                frames.Coalesce();
            }

            if (ReduceNoise)
            {
                var i = 0;
                foreach (var frame in frames)
                {
                    if (Verbose)
                        await console.Error.WriteLineAsync($"Reducing noise in frame #{++i}");
                    frame.ReduceNoise();
                }
            }

            if (Resize != null)
            {
                var i = 0;
                foreach (var frame in frames)
                {
                    if (Verbose)
                        await console.Error.WriteLineAsync($"Resizing frame #{++i}");

                    if (double.IsNaN(resizeWidth) && !double.IsNaN(resizeHeight))
                    {
                        if (isHeightPercent)
                        {
                            resizeWidth = resizeHeight;
                        }
                        else
                        {
                            var aspect = (double)frame.Width / frame.Height;
                            resizeWidth = resizeHeight * aspect;
                        }
                        isWidthPercent = isHeightPercent;
                    }
                    else if (!double.IsNaN(resizeWidth) && double.IsNaN(resizeHeight))
                    {
                        if (isWidthPercent)
                        {
                            resizeWidth = resizeHeight;
                        }
                        else
                        {
                            var aspect = (double)frame.Height / frame.Width;
                            resizeHeight = resizeWidth * aspect;
                        }
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

                    if (Verbose)
                        await console.Error.WriteLineAsync($"Frame #{i} will be resized from {frame.Width}x{frame.Height} to {width}x{height} with method {PixelInterpolateMethod}");

                    frame.InterpolativeResize(width, height, PixelInterpolateMethod);
                }
            }

            if (OptimizeLevel > 0)
            {
                if (OptimizeLevel == 1)
                {
                    if (Verbose)
                        await console.Error.WriteLineAsync("Optimize");
                    frames.Optimize();
                }
                else if (OptimizeLevel == 2)
                {
                    if (Verbose)
                        await console.Error.WriteLineAsync("OptimizePlus");
                    frames.OptimizePlus();
                }
                else if (OptimizeLevel == 3)
                {
                    if (Verbose)
                        await console.Error.WriteLineAsync("OptimizeTransparency");
                    frames.OptimizeTransparency();
                }
                else if (OptimizeLevel == 4)
                {
                    if (Verbose)
                        await console.Error.WriteLineAsync("Optimize");
                    frames.Optimize();
                    if (Verbose)
                        await console.Error.WriteLineAsync("OptimizeTransparency");
                    frames.OptimizeTransparency();
                }
                else if (OptimizeLevel == 5)
                {
                    if (Verbose)
                        await console.Error.WriteLineAsync("OptimizePlus");
                    frames.OptimizePlus();
                    if (Verbose)
                        await console.Error.WriteLineAsync("OptimizeTransparency");
                    frames.OptimizeTransparency();
                }
            }

            if (Verbose)
                await console.Error.WriteLineAsync("Writing output file");

            await MagickConverter.WriteOutputFile(frames, OutputFile, OutputFormat);

            if (Verbose)
                await console.Error.WriteLineAsync("Done");

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
