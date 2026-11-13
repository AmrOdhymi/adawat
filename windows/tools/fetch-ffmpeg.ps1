#!/usr/bin/env pwsh
# Downloads the ffmpeg and ffprobe that ship with Adawat for Windows: BtbN's GPL builds of FFmpeg 8.1
# (redistributable, no nonfree parts), pinned to one build so every release carries the same binaries.
#   windows/tools/fetch-ffmpeg.ps1   -> windows/build/ffmpeg/{win-arm64,win-x64}/{ffmpeg,ffprobe}.exe
[CmdletBinding()]
param(
    [ValidateSet('win-arm64', 'win-x64')]
    [string[]]$Runtime = @('win-arm64', 'win-x64'),
    [string]$Tag = 'autobuild-2026-10-18-13-01',
    [string]$Version = 'n8.1.3-18-g46d899d7a0'
)

$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    $out = Join-Path (Get-Location) 'build/ffmpeg'
    New-Item -ItemType Directory -Force -Path $out | Out-Null

    $archNames = @{ 'win-arm64' = 'winarm64'; 'win-x64' = 'win64' }
    $checksums = @()

    foreach ($rid in $Runtime) {
        $arch = $archNames[$rid]
        $zipName = "ffmpeg-$Version-$arch-gpl-8.1.zip"
        $zip = Join-Path $out $zipName

        if (-not (Test-Path $zip)) {
            $url = "https://github.com/BtbN/FFmpeg-Builds/releases/download/$Tag/$zipName"
            Write-Host "Downloading $zipName..."
            Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing
        }

        $checksums += "$((Get-FileHash -Algorithm SHA256 $zip).Hash.ToLowerInvariant())  $zipName"

        $target = Join-Path $out $rid
        if (Test-Path $target) { Remove-Item $target -Recurse -Force }
        New-Item -ItemType Directory -Force -Path $target | Out-Null

        $temp = Join-Path ([System.IO.Path]::GetTempPath()) ("adawat-ffmpeg-" + [guid]::NewGuid().ToString('N'))
        try {
            Expand-Archive -Path $zip -DestinationPath $temp -Force

            foreach ($name in 'ffmpeg.exe', 'ffprobe.exe') {
                $file = Get-ChildItem -Path $temp -Recurse -File -Filter $name |
                    Where-Object { $_.Directory.Name -eq 'bin' } | Select-Object -First 1
                if (-not $file) { throw "$name was not found in $zipName" }
                Copy-Item $file.FullName (Join-Path $target $name) -Force
            }

            $license = Get-ChildItem -Path $temp -Recurse -File -Filter 'LICENSE.txt' | Select-Object -First 1
            if (-not $license) { throw "LICENSE.txt was not found in $zipName" }
            Copy-Item $license.FullName (Join-Path $target 'LICENSE.txt') -Force
        }
        finally {
            Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
        }

        Write-Host "Ready: $target"
    }

    $checksums | Set-Content (Join-Path $out 'SOURCES.sha256') -Encoding ASCII
    Get-Content (Join-Path $out 'SOURCES.sha256')
}
finally {
    Pop-Location
}
