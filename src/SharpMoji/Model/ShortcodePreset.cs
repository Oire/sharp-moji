namespace Oire.SharpMoji;

/// <summary>
/// A named vocabulary of emoji shortcodes.
/// </summary>
/// <remarks>
/// <para>
/// Shortcodes are the <c>:thumbsup:</c> convention. There is no single standard — GitHub, Slack
/// and the various emoji libraries each grew their own, and they disagree often enough that an
/// application usually wants to pick the one its users already know.
/// </para>
/// <para>
/// SharpMoji ships the English presets only. The per-locale CLDR sets are a mechanical
/// transliteration and an underscored copy of the label, and searching labels and tags finds those
/// already. See <c>docs/SPEC.md</c> section 3.4.
/// </para>
/// </remarks>
public enum ShortcodePreset {
    /// <summary>
    /// The CLDR set: one shortcode per emoji, derived from its English name.
    /// </summary>
    /// <remarks>The default, and the only preset that covers every emoji.</remarks>
    Cldr = 0,

    /// <summary>
    /// CLDR's native-script overrides, which for English is only a handful of accented forms.
    /// </summary>
    CldrNative = 1,

    /// <summary>Emojibase's own set, which offers several alternatives for many emoji.</summary>
    Emojibase = 2,

    /// <summary>Emojibase's previous set, kept for applications migrating from older data.</summary>
    EmojibaseLegacy = 3,

    /// <summary>
    /// GitHub's set — <c>:+1:</c>, <c>:-1:</c>, <c>:tada:</c> and the rest.
    /// </summary>
    /// <remarks>
    /// Covers the Unicode emoji GitHub names. GitHub's own custom images, <c>:shipit:</c> among
    /// them, are not Unicode emoji and are not here.
    /// </remarks>
    GitHub = 4,

    /// <summary>The iamcal set, as used by Slack and much of the JavaScript ecosystem.</summary>
    Iamcal = 5,

    /// <summary>The JoyPixels set.</summary>
    JoyPixels = 6,
}
