#Requires -Version 7.0
<#
.SYNOPSIS
    Builds a SharpMoji locale pack directly from CLDR, for locales Emojibase does not publish.

.DESCRIPTION
    Build-time only. Emojibase ships 28 locales; CLDR carries annotations for many more, Hebrew
    among them (docs/SPEC.md section 3.1, section 7.5).

    This is tractable only because emoji structure is locale-independent (SPEC section 3.7): the
    generator produces a string table and joins it to the existing shared structure table. It
    never has to reason about groups, ordering or skin sequences.

    Planned behavior:

      1. Fetch common/annotations/<Locale>.xml and common/annotationsDerived/<Locale>.xml.
      2. For each <annotation> element: type="tts" is the label; the sibling without a type is
         the pipe-separated tag list.
      3. Map the cp attribute (the literal sequence) to a hexcode, normalizing U+FE0F.
      4. Join against the shared structure table, keeping only codepoints SharpMoji carries.
         CLDR annotates punctuation and some symbols that are not emoji; those are dropped.
      5. Report any emoji in the structure table with no CLDR annotation, rather than emitting a
         blank label. Coverage gaps must be visible.
      6. Emit a Brotli-compressed string table in the same format as the Emojibase-derived ones.

    For reference, CLDR he coverage at the time of writing: 1975 base labels and 2386 derived.

.PARAMETER Locale
    CLDR locale code, for example "he".

.PARAMETER CldrRef
    The CLDR git ref to read from. Pin a release tag for reproducibility.

.EXAMPLE
    ./scripts/generate-cldr-locale.ps1 -Locale he
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[a-z]{2,3}(-[A-Za-z0-9]+)*$')]
    [string] $Locale,

    [string] $CldrRef = 'main',

    [string] $OutputPath = (Join-Path $PSScriptRoot '..' 'src' 'SharpMoji.Locales' 'Resources')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

throw 'Not implemented yet — this is Phase 6 (see docs/SPEC.md section 10).'
