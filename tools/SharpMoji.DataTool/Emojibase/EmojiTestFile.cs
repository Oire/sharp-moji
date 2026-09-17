using System.Collections.Immutable;
using System.Globalization;

namespace Oire.SharpMoji.DataTool.Emojibase;

/// <summary>
/// One entry in Unicode's <c>emoji-test.txt</c>.
/// </summary>
/// <param name="Sequence">The emoji itself.</param>
/// <param name="Hexcode">Hyphen-separated code points, matching SharpMoji's hexcode form.</param>
/// <param name="Status">Whether the sequence is fully, minimally or un-qualified.</param>
/// <param name="Name">Unicode's own name for it.</param>
/// <param name="EmojiVersion">The emoji version that introduced it.</param>
public sealed record EmojiTestEntry(
    string Sequence,
    string Hexcode,
    EmojiQualification Status,
    string Name,
    double EmojiVersion);

/// <summary>How completely a sequence spells out its variation selectors.</summary>
public enum EmojiQualification {
    /// <summary>Every variation selector present.</summary>
    FullyQualified,

    /// <summary>The leading character is qualified but a later one is not.</summary>
    MinimallyQualified,

    /// <summary>Variation selectors omitted.</summary>
    Unqualified,

    /// <summary>A building block such as a bare skin-tone modifier, not a pickable emoji.</summary>
    Component,
}

/// <summary>
/// Parses Unicode's <c>emoji-test.txt</c>, the authoritative list of emoji sequences.
/// </summary>
/// <remarks>
/// <para>
/// This is the independent source the conformance tests check against. Validating SharpMoji's
/// data only against the Emojibase corpus it was generated from would prove nothing about whether
/// the emoji themselves are right.
/// </para>
/// <para>
/// The minimally-qualified and unqualified entries matter most: they are the same emoji written
/// without some U+FE0F, which is exactly the shape a pasted emoji arrives in.
/// </para>
/// </remarks>
public static class EmojiTestFile {
    /// <summary>The emoji version pinned for conformance. See fetch-emoji-corpus.ps1 for why.</summary>
    public const string PinnedVersion = "16.0";

    /// <summary>Whether the conformance file has been fetched.</summary>
    public static bool IsAvailable => Path is not null;

    /// <summary>The reason to skip, or <see langword="null"/> when there is none.</summary>
    public static string? SkipReason => IsAvailable
        ? null
        : $"emoji-test {PinnedVersion} not found. Run scripts/fetch-emoji-corpus.ps1.";

    private static string? Path =>
        EmojiCorpus.Root is { } root
        && File.Exists(System.IO.Path.Combine(root, $"emoji-test-{PinnedVersion}.txt"))
            ? System.IO.Path.Combine(root, $"emoji-test-{PinnedVersion}.txt")
            : null;

    /// <summary>Reads every entry in the file.</summary>
    public static ImmutableArray<EmojiTestEntry> Read() {
        var path = Path ?? throw new InvalidOperationException(SkipReason);
        var entries = ImmutableArray.CreateBuilder<EmojiTestEntry>();

        foreach (var line in File.ReadLines(path)) {
            if (Parse(line) is { } entry) {
                entries.Add(entry);
            }
        }

        return entries.ToImmutable();
    }

    /// <summary>
    /// Parses one line, or returns <see langword="null"/> for comments and blanks.
    /// </summary>
    /// <remarks>
    /// The format is
    /// <c>1F44D FE0F ; fully-qualified # 👍️ E0.6 thumbs up</c> — code points, a status, then a
    /// comment carrying the rendered emoji, an E-prefixed version and the name.
    /// </remarks>
    private static EmojiTestEntry? Parse(string line) {
        if (line.Length == 0 || line[0] == '#') {
            return null;
        }

        var semicolon = line.IndexOf(';', StringComparison.Ordinal);
        var hash = line.IndexOf('#', StringComparison.Ordinal);

        if (semicolon < 0 || hash < semicolon) {
            return null;
        }

        var codePoints = line[..semicolon].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var status = ParseStatus(line[(semicolon + 1)..hash].Trim());
        var comment = line[(hash + 1)..].Trim();

        if (status is null || codePoints.Length == 0) {
            return null;
        }

        // The comment is "<emoji> E<version> <name>". Split on the first two spaces only, because
        // names contain spaces and some emoji are themselves multi-character sequences.
        var parts = comment.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 3 || !parts[1].StartsWith('E')) {
            return null;
        }

        return new EmojiTestEntry(
            Sequence: parts[0],
            Hexcode: string.Join('-', codePoints),
            Status: status.Value,
            Name: parts[2],
            EmojiVersion: double.Parse(parts[1][1..], CultureInfo.InvariantCulture));
    }

    private static EmojiQualification? ParseStatus(string status) => status switch {
        "fully-qualified" => EmojiQualification.FullyQualified,
        "minimally-qualified" => EmojiQualification.MinimallyQualified,
        "unqualified" => EmojiQualification.Unqualified,
        "component" => EmojiQualification.Component,
        _ => null,
    };
}
