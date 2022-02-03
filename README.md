# AniEnc
Cross-platform tool for converting and resizing animations, powered by [Magick.NET](https://github.com/dlemstra/Magick.NET) and [CliFx](https://github.com/Tyrrrz/CliFx).

## Implemented features

* Conversion (to any format supported by ImageMagick)
* Resizing (px / % / auto, various interpolation algorithms)
* GIF optimization (all options available in Magick.NET)
* Coalesce (by default when resizing)
* Image enhancement

## System requirements
* Any platform supported by both [.NET](https://docs.microsoft.com/en-us/dotnet/core/install/) and [Magick.NET](https://github.com/dlemstra/Magick.NET/#supported-platforms)
  - On x86/x64, Windows/Linux/macOS should all work
  - ARM is not fully supported
* [.NET 6.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
* (optional) FFmpeg installed to `$PATH` 
  - This is required for some formats such as WebM, APNG
  - On Windows, I use [media-autobuild_suite](https://github.com/m-ab-s/media-autobuild_suite) to build FFmpeg

## How to use
Usage:
`dotnet anienc.dll <input> [<options>]`

To convert a GIF to WebM:
`dotnet anienc.dll test.gif -f webm`

To resize a GIF:
`dotnet anienc.dll test.gif -r 640 480 -f gif`

To optimize a GIF:
`dotnet anienc.dll test.gif -p 2 -f gif`

(for single-file builds, you may replace `dotnet anienc.dll` with `anienc` in the above examples)

## Unique features

AniEnc has support for converting Ugoira. These are animations from Pixiv, downloaded with a tool such as [PixivUtil2](https://github.com/nandaka/PixivUtil2). Two files are required, `.zip` and `.zip.js` - specify one of them as the input and the tool will find the other one automatically.

## Future plans

* Better resizing options (fit)
* Noise reduction algorithms
* Extra support for JPEG XL animations (one of the initial goals of this project, as soon as ImageMagick has support)
* Simple timing adjustments
* Cutting
* Cropping
* Converting to/from individual frames
