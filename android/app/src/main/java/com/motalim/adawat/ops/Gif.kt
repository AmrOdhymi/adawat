package com.motalim.adawat.ops

import android.graphics.Bitmap
import android.graphics.Color
import java.io.ByteArrayOutputStream
import java.io.File

/** A still GIF: the picture reduced to 256 colours by median cut, with transparency kept as one colour. */
object Gif {
    fun write(bitmap: Bitmap, file: File) {
        val w = bitmap.width
        val h = bitmap.height
        require(w <= 65535 && h <= 65535)
        val pixels = IntArray(w * h).also { bitmap.getPixels(it, 0, w, 0, 0, w, h) }
        val transparent = pixels.any { Color.alpha(it) < 128 }
        val palette = palette(pixels, if (transparent) 255 else 256)
        val offset = if (transparent) 1 else 0
        // Nearest palette entry for every 15-bit colour, filled as colours are met.
        val lookup = IntArray(32768) { -1 }
        val indices = ByteArray(w * h)
        for (i in pixels.indices) {
            val p = pixels[i]
            if (transparent && Color.alpha(p) < 128) { indices[i] = 0; continue }
            val key = ((p shr 9) and 0x7C00) or ((p shr 6) and 0x3E0) or ((p shr 3) and 0x1F)
            var index = lookup[key]
            if (index < 0) { index = nearest(palette, p); lookup[key] = index }
            indices[i] = (index + offset).toByte()
        }
        val out = ByteArrayOutputStream()
        out.write("GIF89a".toByteArray(Charsets.US_ASCII))
        short(out, w); short(out, h)
        out.write(0xF7); out.write(0); out.write(0) // Global table of 256 colours.
        val table = IntArray(256)
        palette.forEachIndexed { i, c -> table[i + offset] = c }
        for (c in table) { out.write(Color.red(c)); out.write(Color.green(c)); out.write(Color.blue(c)) }
        if (transparent) out.write(byteArrayOf(0x21, 0xF9.toByte(), 4, 1, 0, 0, 0, 0))
        out.write(0x2C); short(out, 0); short(out, 0); short(out, w); short(out, h); out.write(0)
        out.write(8)
        lzw(indices, out)
        out.write(0)
        out.write(0x3B)
        file.writeBytes(out.toByteArray())
    }

    private fun short(out: ByteArrayOutputStream, value: Int) { out.write(value and 0xFF); out.write(value shr 8 and 0xFF) }

    private fun nearest(palette: IntArray, p: Int): Int {
        val r = Color.red(p); val g = Color.green(p); val b = Color.blue(p)
        var best = 0
        var bestDistance = Int.MAX_VALUE
        for (i in palette.indices) {
            val c = palette[i]
            val dr = Color.red(c) - r; val dg = Color.green(c) - g; val db = Color.blue(c) - b
            val d = dr * dr * 3 + dg * dg * 4 + db * db * 2
            if (d < bestDistance) { bestDistance = d; best = i }
        }
        return best
    }

    /** Median cut over a sample of the opaque pixels. */
    private fun palette(pixels: IntArray, size: Int): IntArray {
        val step = maxOf(1, pixels.size / 120_000)
        val sample = ArrayList<Int>()
        for (i in pixels.indices step step) if (Color.alpha(pixels[i]) >= 128) sample += pixels[i] and 0xFFFFFF
        if (sample.isEmpty()) return intArrayOf(0)
        val boxes = mutableListOf(sample.toIntArray())
        while (boxes.size < size) {
            val (index, channel) = boxes.withIndex().filter { it.value.size > 1 }.map { (i, box) -> i to widest(box) }
                .maxByOrNull { it.second.second }?.let { it.first to it.second.first } ?: break
            val box = boxes.removeAt(index)
            val shift = 16 - channel * 8
            val sorted = box.sortedBy { (it shr shift) and 0xFF }
            val half = sorted.size / 2
            boxes += sorted.subList(0, half).toIntArray()
            boxes += sorted.subList(half, sorted.size).toIntArray()
        }
        return boxes.map { box ->
            var r = 0L; var g = 0L; var b = 0L
            for (c in box) { r += c shr 16 and 0xFF; g += c shr 8 and 0xFF; b += c and 0xFF }
            Color.rgb((r / box.size).toInt(), (g / box.size).toInt(), (b / box.size).toInt())
        }.toIntArray()
    }

    /** The channel (0 red, 1 green, 2 blue) with the widest range, and that range. */
    private fun widest(box: IntArray): Pair<Int, Int> {
        val min = intArrayOf(255, 255, 255)
        val max = intArrayOf(0, 0, 0)
        for (c in box) for (ch in 0..2) {
            val v = (c shr (16 - ch * 8)) and 0xFF
            if (v < min[ch]) min[ch] = v
            if (v > max[ch]) max[ch] = v
        }
        val ranges = (0..2).map { max[it] - min[it] }
        val channel = ranges.indices.maxBy { ranges[it] }
        return channel to ranges[channel]
    }

    private fun lzw(indices: ByteArray, out: ByteArrayOutputStream) {
        val clear = 256
        val end = 257
        val block = ByteArray(255)
        var blockSize = 0
        var bitBuffer = 0
        var bitCount = 0
        var codeSize = 9
        var next = 258
        val table = HashMap<Int, Int>(8192)
        fun emit(code: Int) {
            bitBuffer = bitBuffer or (code shl bitCount)
            bitCount += codeSize
            while (bitCount >= 8) {
                block[blockSize++] = (bitBuffer and 0xFF).toByte()
                bitBuffer = bitBuffer ushr 8
                bitCount -= 8
                if (blockSize == 255) { out.write(255); out.write(block, 0, 255); blockSize = 0 }
            }
        }
        emit(clear)
        if (indices.isEmpty()) { emit(end) } else {
            var prefix = indices[0].toInt() and 0xFF
            for (i in 1 until indices.size) {
                val k = indices[i].toInt() and 0xFF
                val key = (prefix shl 8) or k
                val found = table[key]
                if (found != null) { prefix = found; continue }
                emit(prefix)
                if (next < 4096) {
                    table[key] = next++
                    if (next - 1 == (1 shl codeSize) && codeSize < 12) codeSize++
                } else {
                    emit(clear)
                    table.clear()
                    next = 258
                    codeSize = 9
                }
                prefix = k
            }
            emit(prefix)
            emit(end)
        }
        if (bitCount > 0) {
            block[blockSize++] = (bitBuffer and 0xFF).toByte()
            if (blockSize == 255) { out.write(255); out.write(block, 0, 255); blockSize = 0 }
        }
        if (blockSize > 0) { out.write(blockSize); out.write(block, 0, blockSize) }
    }
}
