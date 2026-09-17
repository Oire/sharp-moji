namespace Oire.SharpMoji;

/// <summary>
/// Identifies the emoji dataset embedded in this build of SharpMoji.
/// </summary>
/// <remarks>
/// <para>
/// SharpMoji carries its data rather than downloading it, so the dataset is fixed for a given
/// package version. An application can surface or log these values to report exactly which
/// emoji it knows about.
/// </para>
/// <para>
/// A Unicode release reaches consumers as a new package version. See
/// <c>docs/SPEC.md</c> section 7.3 for the upgrade mechanism.
/// </para>
/// </remarks>
public static class SharpMojiData {
    /// <summary>
    /// The <see href="https://github.com/milesj/emojibase">Emojibase</see> release the embedded
    /// data was generated from.
    /// </summary>
    public const string EmojibaseVersion = "17.0.0";

    /// <summary>
    /// The Unicode emoji version covered by the embedded data.
    /// </summary>
    public const string UnicodeVersion = "17.0";

    /// <summary>
    /// The number of base emoji records in the dataset, excluding skin-tone variants.
    /// </summary>
    /// <remarks>
    /// Identical for every locale: emoji structure does not vary by language, only labels and
    /// tags do. See <c>docs/SPEC.md</c> section 3.7.
    /// </remarks>
    public const int EmojiCount = 1949;
}
