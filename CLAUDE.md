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
| `src/SharpMoji` | The library. Bundles English data. |
| `src/SharpMoji.Locales` | Satellite package with the other 28 locales. |
| `tests/SharpMoji.Tests` | xUnit + FluentAssertions. |
| `samples/SharpMoji.Samples.Console` | Demo, and the trim/AOT smoke test. |

## Conventions

- `net10.0` only. No multi-targeting, no .NET Standard.
- Namespaces are `Oire.SharpMoji.*`. Assembly and package IDs are `Oire.SharpMoji[.Locales]`.
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
integrity manifest. Every locale is embedded (1.6 MB for all 29). If a task seems to call for
fetching data at run time, re-read SPEC section 7 first — the answer is almost certainly a
build-time script in `scripts/` instead.

## Data regeneration

```powershell
./scripts/update-emoji-data.ps1 -EmojibaseVersion 17.0.0   # refresh from Emojibase
./scripts/generate-cldr-locale.ps1 -Locale he              # locales Emojibase lacks
```

Both are build-time only and must not become runtime code. After regenerating, update
`SharpMojiData` and the pinning tests together, and write the CHANGELOG entry from the diff the
script prints.

## Licensing

Code is Apache-2.0. The embedded data is **not**: it is MIT (Emojibase) and Unicode-3.0 (CLDR).
`NOTICE` must ship inside both NuGet packages. Do not add a dependency whose license would
complicate that.
