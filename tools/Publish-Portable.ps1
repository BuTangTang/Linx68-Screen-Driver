[CmdletBinding()]
param(
    [ValidateSet('Release')]
    [string]$Configuration = 'Release',
    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64',
    [ValidatePattern('^[A-Za-z0-9._-]+$')]
    [string]$OutputName = 'Linx68ScreenDriver-Portable'
)

$ErrorActionPreference = 'Stop'

$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $workspace 'artifacts'))
$output = [IO.Path]::GetFullPath((Join-Path $artifactsRoot $OutputName))
$staging = [IO.Path]::GetFullPath((Join-Path $artifactsRoot ('.portable-staging-' + [Guid]::NewGuid().ToString('N'))))

if (-not $output.StartsWith($artifactsRoot, [StringComparison]::OrdinalIgnoreCase) -or
    -not $staging.StartsWith($artifactsRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Portable output resolved outside the artifacts directory.'
}

New-Item -ItemType Directory -Path $artifactsRoot -Force | Out-Null

try {
    & dotnet publish (Join-Path $workspace 'src\Linx68.ScreenDriver.App\Linx68.ScreenDriver.App.csproj') `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $staging
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }

    $sourceExe = Join-Path $staging 'Linx68.ScreenDriver.App.exe'
    $targetExe = Join-Path $staging 'Linx68ScreenDriver.exe'
    if (-not (Test-Path -LiteralPath $sourceExe)) {
        throw 'Published executable was not produced.'
    }
    Rename-Item -LiteralPath $sourceExe -NewName 'Linx68ScreenDriver.exe'

    Get-ChildItem -LiteralPath $staging -Recurse -File |
        Where-Object { $_.Extension -in '.pdb', '.xml' } |
        Remove-Item -Force

    $runtimeData = Join-Path $staging 'Data'
    if (Test-Path -LiteralPath $runtimeData) {
        Remove-Item -LiteralPath $runtimeData -Recurse -Force
    }

    $customFonts = Get-ChildItem -LiteralPath (Join-Path $staging 'Fonts') -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -in '.ttf', '.otf', '.ttc' }
    if ($customFonts) {
        throw 'Unexpected user font files were found in the clean publish output.'
    }

    [IO.File]::WriteAllText(
        (Join-Path $staging 'portable.flag'),
        "portable`r`n",
        [Text.UTF8Encoding]::new($false))
    Copy-Item -LiteralPath (Join-Path $workspace 'PORTABLE_README.txt') `
        -Destination (Join-Path $staging 'README-zh-CN.txt') -Force

    $hash = (Get-FileHash -LiteralPath $targetExe -Algorithm SHA256).Hash
    [IO.File]::WriteAllText(
        (Join-Path $staging 'SHA256SUMS.txt'),
        "$hash  Linx68ScreenDriver.exe`r`n",
        [Text.UTF8Encoding]::new($false))

    foreach ($required in @(
        'Linx68ScreenDriver.exe',
        'LICENSE',
        'ASSET_LICENSE.md',
        'PRIVACY.md',
        'THIRD-PARTY-NOTICES.md',
        'Licenses\Doto-OFL.txt',
        'Licenses\dotnet-LICENSE.txt',
        'Licenses\dotnet-ThirdPartyNotices.txt'
    )) {
        if (-not (Test-Path -LiteralPath (Join-Path $staging $required))) {
            throw "Required release file is missing: $required"
        }
    }

    if (Test-Path -LiteralPath $output) {
        Remove-Item -LiteralPath $output -Recurse -Force
    }
    Move-Item -LiteralPath $staging -Destination $output

    Write-Host "Clean portable test build: $output"
    Write-Host "SHA-256: $hash"
    Write-Host 'No files were uploaded or published.'
}
finally {
    if (Test-Path -LiteralPath $staging) {
        Remove-Item -LiteralPath $staging -Recurse -Force
    }
}
