#!/usr/bin/env pwsh
# Builds Adawat-Setup-<arch>.exe for Windows x64 and ARM64.
#   windows/tools/build-installer.ps1 [x64] [arm64] [-InnoSetup <ISCC.exe>]
# Publishes the app and the command line, copies the ffmpeg binaries fetched by fetch-ffmpeg.ps1,
# and compiles windows/installer/Adawat.iss with Inno Setup 6.
[CmdletBinding()]
param(
    [ValidateSet('arm64', 'x64')]
    [string[]]$Arch = @('arm64', 'x64'),
    [string]$InnoSetup
)

$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    $version = (Select-String -Path 'Directory.Build.props' -Pattern '<Version>(.*?)</Version>').Matches[0].Groups[1].Value

    $iscc = if ($InnoSetup) {
        (Resolve-Path $InnoSetup).Path
    }
    else {
        $candidates = @(
            (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
            (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
            (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
        )
        $found = $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
        if (-not $found) {
            $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
            if ($command) { $found = $command.Source }
        }
        if (-not $found) {
            throw 'Inno Setup 6 was not found. Install it or pass -InnoSetup <path to ISCC.exe>.'
        }
        $found
    }

    New-Item -ItemType Directory -Force -Path 'build/installer' | Out-Null

    foreach ($architecture in $Arch) {
        $rid = "win-$architecture"
        $out = "build/publish/app-$architecture"

        if (-not (Test-Path "build/ffmpeg/$rid/ffmpeg.exe")) {
            throw "Missing build/ffmpeg/$rid/ffmpeg.exe. Run windows/tools/fetch-ffmpeg.ps1 first."
        }

        Write-Host "Publishing $architecture..."
        if (Test-Path $out) { Remove-Item $out -Recurse -Force }

        foreach ($project in 'Adawat.App', 'Adawat.Cli') {
            dotnet publish "src/$project/$project.csproj" -f net10.0-windows10.0.22621.0 -c Release -r $rid --self-contained -p:DebugType=none -o $out
            if ($LASTEXITCODE -ne 0) { throw "Publish failed for $project ($architecture)." }
        }

        if (-not (Test-Path "$out/Adawat.exe") -or -not (Test-Path "$out/adawat-cli.exe")) {
            throw "Publish failed for $architecture."
        }

        New-Item -ItemType Directory -Force -Path "$out/tools" | Out-Null
        Copy-Item "build/ffmpeg/$rid/ffmpeg.exe" "$out/tools/ffmpeg.exe" -Force
        Copy-Item "build/ffmpeg/$rid/LICENSE.txt" "$out/tools/ffmpeg-LICENSE.txt" -Force

        Write-Host "Compiling the $architecture installer..."
        & $iscc "/Q" "/DArch=$architecture" "/DAppVersion=$version" "/DSource=$((Resolve-Path $out).Path)" (Resolve-Path 'installer/Adawat.iss').Path
        if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed for $architecture." }

        $setup = "build/installer/Adawat-Setup-$architecture.exe"
        if (-not (Test-Path $setup)) { throw "The installer was not produced: $setup" }
        Write-Host "Built: $setup"
    }

    Get-ChildItem 'build/installer/Adawat-Setup-*.exe' |
        Get-FileHash -Algorithm SHA256 |
        Select-Object Hash, Path
}
finally {
    Pop-Location
}
