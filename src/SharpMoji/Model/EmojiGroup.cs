namespace Oire.SharpMoji;

/// <summary>
/// A top-level emoji category, such as "smileys &amp; emotion".
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not an enum. Unicode adds groups between releases, and a fixed enum would either
/// have to change shape — a breaking change — or fail to represent a new category at all.
/// </para>
/// <para>
/// <see cref="Key"/> is stable across languages and releases and is what to persist;
/// <see cref="Name"/> is display text in the catalog's language and changes with CLDR revisions.
/// </para>
/// </remarks>
public sealed record EmojiGroup {
    /// <summary>The language-independent identifier, for example <c>"people-body"</c>.</summary>
    public required string Key { get; init; }

    /// <summary>The name in the catalog's language, for example <c>"люди й тіло"</c>.</summary>
    public required string Name { get; init; }

    /// <summary>The category's position in canonical order.</summary>
    public required int Index { get; init; }

    /// <summary>
    /// Whether this group holds components rather than pickable emoji.
    /// </summary>
    /// <remarks>
    /// The "component" group contains bare skin-tone and hair modifiers, which are building blocks
    /// rather than emoji anyone would choose from a picker. <see cref="IEmojiCatalog.All"/>
    /// excludes them.
    /// </remarks>
    public bool IsComponent => Key == "component" || Key == "components";

    /// <summary>Returns <see cref="Name"/>.</summary>
    public override string ToString() => Name;
}

/// <summary>
/// A finer emoji category nested under a group, such as "face-smiling".
/// </summary>
public sealed record EmojiSubgroup {
    /// <summary>The language-independent identifier, for example <c>"face-smiling"</c>.</summary>
    public required string Key { get; init; }

    /// <summary>The name in the catalog's language.</summary>
    public required string Name { get; init; }

    /// <summary>The subcategory's position in canonical order.</summary>
    public required int Index { get; init; }

    /// <summary>Returns <see cref="Name"/>.</summary>
    public override string ToString() => Name;
}
