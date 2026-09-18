[![Build](https://github.com/Oire/sharp-moji/actions/workflows/dotnet.yml/badge.svg)](https://github.com/Oire/sharp-moji/actions/workflows/dotnet.yml)
[![codecov](https://codecov.io/gh/Oire/sharp-moji/graph/badge.svg)](https://codecov.io/gh/Oire/sharp-moji)
[![NuGet](https://img.shields.io/nuget/v/Oire.SharpMoji)](https://www.nuget.org/packages/Oire.SharpMoji)
[![Downloads](https://img.shields.io/nuget/dt/Oire.SharpMoji)](https://www.nuget.org/packages/Oire.SharpMoji)
[![License](https://img.shields.io/github/license/Oire/sharp-moji)](https://github.com/Oire/sharp-moji/blob/master/LICENSE)

# SharpMoji

Emoji data for .NET: lookup, localized search, skin tones and canonical ordering — in 28
languages, with no network access and no file I/O.

> **Status: not yet released.** The API is complete and tested. Hebrew support and the 1.0
> release are outstanding. See [the specification](https://github.com/Oire/sharp-moji/blob/master/docs/SPEC.md).

SharpMoji is the data layer for Sourire (accessible emoji input) and a general-purpose emoji
package for the .NET ecosystem.

## Why another emoji library

Everything is in the package. There is no HTTP client, no cache directory, no storage
abstraction and no first-run download — all 28 languages are embedded in about 1.4 MB. Nothing
can fail at run time, nothing phones home, and it works offline, in a sandbox, and in an
air-gapped build.

It is also correct about the parts that are easy to get wrong; see
[Details that bite](#details-that-bite).

## Install

```
dotnet add package Oire.SharpMoji
```

Targets .NET 10. Trimming- and NativeAOT-compatible.

## Usage

```csharp
using Oire.SharpMoji;

var emoji = EmojiCatalog.English;

emoji.Find("👍")?.Label;              // "thumbs up" — with or without the variation selector
emoji.FindByHexcode("1F600")?.Label;  // "grinning face"
emoji.FindByShortcode(":+1:")?.Label; // "thumbs up"
emoji.FindByEmoticon(":D")?.Label;    // "grinning face with smiling eyes"
```

Catalogs are immutable, cached per language, and safe to share across threads. Every member is
synchronous: the data is embedded, so there is no I/O to await.

### Browsing

```csharp
foreach (var group in emoji.Groups.Where(g => !g.IsComponent)) {
    Console.WriteLine($"{group.Name}: {emoji.GetByGroup(group).Length} emoji");
}
```

`All` is in canonical display order and excludes components — the bare skin-tone and hair
modifiers, which are building blocks rather than anything a person picks.

### Search

```csharp
foreach (var match in emoji.Search("thum", limit: 5)) {
    Console.WriteLine($"{match.Emoji.Sequence}  {match.Emoji.Label}  ({match.Kind})");
}
// 👍  thumbs up     (LabelPrefix)
// 👎  thumbs down   (LabelPrefix)
```

Searches labels, tags, shortcodes and emoticons. Case and diacritics are ignored, so `cafe`
finds `café`, and `:-)` finds `:)`. Each result reports **why** it matched, so a UI can group or
highlight accordingly.

### Skin tones

```csharp
emoji.GetSkinToneSlots("👍");   // 1
emoji.GetSkinToneSlots("🤝");   // 2 — two people, toned independently
emoji.GetSkinToneSlots("😀");   // 0

emoji.TryGetSkin("👍", SkinTone.Medium, out var skin);
skin.Sequence;   // "👍🏽"
skin.Label;      // "thumbs up: medium skin tone"

// Nineteen emoji take two modifiers. Pass both; matching tones resolve correctly.
emoji.TryGetSkin("🤝", SkinTone.Light, SkinTone.MediumLight, out var pair);
```

### Other languages

```csharp
var uk = EmojiCatalog.Load("uk");

uk.FindByHexcode("1F44D")?.Label;   // "великі пальці вгору"
uk.Groups[1].Name;                  // "люди"
uk.Search("серце", 5);              // works in Ukrainian
uk.FindByShortcode(":+1:");         // shortcodes work in every language

EmojiLocale.All;                    // all 28, with English and native names
```

## Details that bite

These are the things a naive emoji library gets wrong. Each is measured rather than assumed —
the numbers are in [the specification](https://github.com/Oire/sharp-moji/blob/master/docs/SPEC.md).

- **Variation selectors.** 517 of 1949 records store a sequence ending in U+FE0F, while a pasted
  emoji usually does not. `Find` normalizes both sides, so two spellings that look identical
  resolve to one record.
- **Two-tone emoji.** Nineteen emoji depict two people who take independent skin tones, giving 25
  variants each. Matching tones are stored with a *single* modifier rather than a repeated pair,
  so a lookup searching for `[Light, Light]` would silently lose five variants of every one.
- **Localized categories.** Group names come from per-language data, so a Ukrainian picker gets
  Ukrainian headings rather than English ones above Ukrainian emoji names.
- **Stable keys.** `Hexcode` is stable; `Order` renumbers whenever Unicode inserts emoji. Persist
  hexcodes for favorites and recents, never positions.

## Documentation

- [Technical specification](https://github.com/Oire/sharp-moji/blob/master/docs/SPEC.md) — data model, API, measurements and design decisions
- [Changelog](https://github.com/Oire/sharp-moji/blob/master/CHANGELOG.md)

## Data source

Emoji data comes from [Emojibase](https://github.com/milesj/emojibase) (pinned to 17.0.0), which
derives from the Unicode CLDR. It is regenerated by a build-time script and committed, so
building from source needs neither a network connection nor the upstream corpus.

## License

Code is licensed under the [Apache License 2.0](https://github.com/Oire/sharp-moji/blob/master/LICENSE).

Embedded emoji data is **not** covered by that license: it is MIT (Emojibase) and Unicode-3.0
(CLDR). See [NOTICE](https://github.com/Oire/sharp-moji/blob/master/NOTICE) for the required attributions, which must accompany any
redistribution.

---

Copyright © 2026 André Polykanine, Oire Software
