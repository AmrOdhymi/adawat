package com.motalim.adawat

import android.app.Application
import com.motalim.adawat.core.L10n
import com.motalim.adawat.work.Inputs
import kotlin.concurrent.thread

class AdawatApp : Application() {
    override fun onCreate() {
        super.onCreate()
        L10n.load(this)
        thread(name = "cleanup", isDaemon = true) { Inputs.cleanUp(this) }
    }
}
