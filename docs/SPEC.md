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
| Network | **None.** All data is embedded; the library makes no HTTP calls and does no file I/O |
| Platforms | Windows, macOS, Linux (Sourire is Windows-only; the library is not) |
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

**Hebrew (`he`) does not exist in Emojibase** — but it does exist upstream in CLDR, which is
where Emojibase gets its strings. `common/annotations/he.xml` carries 1975 base labels and
`common/annotationsDerived/he.xml` a further 2386 for skin-tone variants: complete coverage.

Hebrew is therefore generated at build time from CLDR directly (§7.4) and ships as a normal
embedded locale alongside the other 28. The same generator works for any CLDR locale
Emojibase does not publish, so this is a reusable capability rather than a Hebrew special case.

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

### 3.3 Four parsing hazards

Confirmed in Phase 0 by deserializing all 28 locales with
`JsonUnmappedMemberHandling.Disallow`:

1. **`skins[].tone` is polymorphic** — `1` for a single modifier, `[1, 2]` for two. Needs a
   converter.
2. **`emoticon` is polymorphic** — a string on 35 records (`":D"`), an array on 14
   (`["xD", "XD"]`). Needs a converter. *Found during Phase 0; the original three were three.*
3. **`text` is `""`** on 1590 records, meaning "no text presentation", not "empty string".
   Normalize to `null` — and the converter must set `HandleNull => true`, or
   System.Text.Json short-circuits the null on write and the round trip alters the data.
4. **`version` is an int *or* a float** (`1`, `0.6`) — but this needs **no converter**. Modeling
   it as `double` handles both natively. Only the draft's `int` would have thrown.

Optional fields are `tags`, `order`, `group`, `subgroup`, `skins`, `emoticon` and `gender` on
the top-level record, and `tags` and `gender` on skins. `gender` appearing on *both* levels is
easy to miss; Phase 0's strict unmapped-member handling is what caught it.

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

### 3.7 Structure is locale-independent — measured

Verified across all 28 locales: every locale has exactly **1949** records, in the same order,
with byte-identical `hexcode`, `emoji`, `text`, `type`, `order`, `group`, `subgroup`,
`version`, `gender`, `emoticon` and skin structure. **Zero** structural differences.

Only `label` and `tags` are localized.

This is the single most useful fact about the data, because it means the shipped form need
not be 28 copies of a full dataset. It is one shared structure table plus 28 small string
tables:

| | raw | gzipped |
|---|---|---|
| Shared structure (all locales) | 418 KB | **41 KB** |
| One locale's strings (`en`) | 301 KB | 53 KB |
| One locale's strings (`uk`, Cyrillic) | 508 KB | 67 KB |
| All 28 locale string tables | 9.6 MB | **1.48 MB** |
| **Complete bundle, 28 locales** | | **1.52 MB gzip / ~1.25 MB Brotli** |
| Plus generated Hebrew (§7.5), 29 total | | ~1.58 MB gzip / ~1.30 MB Brotli |

Against 26.5 MB for the naive approach of embedding 28 full `data.json` files. This is what
makes shipping every locale in the box practical, and it is why there is no download
subsystem (§7).

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
├── Data/
│   ├── EmbeddedPackReader.cs    # Brotli + source-gen JSON, lazy per locale
│   ├── StructureTable.cs        # shared across all locales (§3.7)
│   ├── StringTable.cs           # one per locale
│   └── SharpMojiData.cs         # EmojibaseVersion, UnicodeVersion constants
│
├── Serialization/
│   ├── EmojiJsonContext.cs      # source-generated
│   ├── ToneConverter.cs         # int | int[]
│   └── VersionConverter.cs      # int | float
│
└── Exceptions/                  # SharpMojiException + 2 subtypes
```

There is no `Packs/` namespace, no storage abstraction, no HTTP client and no integrity
manifest. Embedding every locale (§3.7) removes that entire subsystem along with its
failure modes: no network errors, no partial writes, no cache invalidation, no
platform-specific paths, no offline degradation.

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
// English. Cached; first access decompresses ~95 KB and parses.
IEmojiCatalog catalog = EmojiCatalog.English;

// Any other locale — also embedded, decompressed lazily on first use and then cached.
IEmojiCatalog uk = EmojiCatalog.Load("uk");
IEmojiCatalog he = EmojiCatalog.Load(EmojiLocale.Hebrew);
bool ok = EmojiCatalog.TryLoad(someUserString, out IEmojiCatalog? c);

IReadOnlyList<EmojiLocale> available = EmojiLocale.All;   // all 29, always present

// Escape hatch: load a pack you generated yourself (§7.5).
IEmojiCatalog custom = EmojiCatalog.LoadFrom(stream);
```

**The API is synchronous, deliberately.** With every locale embedded there is no I/O to
await — loading is a Brotli decompress plus a parse, single-digit milliseconds. Exposing
`Task`-returning methods here would be async-over-sync, which is worse than useless: it
costs a state machine and invites callers to believe there is latency to hide. A future
version that genuinely needs I/O can add async overloads without breaking anyone.

Construction is single-phase. A catalog handed to you is loaded.

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

**How the 25 variants are actually stored** — established in Phase 0, and not what this section
originally assumed. A dual-tone emoji's `skins` array is **5 single-tone entries plus 20
pairs**, not 25 pairs:

- Matching tones are encoded as **one** modifier applied to the whole sequence. Handshake with
  two light-skinned hands is `1F91D-1F3FB` with `"tone": 1` — there is no `[1, 1]`.
- Only the 20 mixed combinations are stored as pairs (`"tone": [1, 2]`).

Together they cover the 5×5 matrix exactly once. The consequence for the API is direct: asking
for `(Light, Light)` must resolve to the single-tone entry, so a lookup that searched for a
matching *pair* would find nothing. And a two-slot emoji does accept a single tone — meaning
"both the same" — which contradicts the rule this section first stated.

```csharp
int slots = catalog.GetSkinToneSlots("🤝");               // 0, 1, or 2
IReadOnlyList<EmojiSkin> skins = catalog.GetSkins("🤝");   // 25 for dual, 5 for single

bool ok  = catalog.TryGetSkin("👍", SkinTone.Medium, out var single);
bool ok2 = catalog.TryGetSkin("🤝", SkinTone.Light, SkinTone.MediumLight, out var dual);
// dual.Sequence == "🤝🏻‍🤝🏼" (the mixed pair)

// A two-slot emoji also accepts one tone, meaning both the same. This resolves to the
// single-modifier entry, not to a pair:
bool ok3 = catalog.TryGetSkin("🤝", SkinTone.Light, out var both);          // "🤝🏻"
bool ok4 = catalog.TryGetSkin("🤝", SkinTone.Light, SkinTone.Light, out var same);
// ok3 and ok4 return the same record: (Light, Light) normalizes to the single-tone entry.

// Passing two tones to a one-slot emoji returns false. It does not throw and does not guess.
```

`EmojiSkin.Tones` therefore carries one entry for a matching-tone variant and two for a mixed
one. Callers that want a uniform view should read `GetSkinToneSlots` rather than
`Tones.Length`.

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

- No method returns `List<T>`; collections are `IReadOnlyList<T>` or `ImmutableArray<T>`.
- Models are `sealed record` with `init` accessors. No public setters anywhere.
- Lookups that can miss are `Find…` returning `T?` or `TryGet…`. They do not throw.
- Any async method added later takes a `CancellationToken`. None exist in v1.0.

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

## 7. Data packaging and upgrades

### 7.1 Everything is embedded

Because the complete 28-locale bundle is 1.52 MB gzipped (§3.7), all data ships in the
package. There is no download path, no cache directory and no offline mode, because there is
no online mode. Consequences worth stating plainly:

- Nothing to go wrong at runtime: no network errors, no CDN outage, no corrupted cache, no
  first-run latency, no permissions problems.
- No privacy surface. The library cannot phone home because it has no code that could.
- Reproducible by construction — the data is a build input, not a runtime variable.
- Works in any sandbox, offline installs and air-gapped environments included.

Each locale is a separate Brotli-compressed embedded resource, decompressed on first use and
cached. An app that only ever touches English pays for English.

### 7.2 Package layout

Most consumers want one language. The bundle is split so they do not pay for 28:

| Package | Contents | Size |
|---|---|---|
| `Oire.SharpMoji` | Code, shared structure table, `en` strings | ~95 KB data |
| `Oire.SharpMoji.Locales` | The other 28 locale string tables | ~1.45 MB data |

The core package alone is fully functional in English. If the satellite package is present it
is discovered by assembly scan and `EmojiLocale.All` widens automatically; if absent,
`Load("uk")` throws `LocaleNotAvailableException` naming the package to install. One satellite
rather than 28 per-locale packages — 28 would be a publishing chore for no real benefit.

### 7.3 The upgrade mechanism

With no download path, a Unicode release reaches users as a package version. What that
requires is a maintainer-side pipeline, and this is the part the original draft had no answer
for at all:

1. **`scripts/update-emoji-data.ps1 -EmojibaseVersion <v>`** fetches the pinned version,
   regenerates the structure table, all string tables and the Hebrew pack (§7.4), and rewrites
   `SharpMojiData.EmojibaseVersion` / `UnicodeVersion`.
2. **The script emits a data diff** — emoji added, removed, relabeled, retagged, and any
   change to group or skin structure. This is what the CHANGELOG entry is written from, and
   what tells you whether a release is additive.
3. **A scheduled CI job** checks for new Emojibase releases weekly and opens an issue when one
   appears, so a Unicode update is not discovered by a user.
4. **Golden tests pin the data**: record count and a content hash. An accidental or partial
   regeneration fails CI instead of shipping.
5. **Versioning policy.** New emoji are additive to the data but do not change the API, so a
   Unicode update is a **minor** bump. A major bump is reserved for API changes. The data
   version is queryable at runtime via `SharpMojiData.UnicodeVersion`, so an app can display
   or log what it is carrying.

### 7.4 Stable keys — what consumers may persist

Sourire will store favorites and recents, so this needs to be explicit rather than discovered
the hard way after an upgrade:

- **`Hexcode` is the stable key.** Persist that.
- **`Order` is not stable.** It is a dense index that renumbers whenever Unicode inserts
  emoji. Treat it as opaque, use it for sorting within one session, never persist it.
- **Array positions are not stable** for the same reason.
- `Label` and `Tags` change with CLDR revisions and are display data, never keys.

### 7.5 Generating locales CLDR has but Emojibase lacks

`scripts/generate-cldr-locale.ps1 -Locale he` builds a locale pack directly from CLDR
`common/annotations/{loc}.xml` and `common/annotationsDerived/{loc}.xml`. It is tractable
precisely because of §3.7: structure is locale-independent, so the generator only has to
produce a string table. Per entry it reads `type="tts"` as the label and the pipe-separated
sibling annotation as tags, maps `cp` to a hexcode, and joins against the existing structure
table. Codepoints CLDR annotates but Emojibase does not carry (punctuation, some symbols) are
dropped; any emoji in the structure table with no CLDR annotation is reported rather than
silently blank.

This produces Hebrew, and any other CLDR locale, on the same footing as the rest.

### 7.6 Side-loading

`EmojiCatalog.LoadFrom(Stream)` accepts a pack in the library's own format. This keeps the
no-network guarantee while letting someone ship updated or private data without waiting on a
release. It is a small addition because the reader already exists.

---

## 8. Errors and logging

```
SharpMojiException
├── LocaleNotAvailableException     // unknown locale, or Oire.SharpMoji.Locales not installed
└── PackFormatException             // malformed side-loaded pack (§7.6)
```

Three of the draft's exception types described failures that can no longer occur, since
there is nothing to download and nothing to store.

Lookups never throw for "not found" — that is `null` or `false` (§5.5). Exceptions are for
genuinely exceptional conditions.

Logging via `ILogger` from `Microsoft.Extensions.Logging.Abstractions`, matching SharpSync.
Debug: cache hits, query timing. Information: locale table decompressed. Warning: unknown
locale requested. There is no error tier left worth logging — the failure modes it would have
covered were all network and storage.

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
| 0 | ~~Spike~~ **Done** | Deserialize all 28 locales; prove the converters (§3.3) | ✅ 95 tests, every locale round-trips losslessly |
| 1 | Build pipeline | Structure/string split, Brotli resources, `update-emoji-data` script | Bundle ≤ 1.6 MB; regeneration is reproducible |
| 2 | Model + catalog | Records, source-gen context, indexes, normalization | `emoji-test.txt` conformance passes |
| 3 | Skin tones | 1- and 2-slot lookup by indexing published variants | All 19 dual-tone emoji resolve all 25 variants |
| 4 | Groups + shortcodes | Localized `messages.json`, preset loading | `uk` returns Ukrainian group labels |
| 5 | Search | Index, ranking, diacritic folding | Golden corpus passes for 4 locales |
| 6 | Hebrew from CLDR | `generate-cldr-locale` script (§7.5) | `he` has full coverage; gaps reported, not blank |
| 7 | Package | README, XML docs, sample, NOTICE, CI, satellite package | Trimmed + AOT sample runs on 3 OSes |

Phase 0 is half a day and would have prevented most of this rewrite. It has now run, and earned
its place: it found a fourth polymorphic field (`emoticon`), a `gender` field on skins as well as
on records, and — the one that would have hurt — that the 25 skin variants of a dual-tone emoji
are 5 singles plus 20 pairs rather than 25 pairs, which invalidated this document's own
description of the lookup contract (§5.3). All three would have surfaced as bugs in Phase 3
otherwise.

Phase 0's parser lives in the test project rather than in `src/`. The shipped library reads
SharpMoji's own embedded format, not Emojibase's, so the Emojibase wire model is build-time
only; Phase 1 promotes it into the data tool. The `Serialization/` folder in §4 is for the
embedded-format reader.

Phase 1 moved ahead of the model because the structure/string split (§3.7) determines the
shape everything else consumes. Dropping the download subsystem removed a phase outright;
Hebrew adds one back, but a smaller one.

**Estimate:** 4 weeks / 80–100 h remains plausible, and is better founded than the draft's
identical number, which was costing unknowns. Phase 3 is the risk; phases 4–5 absorb overrun.
Phase 6 is independent and can slip past v1.0 without blocking release.

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
- Core package data payload under 150 KB; full bundle under 1.6 MB.
- Zero analyzer warnings with `TreatWarningsAsErrors`.
- Public API reviewed and frozen via `PublicApiAnalyzers`.

**Post-release**
- Sourire depends on the published package with no `InternalsVisibleTo` and no forked code.
- A Unicode 18 refresh is one script run, a diff review and a minor bump — no model changes.
  This is the specific thing §7.3 exists to guarantee.
- No correctness issue in the first release requiring a data reshape.

---

## 13. Beyond v1.0 — symbols

An accessible picker for mathematical operators, arrows, currency, chess, alchemy and braille
is a natural extension: same search, same ranking, same picker UI, different character set.
Assessed rather than assumed:

- **Source.** Unicode UCD `UnicodeData.txt` (2.2 MB, 41,341 assigned codepoints). Filtering to
  symbol categories (`Sm`, `Sc`, `Sk`, `So`) plus dashes gives **8,787 named characters** —
  **67 KB gzipped**. Comparable to two emoji locales. Size is not an obstacle.
- **The catch is localization.** UCD names are English-only formal names
  (`ALCHEMICAL SYMBOL FOR GOLD`). CLDR annotations cover symbols only patchily: Arrows 91/112
  and Mathematical Operators 94/256 are annotated, but Alchemical, Chess, Braille and
  Mathematical Alphanumeric are **0**. So symbol names would be English-only for most blocks,
  with localized names available for the arrows and common maths characters.
- **Shape.** A separate `Oire.SharpMoji.Symbols` package over the same `IEmojiCatalog`-style
  surface, not a widening of the emoji model — the two datasets share a search engine, not a
  schema. UCD carries no skin tones, no groups and no shortcodes; emoji carry no block or
  general category.
- **Braille** is worth calling out separately given Orbit work: the 256-codepoint Braille
  Patterns block is fully named in UCD and unannotated in CLDR, and dot-pattern search
  (`dots-1245`) would be a better query model than names anyway.

Not v1.0 scope. Recorded so the v1.0 search seam (`IEmojiMatcher`) is built wide enough to be
reused rather than rewritten.

---

## 14. Open questions

1. **Facade naming.** `EmojiCatalog` is used throughout rather than `SharpMoji`, because a
   type named `SharpMoji` inside namespace `Oire.SharpMoji` triggers CA1724 and constant
   ambiguity. Trivial to rename now, breaking later.
2. **Satellite granularity.** One `Oire.SharpMoji.Locales` package is assumed (§7.2). Revisit
   only if 1.45 MB proves to matter to someone.

Resolved since the draft: Hebrew (§3.1, §7.5 — generated from CLDR), platform scope (Windows,
macOS, Linux; Sourire is Windows-only but the library is not), and downloads (§7.1 — removed).

---

## 15. Next steps

1. ~~Create `Oire/sharp-moji`~~ — done, private until v1.0.
2. Scaffold the solution to SharpSync's conventions (csproj properties, GitVersion,
   analyzers, workflows, `CLAUDE.md`).
3. Run Phase 0 — half a day, and it de-risks everything after it.
4. Build the Phase 1 pipeline before writing model code; it fixes the shape everything reads.
