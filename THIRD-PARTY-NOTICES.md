# Third-party software in Adawat

Adawat itself is licensed under the GNU General Public License v3.0 (see `LICENSE`). The Mac app bundle also
carries two command-line tools, each under its own license. They run as separate programs; Adawat calls them
and does not link to them. The Windows app's components are listed in [Windows](#windows) below.

## FFmpeg 7.1.1 — GPL v3

`Adawat.app/Contents/Resources/Tools/ffmpeg` is a static build of [FFmpeg](https://ffmpeg.org), configured with
`--enable-gpl --enable-version3 --disable-nonfree`, so the binary as a whole is covered by the
[GNU GPL v3](https://www.gnu.org/licenses/gpl-3.0.html). It contains these libraries:

| Library | Version | License | Source |
| --- | --- | --- | --- |
| FFmpeg | 7.1.1 | LGPL 2.1+ / GPL with the options above | https://ffmpeg.org/releases/ffmpeg-7.1.1.tar.xz |
| x264 | stable branch | GPL 2+ | https://code.videolan.org/videolan/x264 |
| x265 | 4.1 | GPL 2+ | https://bitbucket.org/multicoreware/x265_git |
| LAME | 3.100 | LGPL 2+ | https://lame.sourceforge.io |
| Opus | 1.5.2 | BSD 3-Clause | https://opus-codec.org |
| libogg | 1.3.5 | BSD 3-Clause | https://xiph.org/ogg/ |
| libvorbis | 1.3.7 | BSD 3-Clause | https://xiph.org/vorbis/ |
| libvpx | 1.15.0 | BSD 3-Clause | https://chromium.googlesource.com/webm/libvpx |

**Corresponding source.** `scripts/build-ffmpeg.sh` in this repository downloads exactly these sources from the
addresses above and builds the binary that ships. The SHA-256 of each archive used for the release build:

```
733984395e0dbbe5c046abda2dc49a5544e7e0e1e2366bba849222ae9e3a03b1  ffmpeg-7.1.1.tar.xz
740126cb48549ca4e2f09b4ae13beed592d5b54744044e38f28662fd96d0ba56  x264-stable.tar.bz2
a31699c6a89806b74b0151e5e6a7df65de4b49050482fe5ebf8a4379d7af8f29  x265_4.1.tar.gz
ddfe36cab873794038ae2c1210557ad34857a4b6bdc515785d1da9e175b1da1e  lame-3.100.tar.gz
65c1d2f78b9f2fb20082c38cbe47c951ad5839345876e46941612ee87f9a7ce1  opus-1.5.2.tar.gz
0eb4b4b9420a0f51db142ba3f9c64b333f826532dc0f48c6410ae51f4799b664  libogg-1.3.5.tar.gz
0e982409a9c3fc82ee06e08205b1355e5c6aa4c36bca58146ef399621b0ce5ab  libvorbis-1.3.7.tar.gz
e935eded7d81631a538bfae703fd1e293aad1c7fd3407ba00440c95105d2011e  libvpx-1.15.0.tar.gz
```

If you received Adawat as a binary and cannot reach these addresses, open an issue in this repository and the
same archives will be provided.

## cwebp (libwebp 1.6.0) — BSD 3-Clause

`Adawat.app/Contents/Resources/Tools/cwebp` and the libraries next to it in `Tools/lib` come from
[libwebp](https://chromium.googlesource.com/webm/libwebp) and the image libraries it reads with:

| Library | License |
| --- | --- |
| libwebp, libsharpyuv, libwebpdemux | BSD 3-Clause |
| libpng | PNG Reference Library License v2 |
| libjpeg-turbo | IJG License and BSD 3-Clause |
| libtiff | libtiff License (BSD-like) |
| zstd | BSD 3-Clause |
| liblzma (XZ Utils) | 0BSD |

Copyright for each component belongs to its authors. Their full license texts are in the source archives above.

## Windows

The Windows app (`windows/` in this repository) is built with .NET and installed with an Inno Setup installer.
Everything below is redistributable together with Adawat under the GPL v3.

### FFmpeg 8.1 — GPL v3

`tools\ffmpeg.exe` is the `gpl` build of FFmpeg `n8.1.3-18-g46d899d7a0` from
[BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds), release `autobuild-2026-10-18-13-01`. It is configured
with `--enable-gpl --enable-version3` and without nonfree components, so the binary as a whole is covered by the
[GNU GPL v3](https://www.gnu.org/licenses/gpl-3.0.html). Its license text ships next to it as
`tools\ffmpeg-LICENSE.txt`. It runs as a separate program; Adawat calls it and does not link to it.

**Corresponding source.** FFmpeg at commit `46d899d7a0` (https://github.com/FFmpeg/FFmpeg), and the build scripts
that pin every bundled library and its version at the release tag above
(https://github.com/BtbN/FFmpeg-Builds/tree/autobuild-2026-10-18-13-01). `windows/tools/fetch-ffmpeg.sh` downloads
exactly these archives:

```
76f92392602b95351f4f057b8f6d74815e8c6947a2495c55193bd92f0ea7a2bd  ffmpeg-n8.1.3-18-g46d899d7a0-win64-gpl-8.1.zip
e3e07f7ec8589b47764ef2ba20ab336bba210e62346ef6c21cca7e584b402fb1  ffmpeg-n8.1.3-18-g46d899d7a0-winarm64-gpl-8.1.zip
```

If you received Adawat as a binary and cannot reach these addresses, open an issue in this repository and the
same sources will be provided.

### Libraries

| Component | License | Source |
| --- | --- | --- |
| .NET runtime and WPF | MIT | https://github.com/dotnet/runtime, https://github.com/dotnet/wpf |
| Magick.NET 15 (with ImageMagick and its image libraries) | Apache 2.0 (ImageMagick License) | https://github.com/dlemstra/Magick.NET |
| PDFtoImage 5 | MIT | https://github.com/sungaila/PDFtoImage |
| PDFium (bblanchon.PDFium) | Apache 2.0 / BSD 3-Clause | https://github.com/bblanchon/pdfium-binaries |
| SkiaSharp | MIT | https://github.com/mono/SkiaSharp |
| PDFsharp 6.2.4 | MIT | https://github.com/empira/PDFsharp |
| PdfPig | Apache 2.0 | https://github.com/UglyToad/PdfPig |
| HtmlAgilityPack | MIT | https://github.com/zzzprojects/html-agility-pack |
| DocumentFormat.OpenXml | MIT | https://github.com/dotnet/Open-XML-SDK |
| ClosedXML, ClosedXML.Parser | MIT | https://github.com/ClosedXML/ClosedXML |
| ExcelNumberFormat, RBush | MIT | https://github.com/andersnm/ExcelNumberFormat, https://github.com/viceroypenguin/RBush |
| SixLabors.Fonts | Apache 2.0 | https://github.com/SixLabors/Fonts |
| ExcelDataReader | MIT | https://github.com/ExcelDataReader/ExcelDataReader |
| Markdig | BSD 2-Clause | https://github.com/xoofx/markdig |
| SharpCompress | MIT | https://github.com/adamhathcock/sharpcompress |
| QRCoder | MIT | https://github.com/codebude/QRCoder |
| ZXing.Net | Apache 2.0 | https://github.com/micjahn/ZXing.Net |
| H.NotifyIcon | MIT | https://github.com/HavenDV/H.NotifyIcon |
| Microsoft Edge WebView2 SDK | BSD 3-Clause | https://www.nuget.org/packages/Microsoft.Web.WebView2 |
| WinRT.Runtime (C#/WinRT) | MIT | https://github.com/microsoft/CsWinRT |
| Microsoft.Windows.SDK.NET (Windows API projection, a system library) | Windows SDK License | https://aka.ms/WinSDKLicenseURL |

The installer is made with [Inno Setup](https://jrsoftware.org/isinfo.php), whose license allows installers for
any program. Copyright for each component belongs to its authors; their full license texts are in their sources.
