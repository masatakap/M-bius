# Third-party components

Source preparation status, identified revisions and remaining work: [Native dependency sources](docs/DEPENDENCY-SOURCES.md). This inventory is incomplete and is not a complete corresponding-source archive.

## libmpv

- Binary: `app/libmpv-2.dll`
- Build: `mpv-dev-x86_64-20260903-git-69e63f425a.7z` (baseline x86_64, not x86_64-v3)
- SHA-256 of DLL: `673E6397920AB64A9C5B3A618F7F16D38854EFE72B58665F1F84E4E873B763A4`
- Download: https://github.com/shinchiro/mpv-winbuild-cmake/releases/download/20260903/mpv-dev-x86_64-20260903-git-69e63f425a.7z
- Build scripts / dependency sources: https://github.com/shinchiro/mpv-winbuild-cmake
- mpv source: https://github.com/mpv-player/mpv
- FFmpeg source: https://git.ffmpeg.org/ffmpeg.git
- Manual: https://mpv.io/manual/stable/

The binary is unmodified. mpv is GPL-2.0-or-later with component-specific exceptions and third-party libraries; see `licenses/mpv-Copyright.txt`, `licenses/GPL.txt`, and the upstream build/dependency sources for applicable terms. This source repository includes links to upstream sources and build scripts, not native DLLs or a complete corresponding-source archive for every static dependency. The locally built installer contains the native DLLs. Prepare the exact corresponding sources and dependency license inventory before public binary redistribution.

## .NET / Windows Forms

The self-contained Windows x64 executable includes .NET 9 runtime components. See `licenses/dotnet-LICENSE.txt` and `licenses/dotnet-THIRD-PARTY-NOTICES.txt`.

- Runtime source: https://github.com/dotnet/runtime
- Windows Forms source: https://github.com/dotnet/winforms

## Application source

Möbius original application source in `source/`, tests in `tests/`, and original build scripts are provided under the MIT License. See `LICENSE`. This does not relicense third-party components or brand assets; see `TRADEMARKS.md`. No warranty is provided.

## FFmpeg shared libraries for Hap demuxing (0.6)

The added Hap reader dynamically links avformat-62.dll, avcodec-62.dll, avutil-60.dll and swresample-6.dll from the BtbN FFmpeg 8.1 win64 LGPL shared build. The baseline DLL hashes are recorded in docs/native-runtime-sha256.json. The binary distribution includes third-party components with their own terms. The original license notice is licenses/ffmpeg-LICENSE.txt.

- Binary provider and build scripts: https://github.com/BtbN/FFmpeg-Builds
- FFmpeg source: https://github.com/FFmpeg/FFmpeg/tree/release/8.1
- Hap format specification and reference implementation: https://github.com/Vidvox/hap
- GPU integration documentation: https://hap.video/developers.html

The C# Hap parsing/Snappy expansion and OpenGL shaders in source are application source; the Shutterstock reference image is not included. hap_demux.c is original application source under the MIT License. The FFmpeg DLLs retain their LGPL and dependency-specific terms. As with the bundled mpv prototype, the complete corresponding-source archive and dependency notice inventory are not bundled; prepare these before public redistribution.

## Inno Setup (installer)

Installer created with Inno Setup 6.7.3. Copyright Jordan Russell and Martijn Laan. See licenses/Inno-Setup-LICENSE.txt. Compiler and source: https://jrsoftware.org/ and https://github.com/jrsoftware/issrc .


## App icon

The app icon is derived by format conversion and transparent-margin fitting from artwork supplied by the user (source/Assets/original.png). The artwork retains its owner's rights; no additional artwork license is granted by the application source license.


Chinese Simplified installer messages are vendored from https://github.com/jrsoftware/issrc/blob/main/Files/Languages/ChineseSimplified.isl . Original translator attribution is retained in installer/Languages/ChineseSimplified.isl; source and checksum are recorded in installer/Languages/README.md.
