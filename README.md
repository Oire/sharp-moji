# SharpMoji

A cross-platform .NET 10 library for emoji reference data: lookup, localized search,
skin-tone variants and canonical ordering.

> **Status: pre-implementation.** The repository currently contains the technical
> specification only. No code has been written yet.

SharpMoji is the data layer for [Sourire](https://github.com/Oire) (accessible emoji input)
and a general-purpose emoji package for the .NET ecosystem.

## Planned capabilities

- Emoji lookup by sequence, hexcode, shortcode or emoticon, tolerant of variation selectors
- Localized labels, tags and category names in 29 locales
- Correct skin-tone handling, including the 19 emoji that take **two** tone modifiers
- Deterministic, diacritic-insensitive search with specified ranking
- Offline by default — bundled English data, with opt-in locale pack downloads
- Trimming- and NativeAOT-compatible, with source-generated JSON

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
