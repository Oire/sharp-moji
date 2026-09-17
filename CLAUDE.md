# CLAUDE.md

Guidance for Claude Code (claude.ai/code) when working in this repository.

## What this is

SharpMoji is a .NET 10 library for emoji reference data: lookup, localized search, skin-tone
variants and canonical ordering. It is the data layer for Sourire (accessible emoji input,
Windows-only) and a general-purpose NuGet package. The library itself targets Windows, macOS
and Linux.

**Read [docs/SPEC.md](docs/SPEC.md) before changing anything.** It is verified against the real
data, and several obvious-looking design choices in this domain are wrong. The ones that bite:

- Emoji **structure is identical across all locales** — only `label` and `tags` are localized.
  The whole packaging design depends on this (SPEC section 3.7).
- **19 emoji take two skin-tone modifiers**, not one, and have 25 variants. Never synthesize
  tone sequences by inserting modifiers; index the published variants (SPEC section 5.3).
- **517 of 1949 records carry U+FE0F.** Sequence lookups must normalize it away on both sides
  (SPEC section 3.6).
- `skins[].tone` is `int` *or* `int[]`; `version` is `int` *or* `float` (SPEC section 3.3).
- **`Hexcode` is the only stable key.** `Order` renumbers between Unicode versions and must
  never be persisted (SPEC section 7.4).

## Commands

```bash
dotnet build                                   # build the solution
dotnet build -c Release
dotnet test                                    # run all tests
dotnet test tests/SharpMoji.Tests/SharpMoji.Tests.csproj
dotnet format --verify-no-changes              # CI runs this; it must pass
dotnet pack -c Release                         # GitVersion sets the version
```

Trimming and AOT are part of the contract, so the sample must publish clean:

```bash
dotnet publish samples/SharpMoji.Samples.Console -c Release -p:PublishAot=true
```

## Solution layout

The solution file is **`SharpMoji.slnx`** (XML format), not a `.sln`.

| Project | Purpose |
|---|---|
| `src/SharpMoji` | The library and all 28 embedded languages. The only shipped package. |
| `tools/SharpMoji.DataTool` | Build-time pack generator. Never shipped, never packable. |
| `tests/SharpMoji.Tests` | xUnit + FluentAssertions. |
| `samples/SharpMoji.Samples.Console` | Demo, and the trim/AOT smoke test. |

## Conventions

- `net10.0` only. No multi-targeting, no .NET Standard.
- Namespaces are `Oire.SharpMoji.*`. The assembly and package ID are both `Oire.SharpMoji`.
- `TreatWarningsAsErrors` is on for `src` and `samples`. Do not suppress a warning without a
  comment saying why.
- Public models are `sealed record` with `init` accessors. No public setters, no `List<T>` on
  the public surface — use `IReadOnlyList<T>` or `ImmutableArray<T>`.
- **The public API is synchronous.** All data is embedded, so there is no I/O to await; adding
  `async` here would be async-over-sync.
- All JSON goes through the source-generated `JsonSerializerContext`. Reflection-based
  `JsonSerializer` calls break trimming and are not allowed.
- American English in code, comments and documentation.
- XML documentation comments on every public member.

## Things that do not exist, deliberately

There is no HTTP client, no download path, no cache directory, no storage abstraction and no
integrity manifest. Every language is embedded (1,360 KB for all 28). If a task seems to call for
fetching data at run time, re-read SPEC section 7 first — the answer is almost certainly a
build-time script in `scripts/` instead.

There is also **no satellite locale package**, and reintroducing one would be a regression.
Anything discovered by assembly name at run time is deleted by `PublishTrimmed` and NativeAOT,
because nothing statically references it — silently, leaving English only. Phase 1 tried exactly
that and measured the failure (SPEC section 7.2).

## Data regeneration

```powershell
./scripts/fetch-emoji-corpus.ps1                # test fixtures, ~22 MB, gitignored
./scripts/update-emoji-data.ps1                 # regenerate the packs and print a data diff
./scripts/generate-cldr-locale.ps1 -Locale he   # locales Emojibase lacks (Phase 6)
```

All three are build-time only and must not become runtime code. After regenerating, update
`SharpMojiData` and the pinning tests together, and write the CHANGELOG entry from the diff the
script prints.

## Licensing

Code is Apache-2.0. The embedded data is **not**: it is MIT (Emojibase) and Unicode-3.0 (CLDR).
`NOTICE` must ship inside the NuGet package. Do not add a dependency whose license would
complicate that.
