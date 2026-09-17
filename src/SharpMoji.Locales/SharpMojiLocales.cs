using System.Reflection;

namespace Oire.SharpMoji.Locales;

/// <summary>
/// Marks the assembly carrying SharpMoji's additional locale data.
/// </summary>
/// <remarks>
/// The core <c>Oire.SharpMoji</c> package bundles English only. When this package is installed
/// alongside it, the locale tables embedded in this assembly are discovered at run time and
/// <c>EmojiLocale.All</c> widens to every supported language. Applications do not normally
/// reference this type directly; it exists so the assembly can be located and so that trimming
/// keeps it.
/// </remarks>
public static class SharpMojiLocales {
    /// <summary>
    /// Gets the assembly containing the embedded locale tables.
    /// </summary>
    public static Assembly Assembly => typeof(SharpMojiLocales).Assembly;
}
