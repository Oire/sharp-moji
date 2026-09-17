#Requires -Version 7.0
<#
.SYNOPSIS
    Regenerates SharpMoji's embedded emoji data from a pinned Emojibase release.

.DESCRIPTION
    Build-time only. This script must never become runtime code: SharpMoji ships its data and
    makes no network calls at run time (docs/SPEC.md section 7.1).

    Planned behavior (see docs/SPEC.md section 7.3):

      1. Fetch emojibase-data@<EmojibaseVersion> for every supported locale.
      2. Emit the shared structure table once — hexcode, emoji, text, type, order, group,
         subgroup, version, gender, emoticon and skin structure are identical across locales
         (SPEC section 3.7), so they are stored once rather than 29 times.
      3. Emit one string table per locale (label + tags only), Brotli-compressed.
      4. Regenerate the Hebrew pack via generate-cldr-locale.ps1.
      5. Rewrite SharpMojiData.EmojibaseVersion / UnicodeVersion / EmojiCount.
      6. Print a data diff — emoji added, removed, relabeled, retagged, and any change to group
         or skin structure. The CHANGELOG entry is written from this.

    After running, update the pinning tests in tests/SharpMoji.Tests/SharpMojiDataTests.cs so a
    partial regeneration fails CI rather than shipping.

.PARAMETER EmojibaseVersion
    The emojibase-data release to generate from, for example "17.0.0". Always an exact version:
    "latest" would make builds non-reproducible.

.PARAMETER OutputPath
    Where to write the generated resources. Defaults to the library's Resources directory.

.EXAMPLE
    ./scripts/update-emoji-data.ps1 -EmojibaseVersion 17.0.0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string] $EmojibaseVersion,

    [string] $OutputPath = (Join-Path $PSScriptRoot '..' 'src' 'SharpMoji' 'Resources')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

throw 'Not implemented yet — this is Phase 1 (see docs/SPEC.md section 10).'
