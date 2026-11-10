# Build a self-contained win-x64 package. The staging directory is always new, so
# an existing portable install (which might hold the player's data) is not touched.
param(
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[a-zA-Z0-9.]+)?$')]
    [string]$Version = "0.3.5-zh.5",
    [string]$OutDir = "",
    [string]$Dotnet = "dotnet"
)
$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "SecondHunt.Desktop.csproj"
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
if (!$OutDir) { $OutDir = Join-Path $repo "out" }
$OutDir = [IO.Path]::GetFullPath($OutDir)
[void][IO.Directory]::CreateDirectory($OutDir)
$name = "SecondHunt-CN-$Version-windows-x64"
$loose = Join-Path $OutDir $name
$zip = "$loose.zip"
if ((Test-Path -LiteralPath $loose) -or (Test-Path -LiteralPath $zip)) {
    throw "Output already exists. Use a new -Version or -OutDir to preserve it: $loose"
}
$stage = [IO.Path]::GetFullPath((Join-Path $OutDir (".stage-" + [Guid]::NewGuid().ToString("N"))))
$tempZip = "$stage.zip"
$numeric = ($Version -split "-", 2)[0]

function Copy-RuntimeNotices([string]$dest) {
    $assets = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot "obj\project.assets.json") | ConvertFrom-Json
    $downloads = @($assets.project.frameworks.PSObject.Properties.Value | ForEach-Object { $_.downloadDependencies })
    $downloadNames = @("Microsoft.NETCore.App.Runtime.win-x64", "Microsoft.WindowsDesktop.App.Runtime.win-x64")
    foreach ($id in $downloadNames) {
        $dep = $downloads | Where-Object { $_.name -eq $id } | Select-Object -First 1
        if ($dep.version -notmatch '\d+\.\d+\.\d+') { throw "Missing restore version: $id" }
        $folder = $id.ToLowerInvariant() + "/" + $Matches[0]
        $path = $assets.packageFolders.PSObject.Properties.Name | ForEach-Object { Join-Path $_ $folder } |
            Where-Object { Test-Path -LiteralPath $_ -PathType Container } | Select-Object -First 1
        if (!$path) { throw "Missing runtime pack: $folder" }
        $license = Get-ChildItem -LiteralPath $path -File | Where-Object Name -Match '^LICENSE(\.txt)?$' | Select-Object -First 1
        if (!$license) { throw "Missing licence in $folder" }
        Copy-Item -LiteralPath $license.FullName -Destination (Join-Path $dest "$id-LICENSE.txt")
        Get-ChildItem -LiteralPath $path -File | Where-Object Name -Match '^THIRD-PARTY-NOTICES(\.txt)?$' |
            ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $dest "$id-THIRD-PARTY-NOTICES.txt") }
    }
    $glfw = $assets.libraries.PSObject.Properties.Name | Where-Object { $_ -like "OpenTK.redist.glfw/*" } | Select-Object -First 1
    $glfwLicense = $assets.packageFolders.PSObject.Properties.Name | ForEach-Object { Join-Path $_ ($glfw.ToLowerInvariant() + "/COPYING.md") } |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    if (!$glfwLicense) { throw "Missing GLFW licence" }
    Copy-Item -LiteralPath $glfwLicense -Destination (Join-Path $dest "glfw-LICENSE.txt")
}

try {
    $publishArgs = @('publish', $project, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
        '-p:MphPublic=true', '-p:MphJitOnly=true', '-p:PublishSingleFile=true', '-p:EnableCompressionInSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=false', "-p:Version=$numeric", "-p:InformationalVersion=$Version", '-o', $stage)
    & $Dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed: $LASTEXITCODE" }

    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "dist\README.zh-CN.txt") -Destination (Join-Path $stage "README.txt")
    Copy-Item -LiteralPath (Join-Path $repo "LICENSE") -Destination (Join-Path $stage "LICENSE.txt")
    Copy-Item -LiteralPath (Join-Path $repo "NOTICE-fork.md") -Destination (Join-Path $stage "NOTICE-fork.txt")
    Copy-Item -LiteralPath (Join-Path $repo "THIRD_PARTY_NOTICES.md") -Destination (Join-Path $stage "THIRD_PARTY_NOTICES.txt")
    $licenses = Join-Path $stage "licenses"
    [void][IO.Directory]::CreateDirectory($licenses)
    Copy-Item -LiteralPath (Join-Path $repo "locales\zh-CN\OFL-Noto-CJK.txt") -Destination $licenses
    Copy-RuntimeNotices $licenses

    # Use forward slashes in the archive even on Windows PowerShell 5.
    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::Open($tempZip, [IO.Compression.ZipArchiveMode]::Create)
    try {
        Get-ChildItem -LiteralPath $stage -Recurse -File | Sort-Object FullName | ForEach-Object {
            $entry = "Second Hunt/" + $_.FullName.Substring($stage.Length + 1).Replace("\", "/")
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $entry,
                [IO.Compression.CompressionLevel]::Optimal)
        }
    } finally { $archive.Dispose() }
    Move-Item -LiteralPath $stage -Destination $loose
    Move-Item -LiteralPath $tempZip -Destination $zip
    $sha = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    "$sha  $(Split-Path $zip -Leaf)" | Set-Content -Encoding ascii -LiteralPath "$zip.sha256"
    "EXE: $(Join-Path $loose 'SecondHunt.exe')"
    "ZIP: $zip"
    "SHA256: $sha"
} finally {
    if ((Split-Path $stage -Parent) -eq $OutDir -and (Split-Path $stage -Leaf) -match '^\.stage-[a-f0-9]{32}$') {
        if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
        if (Test-Path -LiteralPath $tempZip) { Remove-Item -LiteralPath $tempZip -Force }
    }
}
