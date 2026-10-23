param(
    [ValidateSet('win-x64')][string]$Runtime = 'win-x64',
    [string]$OutputDirectory = 'artifacts\release',
    [switch]$SingleFile,
    [switch]$FrameworkDependent
)

$ErrorActionPreference = 'Stop'
if ($FrameworkDependent -and !$SingleFile) { throw '-FrameworkDependent requires -SingleFile.' }
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $repository
try {
    [xml]$project = Get-Content 'PIgHeAD.Launcher\PIgHeAD.Launcher.csproj' -Raw
    $version = 'v' + $project.Project.PropertyGroup.InformationalVersion
    $outputRoot = [IO.Path]::GetFullPath((Join-Path $repository $OutputDirectory))
    $allowedRoot = Join-Path $repository 'artifacts'
    if (!$outputRoot.StartsWith($allowedRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Release output must remain inside the repository artifacts directory.'
    }
    $bundleName = "PIgHeAD-$version-$Runtime"
    if ($FrameworkDependent) { $bundleName += '-lite' }
    $publishDirectory = Join-Path $outputRoot $bundleName
    $archivePath = Join-Path $outputRoot ($bundleName + '.zip')
    if ((Test-Path -LiteralPath $publishDirectory) -or (Test-Path -LiteralPath $archivePath)) {
        throw 'Release output already exists. Use a fresh artifacts subdirectory to avoid mixing builds.'
    }
    New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
    & dotnet publish 'PIgHeAD.Launcher\PIgHeAD.Launcher.csproj' -c Release -r $Runtime --self-contained true `
        '-p:PublishTrimmed=false' '-p:PublishSingleFile=false' '-p:DebugType=None' '-p:DebugSymbols=false' -o $publishDirectory
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

    $documents = Join-Path $publishDirectory 'docs'
    New-Item -ItemType Directory -Path $documents | Out-Null
    Copy-Item -LiteralPath 'README.md', 'README.en.md' -Destination $publishDirectory
    Get-ChildItem -LiteralPath 'docs' -Filter '*.md' -File | Copy-Item -Destination $documents

    $noticeDirectory = Join-Path $publishDirectory 'ThirdPartyNotices'
    New-Item -ItemType Directory -Path $noticeDirectory | Out-Null
    $assets = Get-Content 'PIgHeAD.Launcher\obj\project.assets.json' -Raw | ConvertFrom-Json
    $packageRoot = $assets.packageFolders.PSObject.Properties.Name | Select-Object -First 1
    $attributions = @('Third-party package metadata and original license texts bundled with PIgHeAD.', '')
    foreach ($library in $assets.libraries.PSObject.Properties) {
        if ($library.Value.type -ne 'package') { continue }
        $packageDirectory = Join-Path $packageRoot $library.Value.path
        $nuspec = Get-ChildItem -LiteralPath $packageDirectory -Filter '*.nuspec' -File | Select-Object -First 1
        [xml]$package = Get-Content -LiteralPath $nuspec.FullName -Raw
        $metadata = $package.SelectSingleNode('/*[local-name()="package"]/*[local-name()="metadata"]')
        $license = $metadata.SelectSingleNode('*[local-name()="license"]')
        $source = $metadata.SelectSingleNode('*[local-name()="repository"]')
        $projectUrl = $metadata.SelectSingleNode('*[local-name()="projectUrl"]')
        $authors = $metadata.SelectSingleNode('*[local-name()="authors"]')
        $copyright = $metadata.SelectSingleNode('*[local-name()="copyright"]')
        $name = $library.Name.Replace('/', '-')
        Copy-Item -LiteralPath $nuspec.FullName -Destination (Join-Path $noticeDirectory ($name + '.nuspec'))
        $licensePath = Join-Path $noticeDirectory ($name + '-LICENSE.txt')
        $found = $false
        if ($license -and $license.type -eq 'file') {
            Copy-Item -LiteralPath (Join-Path $packageDirectory $license.InnerText) -Destination $licensePath
            $found = $true
        }
        $repositoryUrl = if ($source) { $source.url } elseif ($projectUrl) { $projectUrl.InnerText } else { '' }
        if (!$found -and $repositoryUrl -match '^https://github.com/([^/]+/[^/]+)') {
            $slug = $Matches[1].TrimEnd('/').Replace('.git', '')
            $revision = if ($source -and $source.commit) { $source.commit } else { 'master' }
            foreach ($file in @('LICENSE', 'LICENSE.txt', 'LICENSE.md', 'LICENSE.TXT')) {
                try {
                    Invoke-WebRequest -UseBasicParsing -Uri "https://raw.githubusercontent.com/$slug/$revision/$file" `
                        -OutFile $licensePath -TimeoutSec 20
                    $found = $true
                    break
                } catch [System.Net.WebException] { }
            }
        }
        if (!$found -and $license -and $license.type -eq 'expression' -and $license.InnerText -eq 'MIT') {
            $mitText = (Invoke-WebRequest -UseBasicParsing -Uri 'https://raw.githubusercontent.com/spdx/license-list-data/main/text/MIT.txt' -TimeoutSec 20).Content
            @($copyright.InnerText, "Package authors: $($authors.InnerText)", '', $mitText) |
                Set-Content -LiteralPath $licensePath -Encoding UTF8
            $found = $true
        }
        if (!$found) { throw "Cannot obtain the third-party license for $($library.Name)." }
        $attributions += @($library.Name, "Authors: $($authors.InnerText)", "Copyright: $($copyright.InnerText)", "Source: $repositoryUrl", '')
    }
    foreach ($runtimePackage in @('microsoft.netcore.app.runtime.win-x64', 'microsoft.windowsdesktop.app.runtime.win-x64')) {
        $runtimeRoot = Join-Path $packageRoot $runtimePackage
        $runtimeVersion = Get-ChildItem -LiteralPath $runtimeRoot -Directory | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
        Get-ChildItem -LiteralPath $runtimeVersion.FullName -File | Where-Object { $_.Name -match '^LICENSE|^THIRD-PARTY-NOTICES' } |
            ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $noticeDirectory ($runtimePackage + '-' + $_.Name)) }
    }
    $attributions | Set-Content -LiteralPath (Join-Path $noticeDirectory 'ATTRIBUTIONS.txt') -Encoding UTF8
    $commit = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot determine the source commit.' }
    [ordered]@{
        version = $version; runtime = $Runtime; selfContained = !$FrameworkDependent; singleFile = [bool]$SingleFile
        sourceCommit = $commit; builtAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $publishDirectory 'build-info.json') -Encoding UTF8

    if ($SingleFile) {
        $singleFileDirectory = Join-Path $outputRoot 'single-file'
        $selfContained = (!$FrameworkDependent).ToString().ToLowerInvariant()
        & dotnet publish 'PIgHeAD.Launcher\PIgHeAD.Launcher.csproj' -c Release -r $Runtime --self-contained $selfContained `
            '-p:PublishProfile=SingleFile' "-p:ReleaseContentDirectory=$publishDirectory" -o $singleFileDirectory
        if ($LASTEXITCODE -ne 0) { throw 'Single-file dotnet publish failed.' }
        $files = @(Get-ChildItem -LiteralPath $singleFileDirectory -File -Recurse -Force)
        if ($files.Count -ne 1 -or $files[0].Name -ne 'PIgHeAD.Launcher.exe') {
            throw 'Single-file publish must produce exactly one EXE with no companion files.'
        }
        $executablePath = Join-Path $outputRoot ($bundleName + '.exe')
        Move-Item -LiteralPath $files[0].FullName -Destination $executablePath
        $hash = (Get-FileHash -LiteralPath $executablePath -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $([IO.Path]::GetFileName($executablePath))" | Set-Content -LiteralPath (Join-Path $outputRoot 'SHA256SUMS.txt') -Encoding ASCII
        Write-Output "Standalone launcher: $executablePath"
        Write-Output "SHA256: $hash"
        return
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    Add-Type -AssemblyName System.IO.Compression
    $archiveStream = [IO.File]::Open($archivePath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
    $archive = New-Object IO.Compression.ZipArchive($archiveStream, [IO.Compression.ZipArchiveMode]::Create, $false)
    try {
        $prefix = $publishDirectory.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        foreach ($file in Get-ChildItem -LiteralPath $publishDirectory -File -Recurse -Force | Sort-Object FullName) {
            $relative = $file.FullName.Substring($prefix.Length).Replace('\', '/')
            $entry = $archive.CreateEntry($relative, [IO.Compression.CompressionLevel]::Optimal)
            $input = [IO.File]::OpenRead($file.FullName)
            $output = $entry.Open()
            try { $input.CopyTo($output) }
            finally { $output.Dispose(); $input.Dispose() }
        }
    } finally { $archive.Dispose(); $archiveStream.Dispose() }
    $hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([IO.Path]::GetFileName($archivePath))" | Set-Content -LiteralPath (Join-Path $outputRoot 'SHA256SUMS.txt') -Encoding ASCII
    Write-Output "Release archive: $archivePath"
    Write-Output "SHA256: $hash"
} finally { Pop-Location }
