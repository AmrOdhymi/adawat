# Third-party software in Adawat

Adawat itself is licensed under the GNU General Public License v3.0 (see `LICENSE`). The app bundle also
carries two command-line tools, each under its own license. They run as separate programs; Adawat calls them
and does not link to them.

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
