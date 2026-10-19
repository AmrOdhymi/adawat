<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/media/banner-dark.png">
    <img src="docs/media/banner-light.png" alt="Adawat: convert, compress and edit any file while you drag it" width="100%">
  </picture>
</p>

<p align="center">
  <b>English</b> · <a href="README.ar.md">العربية</a>
</p>

<p align="center">
  <a href="https://github.com/motalim-ai/adawat/releases/latest"><img alt="Download" src="https://img.shields.io/github/v/release/motalim-ai/adawat?label=Download&color=5B47E6&style=for-the-badge"></a>
  <img alt="macOS 14+" src="https://img.shields.io/badge/macOS-14%2B-111?style=for-the-badge&logo=apple">
  <a href="LICENSE"><img alt="License: GPL v3" src="https://img.shields.io/badge/License-GPL%20v3-7CF2C8?style=for-the-badge"></a>
</p>

<p align="center">
  <a href="https://www.youtube.com/@motalim-ai"><img alt="YouTube: Motalim" src="https://img.shields.io/badge/YouTube-Motalim-FF0000?style=flat-square&logo=youtube&logoColor=white"></a>
  <a href="https://by-wow.com"><img alt="by-wow.com" src="https://img.shields.io/badge/Website-by--wow.com-5B47E6?style=flat-square&logo=safari&logoColor=white"></a>
</p>

---

**Adawat** (أدوات, "tools") lives in your Mac's menu bar. Start dragging any file, press a key, and a wheel of
choices opens right under the pointer. Drop the file on a choice and it is done: a new format, a smaller size,
an edited copy. No upload, no account, no limits. Everything happens on your Mac.

<p align="center">
  <img src="docs/media/demo-en.gif" alt="Dragging a photo, pressing Shift, and dropping it on WEBP" width="760">
</p>

## How it works

1. **Drag** one or more files from Finder, the desktop, or any app.
2. **While dragging, press a key** to open its wheel. The wheel stays open until you drop.
3. **Drop** on a choice. The result is saved next to the original (or in Downloads or on the Desktop, as you prefer).

| Key | Wheel | What's in it |
| :---: | --- | --- |
| <kbd>⇧ Shift</kbd> | **Convert** | Every format the file can become |
| <kbd>⌘ Command</kbd> | **Tools** | Compress, edit, crop, trim, organize, and more for that kind of file |
| <kbd>⌃ Control</kbd> | **Actions** | AirDrop, Float, Copy, Move, Copy Path, open in Terminal |

The keys can be changed in Settings. Tools that need choices (Compress, Crop, Edit…) open a small window first;
the rest run as soon as you drop.

<table>
  <tr>
    <td align="center" width="50%"><img src="docs/media/screens/en-wheel-convert.png" alt="Convert wheel"><br><sub>The Convert wheel for a photo</sub></td>
    <td align="center" width="50%"><img src="docs/media/screens/en-wheel-tools.png" alt="Tools wheel"><br><sub>The Tools wheel for a photo</sub></td>
  </tr>
</table>

## What it can do

### Convert

| File | Becomes |
| --- | --- |
| **Images** (JPEG, PNG, HEIC, WebP, AVIF, TIFF, GIF, BMP, ICO, RAW…) | PNG · JPEG · HEIC · WebP · AVIF · TIFF · GIF · BMP · ICO · PDF, keeping HDR and color profiles |
| **Video** (MP4, MOV, MKV, WebM, AVI…) | MP4 · MOV · MKV · WebM · GIF · MP3 · M4A, copying streams without re-encoding when possible |
| **Audio** (MP3, M4A, WAV, FLAC, AIFF, OGG, Opus…) | MP3 · M4A · WAV · FLAC · AIFF · OGG · Opus |
| **PDF** | Word · Pages · PowerPoint · EPUB · Markdown · Text · PNG · JPEG |
| **Documents** (Word, Pages, RTF, HTML, Markdown, EPUB, ODT, Text) | PDF · Word · Pages · EPUB · Markdown · RTF · HTML · ODT · Text |
| **Spreadsheets** (Excel, Numbers, ODS, CSV, TSV) | Excel · ODS · CSV · TSV · PDF · HTML |
| **Presentations** (PowerPoint, Keynote, ODP) | PDF · Keynote · PNG · JPEG |
| **Subtitles** (SRT, VTT) | SRT · VTT · Text |
| **Archives** (ZIP, TAR, TGZ, GZ, RAR, 7Z…) | ZIP · TAR · TGZ, or extract |

### Tools

- **Photos:** Compress (to a preset, a target size, or a percentage) · Edit (light, color, detail, effects) ·
  Annotate (arrows, text, shapes, highlights, numbered callouts, magnifier) · Add Background · Crop · Redact
  (detects faces and text) · Rotate · Copy Text (OCR, Arabic included) · Read QR Code · Edit or remove metadata ·
  Create PDF · Create Collage
- **PDF:** Compress · Merge · Organize pages (reorder, rotate, duplicate, delete) · Split · Copy Text (OCR) ·
  Remove metadata
- **Video:** Compress · Trim · Crop · Change speed · Save frames · Split into clips · Redact areas over time ·
  Join · Rotate · Mute · Normalize volume · Remove metadata
- **Audio:** Compress · Trim · Trim silence · Normalize loudness (two-pass, EBU R128) · Bleep sections ·
  Mono / stereo channels · Audio visualizer video · Remove metadata
- **QR codes:** create codes for text, links and Wi-Fi, with six dot shapes, four corner styles, gradients, a
  logo in the middle, and a live check that the code still scans. Exports PNG or SVG.

### Float

A small floating shelf for files. Drop files on it to keep them at hand, drag them out to any app, or attach it
to the edge of the screen. Results can open in a Float automatically.

<table>
  <tr>
    <td align="center" width="33%"><img src="docs/media/screens/en-compress.png" alt="Compress"><br><sub>Compress</sub></td>
    <td align="center" width="33%"><img src="docs/media/screens/en-edit.png" alt="Edit Photo"><br><sub>Edit Photo</sub></td>
    <td align="center" width="33%"><img src="docs/media/screens/en-annotate.png" alt="Annotate"><br><sub>Annotate</sub></td>
  </tr>
  <tr>
    <td align="center"><img src="docs/media/screens/en-frame.png" alt="Add Background"><br><sub>Add Background</sub></td>
    <td align="center"><img src="docs/media/screens/en-metadata.png" alt="Metadata"><br><sub>Metadata</sub></td>
    <td align="center"><img src="docs/media/screens/en-bleep.png" alt="Bleep Audio"><br><sub>Bleep Audio</sub></td>
  </tr>
  <tr>
    <td align="center" colspan="2"><img src="docs/media/screens/en-qr.png" alt="Create QR Code"><br><sub>Create QR Code</sub></td>
    <td align="center"><img src="docs/media/screens/en-welcome1.png" alt="Welcome guide"><br><sub>The welcome guide</sub></td>
  </tr>
</table>

## Arabic and English

The whole interface is available in Arabic, right to left, and in English. Adawat follows your Mac's language
on first launch, and you can switch at any time in Settings.

## Install

1. Download **Adawat.dmg** from the [latest release](https://github.com/motalim-ai/adawat/releases/latest).
2. Open it and drag **Adawat** into **Applications**.
3. The first time, open it with **right-click → Open**, then **Open** again.
   This build is not notarized by Apple, so macOS asks once. If it still refuses, go to
   **System Settings → Privacy & Security** and press **Open Anyway**.
4. Look for the wheel icon in the menu bar. The welcome guide lets you practice once.

Requires macOS 14 Sonoma or later on Apple silicon.

## Build from source

You need Xcode 15.3 or later (or its command line tools), plus `cmake` and `pkgconf` for the bundled ffmpeg.

```bash
git clone https://github.com/motalim-ai/adawat.git
cd adawat
brew install cmake pkgconf
scripts/build-ffmpeg.sh            # builds the free, redistributable ffmpeg into build/ffmpeg (takes a few minutes)
scripts/build-app.sh --install     # builds build/Adawat.app and copies it to /Applications
```

`scripts/build-app.sh --dmg` also makes `build/Adawat-<version>.dmg`. The app runs without Homebrew: ffmpeg and
cwebp travel inside the app bundle.

<details>
<summary>Project layout</summary>

| Path | What's there |
| --- | --- |
| `Sources/Adawat/App` | Menu bar, settings, welcome guide, command-line test modes |
| `Sources/Adawat/Wheel` | Drag watching and the radial wheel |
| `Sources/Adawat/Operations` | Every conversion and tool: images, media, PDF, documents, sheets, slides, archives, QR |
| `Sources/Adawat/Windows` | The option windows for tools |
| `Sources/Adawat/Float` | The Float shelf |
| `Resources` | App icon and translations (`ar.lproj`) |
| `scripts` | Build, ffmpeg, icon, banner, translation and screenshot scripts |

</details>

## Privacy

Adawat never sends your files anywhere. It has no analytics, no account and no network features. Pages, Keynote
and Numbers are asked (with your permission) only when you convert their formats.

## License

Adawat is free software under the [GNU General Public License v3.0](LICENSE): you can use it, study it, share it
and change it. If you distribute a modified version, it has to stay free under the same license.
The bundled ffmpeg and cwebp keep their own licenses; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

The name **Adawat** and its icon identify this project and are not covered by the license.

## Made by Motalim

<p>
  <a href="https://www.youtube.com/@motalim-ai"><b>Motalim on YouTube</b></a> ·
  <a href="https://by-wow.com"><b>by-wow.com</b></a>
</p>

Ideas, bugs or translations? [Open an issue](https://github.com/motalim-ai/adawat/issues).
