#Requires -Version 7.0
<#
.SYNOPSIS
    Regenerates SharpMoji's embedded emoji data from a pinned Emojibase release.

.DESCRIPTION
    Build-time only. This never becomes runtime code: SharpMoji ships its data and makes no
    network calls at run time (docs/SPEC.md section 7.1).

    The steps, in order:

      1. Fetch the corpus for the requested Emojibase release.
      2. Verify that emoji structure is still identical across every locale. The whole packaging
         design rests on that (docs/SPEC.md section 3.7), so generation stops if it ever stops
         being true.
      3. Emit one shared structure pack plus one string table per language, Brotli-compressed.
      4. Print a diff against the packs already on disk - added, removed, relabeled and
         restructured emoji. The packs are compressed binary, so this is the only readable account
         of what a regeneration actually changed.

    Afterwards, and not automatically:

      - Update SharpMojiData.EmojibaseVersion / UnicodeVersion / EmojiCount if the release changed.
      - Regenerate any CLDR-only locales: ./scripts/generate-cldr-locale.ps1 -Locale he
      - Run `dotnet test`. The pinning tests are meant to fail until they are updated deliberately.
      - Write the CHANGELOG entry from the printed diff. New emoji are additive, so a Unicode
        release is a minor version bump.

.PARAMETER EmojibaseVersion
    The emojibase-data release to generate from. Always an exact version: "latest" would make
    builds non-reproducible and would silently change data under users.

.PARAMETER OutputPath
    Where to write the packs. Defaults to the library's Resources directory.

.PARAMETER SkipFetch
    Use the corpus already on disk instead of downloading it.

.EXAMPLE
    ./scripts/update-emoji-data.ps1
    Regenerates from the currently pinned release. Useful for checking the packs are in sync.

.EXAMPLE
    ./scripts/update-emoji-data.ps1 -EmojibaseVersion 18.0.0
    Moves to a new Unicode release.
#>
[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string] $EmojibaseVersion = '17.0.0',

    [string] $OutputPath,

    [switch] $SkipFetch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent

if (-not $OutputPath) {
    $OutputPath = Join-Path $repoRoot 'src' 'SharpMoji' 'Resources'
}

$pinned = (Select-String -Path (Join-Path $repoRoot 'src' 'SharpMoji' 'SharpMojiData.cs') `
    -Pattern 'EmojibaseVersion\s*=\s*"([^"]+)"').Matches[0].Groups[1].Value

if ($pinned -ne $EmojibaseVersion) {
    Write-Host ''
    Write-Host "SharpMojiData pins $pinned, but you asked for $EmojibaseVersion." -ForegroundColor Yellow
    Write-Host 'Update SharpMojiData.cs first: the generator reads the version from it, and the' -ForegroundColor Yellow
    Write-Host 'corpus loader looks for a directory named after it.' -ForegroundColor Yellow
    exit 1
}

if (-not $SkipFetch) {
    & (Join-Path $PSScriptRoot 'fetch-emoji-corpus.ps1') -EmojibaseVersion $EmojibaseVersion

    if ($LASTEXITCODE -ne 0) {
        throw "Fetching the corpus failed."
    }

    Write-Host ''
}

Push-Location $repoRoot

try {
    dotnet run --project (Join-Path 'tools' 'SharpMoji.DataTool') -- $OutputPath

    if ($LASTEXITCODE -ne 0) {
        throw "Pack generation failed."
    }
}
finally {
    Pop-Location
}
