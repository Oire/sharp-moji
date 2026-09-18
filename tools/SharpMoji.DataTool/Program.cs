using System.Globalization;
using Oire.SharpMoji;
using Oire.SharpMoji.Data;
using Oire.SharpMoji.DataTool;
using Oire.SharpMoji.DataTool.Emojibase;

// Build-time only. Regenerates the emoji packs SharpMoji embeds, from the corpus fetched by
// scripts/fetch-emoji-corpus.ps1. Nothing here ships: the library makes no network calls and this
// project is not packable.
//
// Usage: dotnet run --project tools/SharpMoji.DataTool -- [resources-directory]

Console.OutputEncoding = System.Text.Encoding.UTF8;

var output = args.Length > 0 ? args[0] : Path.Combine("src", "SharpMoji", "Resources");

if (!EmojiCorpus.IsAvailable) {
    Console.Error.WriteLine(EmojiCorpus.SkipReason);
    Console.Error.WriteLine("Run scripts/fetch-emoji-corpus.ps1 first.");
    return 1;
}

Console.WriteLine($"Emojibase {SharpMojiData.EmojibaseVersion}, corpus at {EmojiCorpus.Root}");
Console.WriteLine();

// The structure pack is generated from English and used by every language, so prove that the
// assumption still holds before writing anything. A future release that localized something
// structural must fail here, not ship 27 mislabeled languages.
Console.Write("Verifying structure is locale-independent... ");
var mismatches = PackGenerator.VerifyStructureIsShared();

if (mismatches.Length > 0) {
    Console.WriteLine("FAILED");
    Console.Error.WriteLine();
    Console.Error.WriteLine("These locales do not share English's structure:");

    foreach (var mismatch in mismatches) {
        Console.Error.WriteLine($"  {mismatch}");
    }

    Console.Error.WriteLine();
    Console.Error.WriteLine("docs/SPEC.md section 3.7 no longer holds. The packaging design depends on it.");
    return 1;
}

Console.WriteLine($"ok ({EmojiCorpus.Locales.Length} locales agree)");

Directory.CreateDirectory(output);

var structure = PackGenerator.BuildStructure(SharpMojiData.EmojibaseVersion, SharpMojiData.UnicodeVersion);
var english = PackGenerator.BuildStrings(PackGenerator.CoreLocale);

// Compare against what is already on disk before overwriting it. The packs are compressed binary,
// so git can only report that the files changed - this is the only account of what actually did.
var diff = PackDiff.Compare(output, structure, english);

var structureBytes = EmojiPackSerializer.Write(structure);

File.WriteAllBytes(Path.Combine(output, "structure.br"), structureBytes);

var shortcodes = PackGenerator.BuildShortcodes();
var shortcodeBytes = EmojiPackSerializer.Write(shortcodes);

File.WriteAllBytes(Path.Combine(output, "shortcodes.br"), shortcodeBytes);

Console.WriteLine();
Console.WriteLine($"  structure.br          {Kb(structureBytes.Length),10}   ({structure.Emoji.Length} emoji, shared by every language)");
Console.WriteLine($"  shortcodes.br         {Kb(shortcodeBytes.Length),10}   ({shortcodes.Presets.Count} presets, shared by every language)");

var total = (long)structureBytes.Length + shortcodeBytes.Length;

foreach (var locale in EmojiCorpus.Locales) {
    var strings = PackGenerator.BuildStrings(locale);
    var bytes = EmojiPackSerializer.Write(strings);

    File.WriteAllBytes(Path.Combine(output, $"strings.{locale}.br"), bytes);

    total += bytes.Length;

    Console.WriteLine($"  strings.{locale + ".br",-13} {Kb(bytes.Length),10}   ({strings.Labels.Count} labels)");
}

Console.WriteLine();
Console.WriteLine($"  Complete bundle       {Kb(total),10}   ({EmojiCorpus.Locales.Length} languages, one assembly)");

// The ship gate from docs/SPEC.md section 12. Exceeding it means the structure/string split has
// regressed, which is worth knowing before it reaches a package.
const long BudgetBytes = 1_600_000;

if (total > BudgetBytes) {
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Bundle is {Kb(total)}, over the {Kb(BudgetBytes)} budget.");
    return 1;
}

if (diff is not null) {
    diff.Report(Console.Out);
} else {
    Console.WriteLine();
    Console.WriteLine("No readable previous packs, so there is nothing to diff against.");
    Console.WriteLine("(Expected on a first run, or when the pack format itself has changed.)");
}

Console.WriteLine();
Console.WriteLine("Done. Next: update SharpMojiData if the version changed, rerun the tests, and");
Console.WriteLine("write the CHANGELOG entry from the diff above.");
return 0;

static string Kb(long bytes) => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024.0:N1} KB");
