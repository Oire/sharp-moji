#Requires -Version 7.0
<#
.SYNOPSIS
    Downloads the raw Emojibase corpus that the data-assumption tests validate against.

.DESCRIPTION
    The corpus is ~22 MB of upstream JSON and is NOT committed: it is a test fixture, not
    product data. The library itself never downloads anything (docs/SPEC.md section 7.1) —
    this runs on a developer machine or in CI, before `dotnet test`.

    Without the corpus, the tests in tests/SharpMoji.Tests/Emojibase skip rather than fail, so
    `dotnet test` still works offline.

    Files land in .emoji-corpus/<version>/ which is gitignored.

.PARAMETER EmojibaseVersion
    The emojibase-data release to fetch. Must match SharpMojiData.EmojibaseVersion, or the
    tests will skip: they assert against the pinned release, not whatever is newest.

.EXAMPLE
    ./scripts/fetch-emoji-corpus.ps1
#>
[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string] $EmojibaseVersion = '17.0.0',

    [switch] $Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$locales = @(
    'bn', 'da', 'de', 'en', 'en-gb', 'es', 'es-mx', 'et', 'fi', 'fr', 'hi', 'hu', 'it', 'ja',
    'ko', 'lt', 'ms', 'nb', 'nl', 'pl', 'pt', 'ru', 'sv', 'th', 'uk', 'vi', 'zh', 'zh-hant'
)

$root = Join-Path (Split-Path $PSScriptRoot -Parent) '.emoji-corpus' $EmojibaseVersion
$null = New-Item -ItemType Directory -Force -Path $root

Write-Host "Fetching emojibase-data $EmojibaseVersion into $root"
Write-Host ''

$downloaded = 0
$skipped = 0

foreach ($locale in $locales) {
    $localeDir = Join-Path $root $locale
    $null = New-Item -ItemType Directory -Force -Path $localeDir

    # data.json carries the emoji themselves; messages.json carries the localized group and
    # skin-tone names that group indices point into (docs/SPEC.md section 3.5).
    foreach ($file in @('data.json', 'messages.json')) {
        $target = Join-Path $localeDir $file

        if ((Test-Path $target) -and -not $Force) {
            $skipped++
            continue
        }

        $uri = "https://cdn.jsdelivr.net/npm/emojibase-data@$EmojibaseVersion/$locale/$file"
        Invoke-WebRequest -Uri $uri -OutFile $target -MaximumRetryCount 3 -RetryIntervalSec 2
        $downloaded++
    }

    Write-Host "  $locale" -NoNewline
    Write-Host " ok" -ForegroundColor Green
}

# English shortcodes are a separate axis entirely (docs/SPEC.md section 3.4) and the only locale
# with more than the two CLDR presets.
$shortcodeDir = Join-Path $root 'en' 'shortcodes'
$null = New-Item -ItemType Directory -Force -Path $shortcodeDir

foreach ($preset in @('cldr', 'cldr-native', 'emojibase', 'emojibase-legacy', 'github', 'iamcal', 'joypixels')) {
    $target = Join-Path $shortcodeDir "$preset.json"
    if ((Test-Path $target) -and -not $Force) { $skipped++; continue }

    $uri = "https://cdn.jsdelivr.net/npm/emojibase-data@$EmojibaseVersion/en/shortcodes/$preset.json"
    Invoke-WebRequest -Uri $uri -OutFile $target -MaximumRetryCount 3 -RetryIntervalSec 2
    $downloaded++
}

# Unicode's own conformance lists, used to check the catalog against an independent source rather
# than against the Emojibase corpus it was generated from.
#
# TWO files, because neither alone can check both directions. Unicode publishes emoji-test.txt
# under /Public/emoji/<version>/, which stops at 16.0, while /latest/ is 18.0 - there is no 17.0
# file matching emojibase 17.0.0.
#
#   16.0 - a strict SUBSET of our data, since emoji are never removed. Used to assert that every
#          emoji Unicode lists resolves through the catalog, including the minimally-qualified
#          and unqualified spellings (docs/SPEC.md section 3.6).
#
#   18.0 - a strict SUPERSET of our data. Used for the converse: every sequence SharpMoji ships
#          must be one Unicode actually defines. Checking that against 16.0 instead reports false
#          positives for anything introduced later, which is exactly the mistake that produced
#          the retracted claim in SPEC 3.8.
$conformanceVersions = @{
    '16.0' = 'https://www.unicode.org/Public/emoji/16.0/emoji-test.txt'
    '18.0' = 'https://www.unicode.org/Public/emoji/latest/emoji-test.txt'
}

foreach ($version in $conformanceVersions.Keys | Sort-Object) {
    $target = Join-Path $root "emoji-test-$version.txt"

    if ((-not (Test-Path $target)) -or $Force) {
        Invoke-WebRequest -Uri $conformanceVersions[$version] -OutFile $target `
            -MaximumRetryCount 3 -RetryIntervalSec 2
        $downloaded++
    } else {
        $skipped++
    }

    Write-Host "  emoji-test $version" -NoNewline
    Write-Host ' ok' -ForegroundColor Green
}

$size = (Get-ChildItem $root -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB

Write-Host ''
Write-Host ('Done: {0} downloaded, {1} already present, {2:N1} MB total.' -f $downloaded, $skipped, $size)
