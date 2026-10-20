package com.motalim.adawat.ops

import android.graphics.Bitmap
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import com.google.zxing.EncodeHintType
import com.google.zxing.WriterException
import com.google.zxing.qrcode.decoder.ErrorCorrectionLevel
import com.google.zxing.qrcode.encoder.Encoder
import com.motalim.adawat.core.AdawatException
import com.motalim.adawat.core.OperationContext
import com.motalim.adawat.core.ext
import java.io.File

/** QR codes from text, links and contact cards. */
object QrCode {
    val fileExtensions = setOf("txt", "md", "markdown", "url", "vcf", "webloc")

    fun canEncode(files: List<File>) = files.size == 1 && files[0].ext in fileExtensions

    /** The text inside a text file, a Windows internet shortcut (.url), a Safari link (.webloc) or a contact card. */
    fun text(file: File): String? = runCatching {
        when (file.ext) {
            "url" -> file.readLines().firstOrNull { it.startsWith("URL=", ignoreCase = true) }?.substring(4)
            "webloc" -> Regex("<key>URL</key>\\s*<string>([^<]*)</string>").find(file.readText())?.groupValues?.get(1)
            else -> file.readText().trim()
        }
    }.getOrNull()

    fun matrix(text: String, level: ErrorCorrectionLevel = ErrorCorrectionLevel.M) = try {
        Encoder.encode(text, level, mapOf(EncodeHintType.CHARACTER_SET to "UTF-8")).matrix
    } catch (_: WriterException) {
        null
    }

    /** Square modules, black on white, with the standard four-module margin. */
    fun render(text: String, size: Int = 1024): Bitmap {
        val matrix = matrix(text) ?: throw AdawatException("This is too long for a QR code. Shorten it or lower the error correction.")
        val modules = matrix.width + 8
        val bitmap = Bitmap.createBitmap(size, size, Bitmap.Config.ARGB_8888)
        val canvas = Canvas(bitmap)
        canvas.drawColor(Color.WHITE)
        val paint = Paint().apply { color = Color.BLACK }
        val unit = size.toFloat() / modules
        for (y in 0 until matrix.height) for (x in 0 until matrix.width) {
            if (matrix.get(x, y).toInt() == 1) {
                // Edges are rounded to whole pixels so neighbouring modules meet without seams.
                val left = Math.round((x + 4) * unit).toFloat()
                val top = Math.round((y + 4) * unit).toFloat()
                canvas.drawRect(left, top, Math.round((x + 5) * unit).toFloat(), Math.round((y + 5) * unit).toFloat(), paint)
            }
        }
        return bitmap
    }

    /** Without the screen: a 1024 px PNG with the default style. */
    suspend fun quick(files: List<File>, context: OperationContext): List<File> {
        val file = files[0]
        val text = text(file)
        if (text.isNullOrEmpty()) throw AdawatException("No text found")
        val output = context.pathFor(file, "png", "QR")
        ImageOps.write(render(text), ImageFormat.Png, output)
        return listOf(output)
    }
}
