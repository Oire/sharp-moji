namespace Oire.SharpMoji;

/// <summary>
/// A Fitzpatrick skin-tone modifier.
/// </summary>
/// <remarks>
/// The numeric values match Emojibase's <c>tone</c> field exactly, so no mapping table is needed
/// anywhere. Types I and II share a single emoji modifier, which is why there are five tones and
/// not six.
/// </remarks>
public enum SkinTone {
    /// <summary>No skin tone: the default, non-human-colored form.</summary>
    None = 0,

    /// <summary>Light skin tone, Fitzpatrick types I and II (U+1F3FB).</summary>
    Light = 1,

    /// <summary>Medium-light skin tone, Fitzpatrick type III (U+1F3FC).</summary>
    MediumLight = 2,

    /// <summary>Medium skin tone, Fitzpatrick type IV (U+1F3FD).</summary>
    Medium = 3,

    /// <summary>Medium-dark skin tone, Fitzpatrick type V (U+1F3FE).</summary>
    MediumDark = 4,

    /// <summary>Dark skin tone, Fitzpatrick type VI (U+1F3FF).</summary>
    Dark = 5,
}

/// <summary>
/// Whether a sequence is meant to render as a colorful emoji or as monochrome text.
/// </summary>
public enum EmojiPresentation {
    /// <summary>Renders as text by default, unless followed by U+FE0F.</summary>
    Text = 0,

    /// <summary>Renders as an emoji by default.</summary>
    Emoji = 1,
}

/// <summary>
/// The gender a sequence explicitly encodes, where it encodes one.
/// </summary>
/// <remarks>
/// Only 108 of 1949 emoji carry this. Most are gender-neutral or gender-inclusive and report
/// <see langword="null"/> instead, which is not the same as either value here.
/// </remarks>
public enum EmojiGender {
    /// <summary>The sequence explicitly depicts a woman.</summary>
    Female = 0,

    /// <summary>The sequence explicitly depicts a man.</summary>
    Male = 1,
}
