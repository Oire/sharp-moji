using System.Collections.Immutable;
using System.Reflection;

namespace Oire.SharpMoji.Data;

/// <summary>
/// Finds and decompresses the emoji packs embedded in the SharpMoji assembly.
/// </summary>
/// <remarks>
/// <para>
/// Every language ships in this one assembly. An earlier design split the 27 non-English
/// languages into an optional satellite package to keep the core small, discovered at run time by
/// assembly name. It does not survive trimming: nothing statically references the satellite, so
/// both <c>PublishTrimmed</c> and NativeAOT delete it outright and the application is left with
/// English and no error to explain why. Since trimming is a stated ship gate
/// (<c>docs/SPEC.md</c> section 9.4) and the whole bundle is only about 1.4 MB, one assembly is
/// both simpler and the only version that actually works.
/// </para>
/// <para>
/// There is no file or network I/O here by design (<c>docs/SPEC.md</c> section 7.1): a pack is a
/// Brotli blob inside an assembly the runtime has already loaded.
/// </para>
/// </remarks>
internal static class EmbeddedPackReader {
    private const string StructureSuffix = ".structure.br";
    private const string StringsMarker = ".strings.";

    private static Assembly PackAssembly => typeof(EmbeddedPackReader).Assembly;

    /// <summary>Loads the shared structure pack.</summary>
    public static StructurePack ReadStructure() {
        // Matched on the trailing file name rather than a fixed prefix, because the SDK derives the
        // prefix from RootNamespace. Pinning one with LogicalName is a trap: on a wildcard it does
        // not batch per item, collapsing every file onto a single name and dropping all but one.
        var name = PackAssembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(StructureSuffix, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                "The embedded structure pack is missing. Run scripts/update-emoji-data.ps1.");

        using var stream = PackAssembly.GetManifestResourceStream(name)!;

        return EmojiPackSerializer.ReadStructure(stream);
    }

    /// <summary>Loads one language's string pack.</summary>
    /// <returns><see langword="false"/> when that locale is not one of the embedded languages.</returns>
    public static bool TryReadStrings(string locale, out StringPack? pack) {
        var suffix = $"{StringsMarker}{locale}.br";
        var name = PackAssembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.Ordinal));

        if (name is null) {
            pack = null;
            return false;
        }

        using var stream = PackAssembly.GetManifestResourceStream(name)!;

        pack = EmojiPackSerializer.ReadStrings(stream);
        return true;
    }

    /// <summary>Lists every embedded locale, sorted.</summary>
    public static ImmutableArray<string> AvailableLocales() =>
    [
        .. PackAssembly.GetManifestResourceNames()
            .Select(ExtractLocale)
            .OfType<string>()
            .Order(StringComparer.Ordinal)
    ];

    private static string? ExtractLocale(string resourceName) {
        var start = resourceName.LastIndexOf(StringsMarker, StringComparison.Ordinal);

        return start >= 0 && resourceName.EndsWith(".br", StringComparison.Ordinal)
            ? resourceName[(start + StringsMarker.Length)..^".br".Length]
            : null;
    }
}
