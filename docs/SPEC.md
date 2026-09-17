# SharpMoji — Technical Specification

**Package:** `Oire.SharpMoji`
**Repository:** https://github.com/Oire/sharp-moji
**Author:** André Polykanine (Oire Software)
**Status:** Spec v0.1 — supersedes `SharpMoji-Project-Plan.md` (Feb 2026 draft)
**Data source:** `emojibase-data` **17.0.0** (pinned), verified against the live package on 2026-09-17

---

## 1. Scope

A .NET 10 library providing emoji reference data: lookup, localized search, skin-tone
variants, and canonical ordering. It is the data layer for **Sourire** (accessible emoji
input) and a general-purpose package for the .NET ecosystem.

**Non-goals for v1.0:** emoji rendering, images or fonts, custom/user emoji, usage
analytics, cloud sync, semantic search.

### 1.1 What changed from the Feb 2026 draft

The draft's data model was written without reading the source data. Every field name in it
was wrong, and its skin-tone API could not represent 19 of the emoji it was meant to handle.
The corrections are recorded in §3 and §6. The draft's phase structure, provider
abstractions, and exception hierarchy survive largely intact.

---

## 2. Target and dependencies

| Item | Decision |
|---|---|
| TFM | `net10.0` only — single target, no multi-targeting, no .NET Standard |
| Nullable | `enable`, `TreatWarningsAsErrors` |
| Namespace | `Oire.SharpMoji` |
| Package ID | `Oire.SharpMoji` |
| License (code) | Apache-2.0 (see §11) |
| Runtime deps | `Microsoft.Extensions.Logging.Abstractions` only |
| Analyzers | `Microsoft.CodeAnalysis.NetAnalyzers`, `Roslynator.Analyzers`, `Microsoft.CodeAnalysis.PublicApiAnalyzers` |
| Versioning | `GitVersion.MsBuild`, ManualDeployment, `v*` tags — as in SharpSync |

`System.Net.Http` and `System.Text.Json` are in-box on .NET 10 and are **not** package
references.

**Trimming and AOT:** the library sets `IsAotCompatible` and `IsTrimmable`. All JSON goes
through a source-generated `JsonSerializerContext`; reflection-based `JsonSerializer`
overloads are banned (enforced by a banned-symbols analyzer entry). Sourire is a desktop
app that will want trimming, so this is a v1.0 requirement, not a nicety.

---

## 3. The data source — verified facts

Everything below was confirmed against `emojibase-data@17.0.0`, not assumed.

### 3.1 Locales actually available

```
bn da de en en-gb es es-mx et fi fr hi hu it ja ko lt ms nb nl pl pt ru sv th uk vi zh zh-hant
```

**Hebrew (`he`) does not exist in Emojibase.** The original goal of "7 languages including
Hebrew with RTL support" is not achievable from this data source. Six of the seven
(`en fr uk ru de es`) ship as normal locale packs; Hebrew is deferred (§13).

RTL is a *rendering* concern and is the consuming app's responsibility. The library stays
direction-agnostic: it returns strings and never composes display text. It does expose
`EmojiLocale.IsRightToLeft` so a UI can make that decision without a second lookup table.

### 3.2 Record shape

Top-level fields, exhaustively: `label`, `hexcode`, `tags`, `emoji`, `text`, `type`,
`order`, `group`, `subgroup`, `version`, `skins`, `emoticon`, `gender`.

There is no `name`, no `unicode`, no `keywords`, no `shortcodes`, no `category` field.

Counts for `en`: **1949** base records, **3979** including skin variants. 9 records are
group 2 (`components` — bare modifiers, not pickable). **26 records have no `group` at
all**, so the group property must be nullable.

### 3.3 Three parsing hazards

These are load-bearing and each needs a custom `JsonConverter`:

1. **`skins[].tone` is polymorphic** — `1` for single-tone emoji, `[1, 2]` for dual-tone.
2. **`version` is an int *or* a float** (`1`, `0.6`). Model as `double`.
3. **`text` is `""`** on 1590 records, meaning "no text presentation", not "empty string".
   Normalize to `null`.

### 3.4 Shortcodes are a separate data axis

Shortcodes are **not** in `data.json`. They live in `{locale}/shortcodes/{preset}.json`,
keyed by hexcode. `en` has seven presets (`cldr`, `cldr-native`, `emojibase`,
`emojibase-legacy`, `github`, `iamcal`, `joypixels`); every other locale has only `cldr`
and `cldr-native`.

This means shortcode support needs its own load path, its own cache, and a preset-selection
API. Default preset is `cldr` — it is the only one present in all locales.

### 3.5 Groups are localized integers

`group` and `subgroup` are integer indices into `{locale}/messages.json`, which carries the
**localized** group names (10 groups, 101 subgroups) and skin-tone names. A Ukrainian picker
needs Ukrainian category headings, and those come from this file — the draft missed it
entirely. `messages.json` is therefore part of every locale pack, not an optional extra.

A hard `enum Category` is wrong: Unicode adds groups. Groups are exposed as a record with
an integer key, a localized label, and an order.

### 3.6 Sequence qualification

**517 of 1949** records carry U+FE0F (VS16) in their `emoji` field — thumbs-up is
`[U+1F44D, U+FE0F]`. A user pasting bare `U+1F44D` will miss a naive dictionary lookup.

All sequence-keyed indexes are built on a **normalized key**: VS16 stripped. Lookups
normalize the input the same way. `Emoji.Sequence` still returns the fully-qualified form
as published.

---

## 4. Architecture

The draft's single mutable `SharpMoji` facade with an ambient `SetLanguage()` is replaced.
State that changes under concurrent readers is the main source of bugs in a library like
this, so there is none.

```
Oire.SharpMoji/
├── EmojiCatalog.cs              # immutable, thread-safe, ONE locale
├── IEmojiCatalog.cs             # the surface consumers mock
├── EmojiCatalogFactory.cs       # static async entry points
│
├── Model/
│   ├── Emoji.cs                 # record
│   ├── EmojiSkin.cs             # record — 1 or 2 tones
│   ├── EmojiGroup.cs            # record — localized, int-keyed
│   ├── SkinTone.cs              # enum
│   ├── EmojiPresentation.cs     # enum (Text = 0, Emoji = 1)
│   └── EmojiLocale.cs           # code, English + native name, IsRightToLeft
│
├── Search/
│   ├── IEmojiMatcher.cs         # seam for fuzzy matching in v1.1
│   ├── RankedMatcher.cs         # default, deterministic ranking
│   └── SearchIndex.cs           # prebuilt, immutable
│
├── Packs/
│   ├── ILocalePackStore.cs      # read/write locale packs
│   ├── FileSystemPackStore.cs   # default
│   ├── InMemoryPackStore.cs     # sandboxed platforms, tests
│   ├── ILocalePackSource.cs     # where packs come from
│   ├── HttpPackSource.cs        # opt-in CDN download
│   └── BundledPackSource.cs     # embedded en, always available
│
├── Serialization/
│   ├── EmojiJsonContext.cs      # source-generated
│   ├── ToneConverter.cs         # int | int[]
│   └── VersionConverter.cs      # int | float
│
└── Exceptions/                  # SharpMojiException + 4 subtypes
```

### 4.1 Why per-locale catalogs

`EmojiCatalog` is immutable and bound to one locale. Getting another locale gives you
another catalog object. Consequences:

- Thread safety is structural, not documented-and-hoped-for.
- No `SetLanguage()` / `Search(language:)` precedence ambiguity — there is one mechanism.
- A search index per catalog can be built once at load and shared freely.
- Sourire can hold `en` and `uk` simultaneously and search both, which the draft's ambient
  design made awkward.

---

## 5. Public API

### 5.1 Entry points

```csharp
// Bundled English. No file I/O, no network. The zero-config path.
IEmojiCatalog catalog = await EmojiCatalogFactory.CreateBundledAsync(ct);

// Full control.
var options = new SharpMojiOptions
{
    PackStore  = new FileSystemPackStore(),      // or InMemoryPackStore
    PackSource = new HttpPackSource(httpClient), // opt-in; null = bundled only
    Shortcodes = ShortcodePreset.Cldr,
    Logger     = logger,
};
var factory = new EmojiCatalogFactory(options);

IEmojiCatalog uk = await factory.GetAsync("uk", ct);
bool installed  = factory.IsInstalled("uk");
IReadOnlyList<EmojiLocale> available = EmojiLocale.All;   // the 29 real locales
```

Construction is single-phase: there is no `new` followed by `InitializeAsync`. A catalog
handed to you is loaded.

### 5.2 Lookup and enumeration

```csharp
Emoji? byChar  = catalog.Find("👍");          // qualified or not — normalized internally
Emoji? byHex   = catalog.FindByHexcode("1F44D");
Emoji? byShort = catalog.FindByShortcode("thumbsup");
Emoji? byEmoticon = catalog.FindByEmoticon(":-)");   // 49 records carry one

IReadOnlyList<Emoji> all = catalog.All;               // canonical `order`, components excluded
IReadOnlyList<Emoji> withComponents = catalog.AllIncludingComponents;
IReadOnlyList<EmojiGroup> groups = catalog.Groups;    // localized labels
IReadOnlyList<Emoji> inGroup = catalog.GetByGroup(groups[0]);
```

`All` is ordered by the data's `order` field — canonical picker order. The draft ignored
`order`, which would have left Sourire sorting emoji by accident of file layout.

### 5.3 Skin tones — the part the draft got wrong

19 emoji (🤝, 👯, 🧑‍🤝‍🧑, …) take **two** modifiers and have **25** variants, with hexcodes
like `1FAF1-1F3FB-200D-1FAF2-1F3FC`. `ApplySkinTone(string, SkinTone)` cannot express that,
and a 6-element variant list is wrong for them.

The fix is also the simplification: **never synthesize sequences by inserting modifiers.**
The data already contains every valid variant. Index it and look it up. This sidesteps ZWJ
surgery entirely and is correct by construction.

```csharp
int slots = catalog.GetSkinToneSlots("🤝");               // 0, 1, or 2
IReadOnlyList<EmojiSkin> skins = catalog.GetSkins("🤝");   // 25 for dual, 5 for single

bool ok  = catalog.TryGetSkin("👍", SkinTone.Medium, out var single);
bool ok2 = catalog.TryGetSkin("🤝", SkinTone.Light, SkinTone.MediumLight, out var dual);
// dual.Sequence == "🫱🏻‍🫲🏼"

// Asking for two tones on a one-slot emoji, or one on a two-slot emoji, returns false.
// It does not throw and does not guess.
```

```csharp
public enum SkinTone { None = 0, Light = 1, MediumLight = 2, Medium = 3, MediumDark = 4, Dark = 5 }
```

Values match the data's `tone` field exactly, so no mapping table is needed.

### 5.4 Search

```csharp
IReadOnlyList<EmojiMatch> results = catalog.Search("sourire");
IReadOnlyList<EmojiMatch> top5    = catalog.Search("smile", limit: 5);
```

Ranking is deterministic and specified, in descending priority:

1. Exact shortcode match
2. Exact label match
3. Label prefix match
4. Exact tag match
5. Label substring match
6. Tag substring match

Ties break by `order` ascending, so results are stable across runs. `EmojiMatch` exposes the
matched field and rank so a UI can explain or group results.

Matching is case- and diacritic-insensitive (`CompareOptions.IgnoreCase | IgnoreNonSpace`)
using the **catalog's** culture, not the ambient one — otherwise a Turkish user's locale
silently changes English results.

Fuzzy matching is v1.1, but `IEmojiMatcher` is the seam for it in v1.0 so it does not become
a breaking change.

### 5.5 Conventions

- Every async method takes a `CancellationToken`. No exceptions.
- No method returns `List<T>`; collections are `IReadOnlyList<T>` or `ImmutableArray<T>`.
- Models are `sealed record` with `init` accessors. No public setters anywhere.
- Lookups that can miss are `Find…` returning `T?` or `TryGet…`. They do not throw.
- `HttpClient` is always supplied by the caller. The library never constructs or disposes one.

---

## 6. Data model

```csharp
public sealed record Emoji
{
    public required string Sequence { get; init; }          // "👍️" — as published, may carry VS16
    public required string Hexcode { get; init; }           // "1F44D"
    public required string Label { get; init; }             // localized
    public ImmutableArray<string> Tags { get; init; }       // localized
    public string? TextSequence { get; init; }              // `text`; null when absent
    public EmojiPresentation Presentation { get; init; }    // `type`
    public int Order { get; init; }
    public EmojiGroup? Group { get; init; }                 // NULLABLE — 26 records lack it
    public EmojiSubgroup? Subgroup { get; init; }
    public double UnicodeVersion { get; init; }             // 0.6 — NOT an int
    public string? Emoticon { get; init; }
    public EmojiGender? Gender { get; init; }
    public ImmutableArray<EmojiSkin> Skins { get; init; }
}

public sealed record EmojiSkin
{
    public required string Sequence { get; init; }
    public required string Hexcode { get; init; }
    public required string Label { get; init; }
    public ImmutableArray<SkinTone> Tones { get; init; }    // 1 or 2 entries
    public int Order { get; init; }
}
```

Side-by-side with the draft, for the record:

| Draft | Reality |
|---|---|
| `Character` | `emoji` → `Sequence` |
| `Unicode` | `hexcode` → `Hexcode` |
| `Name` | `label` → `Label` (localized) |
| `Keywords` | `tags` → `Tags` (localized) |
| `Shortcodes` | separate files, separate axis (§3.4) |
| `Category` enum | `group` int + localized `messages.json` |
| `Group`/`Subgroup` strings | ints |
| `int Version` | `double` |
| `List<string> SkinToneVariants` | `skins[]` of full objects, 1–2 tones each |

---

## 7. Packs, storage and network

### 7.1 Defaults are offline

Out of the box SharpMoji does no file I/O and no network access: bundled `en` is an embedded,
Brotli-compressed resource (`data.json` ~775 KB raw, ~97 KB compressed, plus
`messages.json` and `shortcodes/cldr.json`). Downloading is **opt-in** by supplying an
`HttpPackSource`. A library that silently phones a CDN on first use is not something an
accessibility app should inherit by default.

### 7.2 Pinned and verified

The CDN URL pins an exact version — never `@latest`, which makes builds non-reproducible and
silently changes data under users:

```
https://cdn.jsdelivr.net/npm/emojibase-data@17.0.0/{locale}/data.json
```

The library ships a manifest of **SHA-256 hashes** for every pinned file (jsDelivr publishes
these). Downloads are verified against it before being written to the store; a mismatch
throws `PackIntegrityException` and nothing is persisted. A CDN compromise would otherwise
feed arbitrary text straight into a UI.

### 7.3 Storage

`FileSystemPackStore` writes under `Environment.SpecialFolder.LocalApplicationData` +
`Oire/SharpMoji/{emojibaseVersion}/`, respecting `XDG_DATA_HOME` on Linux. Version-scoping
the directory makes data upgrades a non-event.

`InMemoryPackStore` exists for sandboxed or read-only targets and for tests. Platform support
is Windows, macOS and Linux for v1.0; the store abstraction is what will make MAUI/iOS/WASM
cheap later, and **this should be confirmed against Sourire's roadmap before v1.0 ships**.

### 7.4 Refreshing data

`scripts/update-emoji-data.ps1` pulls a named emojibase version, regenerates the embedded
resources and the hash manifest, and updates the pinned constant. A test asserts the embedded
data matches the pinned version, so a partial refresh fails CI rather than shipping.

---

## 8. Errors and logging

```
SharpMojiException
├── LocaleNotAvailableException     // not one of the 29 — lists what is
├── PackDownloadException           // transport, HTTP status, timeout
├── PackIntegrityException          // SHA-256 mismatch or malformed JSON
└── PackStoreException              // I/O, permissions
```

Lookups never throw for "not found" — that is `null` or `false` (§5.5). Exceptions are for
genuinely exceptional conditions.

Logging via `ILogger` from `Microsoft.Extensions.Logging.Abstractions`, matching SharpSync.
Debug: cache hits, query timing. Information: pack load and download. Warning: fallback to
bundled `en`. Error: download and store failures.

---

## 9. Testing and acceptance

### 9.1 Correctness against an independent source

Unit tests alone cannot catch the class of error that sank the draft. The suite validates
bundled data against **`emoji-test.txt` from unicode.org** — the authoritative list — as
golden data. Required assertions:

- Every fully-qualified sequence in `emoji-test.txt` for the supported Unicode version
  resolves via `Find()`.
- Both qualified and unqualified forms resolve to the same record (the VS16 case, §3.6).
- Every emoji with skin-tone variants reports the correct slot count; all 19 dual-tone emoji
  return exactly 25 variants and every `(tone, tone)` pair resolves.
- The 26 group-less and 9 component records load without error and are excluded from `All`.
- Round-tripping every record through the source-generated serializer is lossless.

### 9.2 Search quality

Search quality gets an actual criterion, which the draft lacked entirely: a golden corpus of
≥100 query→expected-top-N cases across `en`, `fr`, `uk` and `ru`, including diacritics and
Cyrillic. Regressions fail the build. This matters more than the draft's `<50ms` target —
the dataset is 1949 records, so speed is not the risk; relevance is.

### 9.3 Performance

Benchmarked with BenchmarkDotNet, reported rather than asserted, except two gates chosen to
catch algorithmic regressions rather than to look impressive:

- Search p99 under 10 ms on the CI runner for single-word queries.
- Bundled catalog load under 250 ms cold.

### 9.4 CI

GitHub Actions matrix: `windows-latest`, `macos-latest`, `ubuntu-latest` on `net10.0` —
matching the actual target, unlike the draft's net462/net6.0/net8.0 matrix, which included a
Windows-only framework in a cross-platform library. Trimming and NativeAOT smoke-test
projects publish and run on every build. `PublicApiAnalyzers` makes any public surface change
a reviewed diff.

---

## 10. Phases

Reordered from the draft on one principle: **validate the data and settle the hardest design
before committing to everything downstream.** The draft put schema work in Phase 1 as "basic
JSON deserialization" and skin tones at Phase 5 of 7 — which is exactly how its data model
came to be wrong in every field and its skin-tone API came to be unimplementable.

| # | Phase | Content | Exit criteria |
|---|---|---|---|
| 0 | Spike | Deserialize all 29 locales; prove the three converters (§3.3) | Every locale round-trips losslessly |
| 1 | Model + serialization | Records, source-gen context, embedded `en` | `emoji-test.txt` conformance passes |
| 2 | Catalog + skin tones | Indexes, normalization, 1- and 2-slot lookup | All 19 dual-tone emoji resolve all 25 variants |
| 3 | Groups + shortcodes | Localized `messages.json`, preset loading | `uk` returns Ukrainian group labels |
| 4 | Search | Index, ranking, diacritic folding | Golden corpus passes for 4 locales |
| 5 | Packs | Store, HTTP source, hashes, progress, cancellation | Integrity failure persists nothing |
| 6 | Package | README, XML docs, sample, NOTICE, CI, NuGet | Trimmed + AOT sample runs on 3 OSes |

Phase 0 is half a day and would have prevented most of this rewrite.

**Estimate:** the draft's 4 weeks / 80–100 h is plausible for phases 1–6 *now that the data
questions are answered*, but it was not plausible before, because it was costing an unknown.
Phase 2 is the risk; if it runs long, phases 3–4 absorb it.

---

## 11. Licensing — resolved

**Apache-2.0 is permitted.** Checked, not assumed:

- `emojibase-data@17.0.0` is **MIT** (© 2017–2019 Miles Johnson). MIT permits redistribution
  and sublicensing; keeping the notice satisfies it.
- The underlying CLDR data is under the **Unicode License v3** (`Unicode-3.0`,
  OSI-approved), which grants use, copy, modify, publish, distribute and sell "provided that
  either (a) this copyright and permission notice appear with all copies… or (b) this
  copyright and permission notice appear in associated Documentation."

The restrictive clause in the CLDR repository's LICENSE applies to the **LDML specification
document** (UTS #35), not the data files. SharpMoji redistributes data, not the spec.

Required deliverables:

- `LICENSE` — Apache-2.0, covering Oire's code.
- `NOTICE` / `THIRD-PARTY-NOTICES.md` — the emojibase MIT notice and the full Unicode-3.0
  copyright and permission notice, packed into the `.nupkg` (it embeds the data, so the
  notice must travel with it).
- `PackageLicenseExpression` stays `Apache-2.0`; the notice file covers the data.

Worth a second read by you before the repo goes public, since I am not a lawyer — but the
terms are permissive and the obligation is attribution only.

---

## 12. Success criteria

The draft's metrics (>100 downloads, >50 stars, 80% coverage) measure attention and not
quality, and download counts are mostly CI. Replaced with:

**Ship gates**
- `emoji-test.txt` conformance: 100%, all 29 locales load.
- Search golden corpus passes for `en`, `fr`, `uk`, `ru`.
- Trimmed and NativeAOT samples run on Windows, macOS, Linux.
- Zero analyzer warnings with `TreatWarningsAsErrors`.
- Public API reviewed and frozen via `PublicApiAnalyzers`.

**Post-release**
- Sourire depends on the published package with no `InternalsVisibleTo` and no forked code.
- A Unicode 18 data refresh is a script run plus a version bump — no model changes.
- No correctness issue in the first release requiring a data reshape.

---

## 13. Open questions

1. **Hebrew.** Not in Emojibase (§3.1). Options: hand-authored pack, direct CLDR extraction,
   or drop from v1.0 scope. Needs a decision — it was a stated goal.
2. **Mobile/WASM.** Out of scope for v1.0; confirm against Sourire's roadmap before the
   storage defaults harden (§7.3).
3. **Facade naming.** `EmojiCatalog` is used throughout rather than `SharpMoji`, because a
   type named `SharpMoji` inside namespace `Oire.SharpMoji` triggers CA1724 and constant
   ambiguity. Trivial to rename now, breaking later.

---

## 14. Next steps

1. Create `Oire/sharp-moji`, Apache-2.0, `.gitignore` for VisualStudio.
2. Scaffold the solution to SharpSync's conventions (csproj properties, GitVersion,
   analyzers, workflows, `.editorconfig`, `CLAUDE.md`).
3. Run Phase 0 — it is half a day and de-risks everything after it.
4. Settle the Hebrew question (§13.1) before advertising locale coverage.
