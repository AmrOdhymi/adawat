package com.motalim.adawat.ops

import android.graphics.Bitmap
import android.graphics.Color
import android.graphics.pdf.PdfRenderer
import android.os.ParcelFileDescriptor
import com.motalim.adawat.core.OperationContext
import kotlinx.coroutines.ensureActive
import java.io.File
import kotlin.coroutines.coroutineContext

/** Text in a PDF, read from its pages as pictures. */
object PdfText {
    suspend fun read(file: File, context: OperationContext): String {
        ParcelFileDescriptor.open(file, ParcelFileDescriptor.MODE_READ_ONLY).use { descriptor ->
            PdfRenderer(descriptor).use { renderer ->
                val pages = mutableListOf<String>()
                for (index in 0 until renderer.pageCount) {
                    coroutineContext.ensureActive()
                    context.report("Reading page ${index + 1} of ${renderer.pageCount}")
                    renderer.openPage(index).use { page ->
                        // About 200 dpi, enough for small print.
                        val scale = 200f / 72f
                        val bitmap = Bitmap.createBitmap((page.width * scale).toInt(), (page.height * scale).toInt(), Bitmap.Config.ARGB_8888)
                        bitmap.eraseColor(Color.WHITE)
                        page.render(bitmap, null, null, PdfRenderer.Page.RENDER_MODE_FOR_PRINT)
                        pages += Recognition.text(bitmap, context.progressFor(index, renderer.pageCount)).trim()
                        bitmap.recycle()
                    }
                }
                return pages.filter { it.isNotEmpty() }.joinToString("\n\n")
            }
        }
    }
}
