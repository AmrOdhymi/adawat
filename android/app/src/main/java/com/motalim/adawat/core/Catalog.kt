package com.motalim.adawat.core

import com.motalim.adawat.media.AudioOps
import com.motalim.adawat.media.MediaFormatChoice
import com.motalim.adawat.media.VideoOps
import com.motalim.adawat.ops.Adjustments
import com.motalim.adawat.ops.FileActions
import com.motalim.adawat.ops.PhotoMetadata
import com.motalim.adawat.ops.ImageFormat
import com.motalim.adawat.ops.ImageOps
import com.motalim.adawat.ops.ImageTools
import com.motalim.adawat.ops.PdfOps
import com.motalim.adawat.ops.QrCode
import java.io.File

/** Decides which wheel choices fit the shared files. Mirrors the desktop catalog, choice for choice, as far as Android can go. */
@androidx.annotation.OptIn(androidx.media3.common.util.UnstableApi::class)
object Catalog {
    /** Material Icons Round code points, matched to the Segoe icons the Windows app draws. */
    object Icons {
        const val Photo = ''; const val Document = ''; const val Film = ''; const val Audio = ''
        const val Archive = ''; const val Compress = ''; const val Metadata = ''; const val Edit = ''
        const val Annotate = ''; const val Background = ''; const val Crop = ''; const val Redact = ''
        const val Rotate = ''; const val Text = ''; const val Qr = ''; const val Pdf = ''
        const val Collage = ''; const val Merge = ''; const val Organize = ''; const val Split = ''
        const val Trim = ''; const val Speed = ''; const val Camera = ''; const val Join = ''
        const val Mute = ''; const val Volume = ''; const val Waveform = ''; const val Channels = ''
        const val Bleep = ''; const val Silence = ''; const val Share = ''; const val Copy = ''
        const val Save = ''; const val OpenWith = ''; const val Info = ''; const val Print = ''
        const val Extract = ''; const val Folder = ''
    }

    fun forMode(mode: WheelMode, files: List<File>): List<Operation> {
        if (files.isEmpty()) return emptyList()
        return when (mode) {
            WheelMode.Convert -> conversions(files)
            WheelMode.Tools -> tools(files)
            WheelMode.Actions -> actions(files)
        }
    }

    fun find(id: String, files: List<File>): Operation? =
        WheelMode.entries.asSequence().flatMap { forMode(it, files) }.firstOrNull { it.id == id }

    // Convert

    private fun conversions(files: List<File>): List<Operation> = when (files.commonKind()) {
        FileKind.Image -> {
            val sources = files.map { ImageFormat.of(it) }.toSet()
            val ops = ImageFormat.all
                .filter { format -> !(sources.size == 1 && format in sources) && format.canWrite }
                .map { format ->
                    Operation("image.${format.ext}", "To ${format.title}", Icons.Photo, { f, c -> ImageOps.convert(f, format, c) }, badge = format.title)
                }
            ops + Operation("image.pdf", "To PDF", Icons.Pdf, ImageOps::makePdf, badge = "PDF")
        }
        FileKind.Pdf -> listOf(
            Operation("pdf.md", "To Markdown", Icons.Document, PdfOps::toMarkdown, badge = "MD"),
            Operation("pdf.png", "Pages to PNG", Icons.Photo, { f, c -> PdfOps.pagesToPictures(f, ImageFormat.Png, c) }, badge = "PNG"),
            Operation("pdf.jpg", "Pages to JPEG", Icons.Photo, { f, c -> PdfOps.pagesToPictures(f, ImageFormat.Jpeg, c) }, badge = "JPG"),
            Operation("pdf.txt", "To Text", Icons.Text, PdfOps::toText, badge = "TXT"),
        )
        FileKind.Video -> MediaFormatChoice.video.filter { it.ext != files[0].ext || files.size > 1 }.map(::mediaConversion)
        FileKind.Audio -> MediaFormatChoice.audio.filter { it.ext != files[0].ext || files.size > 1 }.map(::mediaConversion)
        else -> emptyList()
    }

    private fun mediaConversion(format: MediaFormatChoice) =
        Operation("media.${format.ext}", "To ${format.title}", if (format.audioOnly) Icons.Audio else Icons.Film,
                  { f, c -> VideoOps.convert(f, format, c) }, badge = format.title)

    // Tools

    private fun tool(id: String, title: String, short: String?, icon: Char, run: OperationWork, window: String? = null,
                     producesFiles: Boolean = true) = Operation(id, title, icon, run, short = short, window = window, producesFiles = producesFiles)

    private fun tools(files: List<File>): List<Operation> {
        val kinds = files.kinds()
        val single = files.size == 1
        val ops = mutableListOf<Operation>()
        if (kinds == setOf(FileKind.Image)) {
            ops += tool("image.compress", "Compress", null, Icons.Compress, { f, c -> ImageTools.compress(f, c) }, "compress")
            ops += tool("image.metadata", "Edit Metadata", "Metadata", Icons.Metadata, PhotoMetadata::save, if (single) "metadata" else null)
            if (single) ops += tool("image.edit", "Edit Photo", "Edit", Icons.Edit, Adjustments::save, "edit")
            ops += tool("image.frame", "Add Background", "Add BG", Icons.Background, { f, c -> ImageTools.addBackground(f, c) }, "frame")
            ops += tool("image.crop", "Crop", null, Icons.Crop, ImageTools::cropTool, "crop")
            if (single) ops += tool("image.redact", "Redact", null, Icons.Redact, ImageTools::redact, "redact")
            ops += tool("image.rotate", "Rotate", null, Icons.Rotate, ImageTools::rotate)
            ops += tool("image.ocr", "Copy Text", "Text", Icons.Text, ImageTools::extractText)
            ops += tool("image.qr", "Read QR Code", "QR Code", Icons.Qr, ImageTools::readCodes, producesFiles = false)
            if (!single) {
                ops += tool("image.topdf", "Create PDF", "PDF", Icons.Pdf, ImageOps::makePdf)
                ops += tool("image.collage", "Create Collage", "Collage", Icons.Collage, { f, c -> ImageTools.makeCollage(f, c) }, "collage")
            }
        }

        if (kinds == setOf(FileKind.Pdf)) {
            ops += tool("pdf.compress", "Compress", null, Icons.Compress, { f, c -> PdfOps.compress(f, c) })
            ops += tool("pdf.metadata", "Remove Metadata", "Metadata", Icons.Metadata, PdfOps::stripMetadata)
            if (!single) ops += tool("pdf.merge", "Merge PDFs", "Merge", Icons.Merge, PdfOps::merge)
            ops += tool("pdf.split", "Split Pages", "Split", Icons.Split, PdfOps::split)
            ops += tool("pdf.ocr", "Copy Text", "Text", Icons.Text, ImageTools::extractText)
        }

        if (kinds == setOf(FileKind.Video)) {
            ops += tool("video.compress", "Compress", null, Icons.Compress, { f, c -> VideoOps.compress(f, c) }, "compress")
            ops += tool("video.metadata", "Remove Metadata", "Metadata", Icons.Metadata, AudioOps::stripMetadata)
            if (!single) ops += tool("video.join", "Join Videos", "Join", Icons.Join, VideoOps::join)
            ops += tool("video.rotate", "Rotate", null, Icons.Rotate, VideoOps::rotate)
            ops += tool("video.mute", "Mute", null, Icons.Mute, VideoOps::mute)
            ops += tool("video.normalize", "Normalize Volume", "Normalize", Icons.Volume, VideoOps::normalize)
        }

        if (kinds == setOf(FileKind.Audio)) {
            ops += tool("audio.compress", "Compress", null, Icons.Compress, { f, c -> AudioOps.compress(f, c) }, "compress")
            ops += tool("audio.metadata", "Remove Metadata", "Metadata", Icons.Metadata, AudioOps::stripMetadata)
            ops += tool("audio.normalize", "Normalize Volume", "Normalize", Icons.Volume, AudioOps::normalize)
            if (single) {
                ops += tool("audio.channels", "Convert Channels", "Channels", Icons.Channels, AudioOps::toMono, "channels")
            } else {
                ops += tool("audio.silence", "Trim Silence", "Silence", Icons.Silence, AudioOps::trimSilence)
                ops += tool("audio.mono", "To Mono", "Mono", Icons.Channels, AudioOps::toMono)
            }
        }

        if (QrCode.canEncode(files)) ops += tool("text.qr", "Create QR Code", "Make QR", Icons.Qr, QrCode::quick, "qr")

        return ops
    }

    // Actions

    /** The same actions for any file, clockwise from the top. The app performs them. */
    private fun actions(files: List<File>): List<Operation> = buildList {
        add(tool("action.share", "Share", null, Icons.Share, FileActions.Share, producesFiles = false))
        add(tool("action.open", "Open With", "Open", Icons.OpenWith, FileActions.OpenWith, producesFiles = false))
        add(tool("action.copy", "Copy", null, Icons.Copy, FileActions.Copy, producesFiles = false))
        add(tool("action.save", "Save To", "Save", Icons.Save, FileActions.SaveTo, producesFiles = false))
        if (files.kinds().all { it == FileKind.Image || it == FileKind.Pdf }) {
            add(tool("action.print", "Print", null, Icons.Print, FileActions.Print, producesFiles = false))
        }
        add(tool("action.details", "Details", null, Icons.Info, FileActions.Details, producesFiles = false))
    }
}
