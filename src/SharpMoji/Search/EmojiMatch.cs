namespace Oire.SharpMoji;

/// <summary>
/// Why an emoji matched a query, in descending order of how strongly it did.
/// </summary>
/// <remarks>
/// The order of these values <em>is</em> the ranking. A user typing <c>smile</c> expects the emoji
/// actually named "smile" first, not something that merely lists it as a tag — so an exact label
/// beats an exact tag, and both beat a substring.
/// </remarks>
public enum MatchKind {
    /// <summary>The query is exactly a shortcode for this emoji.</summary>
    /// <remarks>
    /// Ranked first because a shortcode is unambiguous: someone typing <c>:+1:</c> or <c>tada</c>
    /// is naming one specific emoji rather than describing one.
    /// </remarks>
    Shortcode = 0,

    /// <summary>The query is exactly a text emoticon for this emoji, such as <c>:D</c>.</summary>
    /// <remarks>
    /// Ranked with shortcodes, and for the same reason: someone typing <c>:D</c> is naming one
    /// specific emoji, not describing a category. Only 49 emoji have an emoticon, so this fires
    /// rarely — but when it does, it is the most certain signal available.
    /// </remarks>
    Emoticon = 1,

    /// <summary>The query is exactly this emoji's name.</summary>
    Label = 2,

    /// <summary>The query is exactly one of this emoji's tags.</summary>
    /// <remarks>
    /// Ranked above a name the query merely starts, which is the opposite of what this
    /// specification first said. Tags are curated keywords: being tagged <c>love</c> is an
    /// editorial statement that the emoji <em>means</em> love, while "love hotel" only happens to
    /// begin with those letters. With prefixes ranked first, typing <c>love</c> buried ❤️ below
    /// 💌 💘 🏩 💝 — which is not what anyone means.
    /// </remarks>
    Tag = 3,

    /// <summary>This emoji's name starts with the query.</summary>
    /// <remarks>What makes type-ahead feel right: <c>thum</c> should already be offering 👍.</remarks>
    LabelPrefix = 4,

    /// <summary>The query appears somewhere in this emoji's name.</summary>
    LabelSubstring = 5,

    /// <summary>The query appears somewhere in one of this emoji's tags.</summary>
    TagSubstring = 6,
}

/// <summary>
/// One search result: the emoji, and why it matched.
/// </summary>
/// <remarks>
/// <see cref="Kind"/> is exposed so a user interface can group or explain results — "named
/// smile" above "tagged smile" — rather than presenting one flat list whose order looks arbitrary.
/// </remarks>
public sealed record EmojiMatch {
    /// <summary>The emoji that matched.</summary>
    public required Emoji Emoji { get; init; }

    /// <summary>How it matched.</summary>
    public required MatchKind Kind { get; init; }

    /// <summary>The label, tag or shortcode the query matched against.</summary>
    /// <remarks>Useful for highlighting the matched text in a result list.</remarks>
    public required string MatchedText { get; init; }

    /// <summary>Returns the emoji's sequence.</summary>
    public override string ToString() => Emoji.Sequence;
}
