pluginManagement {
    repositories {
        google()
        mavenCentral()
        gradlePluginPortal()
    }
}

dependencyResolutionManagement {
    repositoriesMode = RepositoriesMode.FAIL_ON_PROJECT_REPOS
    repositories {
        google()
        mavenCentral()
        maven("https://jitpack.io") { content { includeGroup("com.github.adaptech-cz") } }
    }
}

rootProject.name = "Adawat"
include(":app")
