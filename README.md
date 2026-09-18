# SharpMoji

A cross-platform .NET 10 library for emoji reference data: lookup, localized search,
skin-tone variants and canonical ordering.

> **Status: in development, not yet released.** Emoji data, lookup and localization work today;
> skin-tone lookup and search are still to come. See [the specification](docs/SPEC.md) for the
> plan and its current state.

SharpMoji is the data layer for [Sourire](https://github.com/Oire) (accessible emoji input)
and a general-purpose emoji package for the .NET ecosystem.

## Capabilities

Working today:

- Lookup by sequence, hexcode or emoticon, tolerant of variation selectors
- Localized labels, tags and category names in 28 languages
- Canonical display ordering, categories and subcategories
- **No network access and no file I/O** — every language embedded in one 1.4 MB package
- Synchronous API: there is no I/O to await
- Skin-tone lookup, including the 19 emoji that take **two** tone modifiers
- Trimming- and NativeAOT-compatible, with source-generated JSON

Still to come:

- Deterministic, diacritic-insensitive search with specified ranking
- Shortcodes, and Hebrew generated from CLDR

## Example

```csharp
var emoji = EmojiCatalog.English;

emoji.Find("👍")?.Label;             // "thumbs up" — with or without the variation selector
emoji.FindByHexcode("1F600")?.Label; // "grinning face"

var ukrainian = EmojiCatalog.Load("uk");

ukrainian.FindByHexcode("1F44D")?.Label;   // "великі пальці вгору"
ukrainian.Groups[1].Name;                  // "люди"
```

## Documentation

- [Technical specification](docs/SPEC.md) — data model, API surface, phases and open questions

## Data source

Emoji data comes from [Emojibase](https://github.com/milesj/emojibase) (pinned to 17.0.0),
which derives from the Unicode CLDR.

## License

Code is licensed under the [Apache License 2.0](LICENSE).

Embedded emoji data is **not** covered by that license: it is MIT (Emojibase) and
Unicode-3.0 (CLDR). See [NOTICE](NOTICE) for the required attributions, which must
accompany any redistribution.

---

Copyright © 2026 André Polykanine, Oire Software
