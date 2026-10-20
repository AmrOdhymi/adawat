package com.motalim.adawat.media

import android.media.MediaCodec
import android.media.MediaExtractor
import android.media.MediaFormat
import android.media.MediaMuxer
import com.motalim.adawat.core.AdawatException
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive
import java.io.File
import java.nio.ByteBuffer

/**
 * Copies tracks into a new MP4 or WebM file without re-encoding: instant and lossless. The new file carries no tags and
 * no location, only the tracks and their orientation.
 */
object Remux {
    suspend fun copy(
        source: File,
        target: File,
        video: Boolean = true,
        audio: Boolean = true,
        rotation: Int? = null,
        webm: Boolean = false,
        progress: (Double) -> Unit = {},
    ) {
        val info = MediaInfo.of(source)
        val extractor = MediaExtractor()
        extractor.setDataSource(source.path)
        val muxer = MediaMuxer(target.path, if (webm) MediaMuxer.OutputFormat.MUXER_OUTPUT_WEBM else MediaMuxer.OutputFormat.MUXER_OUTPUT_MPEG_4)
        var started = false
        try {
            val map = mutableMapOf<Int, Int>()
            for (i in 0 until extractor.trackCount) {
                val format = extractor.getTrackFormat(i)
                val mime = format.getString(MediaFormat.KEY_MIME) ?: continue
                val keep = (video && i == info.videoTrack) || (audio && i == info.audioTrack)
                if (!keep) continue
                extractor.selectTrack(i)
                map[i] = try { muxer.addTrack(format) } catch (e: Exception) { throw AdawatException("This file's ${if (mime.startsWith("video")) "video" else "audio"} cannot be copied into this format.") }
            }
            if (map.isEmpty()) throw AdawatException("This file has no audio track")
            muxer.setOrientationHint(((rotation ?: info.rotation) % 360 + 360) % 360)
            muxer.start()
            started = true
            val buffer = ByteBuffer.allocate(4 shl 20)
            val bufferInfo = MediaCodec.BufferInfo()
            while (true) {
                currentCoroutineContext().ensureActive()
                val size = extractor.readSampleData(buffer, 0)
                if (size < 0) break
                val track = map[extractor.sampleTrackIndex]
                if (track != null) {
                    bufferInfo.set(0, size, extractor.sampleTime, if (extractor.sampleFlags and MediaExtractor.SAMPLE_FLAG_SYNC != 0) MediaCodec.BUFFER_FLAG_KEY_FRAME else 0)
                    muxer.writeSampleData(track, buffer, bufferInfo)
                    if (info.durationUs > 0) progress(extractor.sampleTime.toDouble() / info.durationUs)
                }
                extractor.advance()
            }
        } catch (e: Throwable) {
            runCatching { if (started) muxer.stop() }
            muxer.release()
            target.delete()
            throw e
        } finally {
            extractor.release()
        }
        muxer.stop()
        muxer.release()
    }
}
