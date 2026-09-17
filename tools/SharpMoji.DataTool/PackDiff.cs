using System.Collections.Immutable;
using Oire.SharpMoji.Data;

namespace Oire.SharpMoji.DataTool;

/// <summary>
/// Compares a newly generated pack against the one already on disk.
/// </summary>
/// <remarks>
/// The packs are compressed binary, so a regeneration shows up in <c>git diff</c> as "29 files
/// changed" and nothing more. Without this, nobody can tell a Unicode release that added 20 emoji
/// from one that quietly relabeled a thousand. The CHANGELOG entry is written from this output —
/// see <c>docs/SPEC.md</c> section 7.3.
/// </remarks>
internal sealed record PackDiff {
    public required ImmutableArray<string> Added { get; init; }
    public required ImmutableArray<string> Removed { get; init; }
    public required ImmutableArray<string> Relabeled { get; init; }
    public required ImmutableArray<string> StructureChanged { get; init; }
    public required string? PreviousEmojibaseVersion { get; init; }

    public bool IsEmpty =>
        Added.IsEmpty && Removed.IsEmpty && Relabeled.IsEmpty && StructureChanged.IsEmpty;

    /// <summary>
    /// Builds a diff, or returns <see langword="null"/> when there is nothing to compare against.
    /// </summary>
    public static PackDiff? Compare(string outputDirectory, StructurePack newStructure, StringPack newEnglish) {
        var structurePath = Path.Combine(outputDirectory, "structure.br");
        var englishPath = Path.Combine(outputDirectory, "strings.en.br");

        if (!File.Exists(structurePath) || !File.Exists(englishPath)) {
            return null;
        }

        StructurePack oldStructure;
        StringPack oldEnglish;

        using (var structureStream = File.OpenRead(structurePath)) {
            oldStructure = EmojiPackSerializer.ReadStructure(structureStream);
        }

        using (var englishStream = File.OpenRead(englishPath)) {
            oldEnglish = EmojiPackSerializer.ReadStrings(englishStream);
        }

        var oldByHexcode = oldStructure.Emoji.ToDictionary(e => e.Hexcode, StringComparer.Ordinal);
        var newByHexcode = newStructure.Emoji.ToDictionary(e => e.Hexcode, StringComparer.Ordinal);

        var added = ImmutableArray.CreateBuilder<string>();
        var removed = ImmutableArray.CreateBuilder<string>();
        var relabeled = ImmutableArray.CreateBuilder<string>();
        var structureChanged = ImmutableArray.CreateBuilder<string>();

        foreach (var (hexcode, emoji) in newByHexcode) {
            if (!oldByHexcode.TryGetValue(hexcode, out var before)) {
                added.Add($"{emoji.Sequence}  {hexcode}  {Label(newEnglish, hexcode)}");
                continue;
            }

            // Order is excluded deliberately: it renumbers whenever anything is inserted, so
            // including it would drown every real change in noise.
            if (before.Sequence != emoji.Sequence
                || before.Group != emoji.Group
                || before.Subgroup != emoji.Subgroup
                || (before.Skins?.Length ?? 0) != (emoji.Skins?.Length ?? 0)) {
                structureChanged.Add($"{emoji.Sequence}  {hexcode}  {Label(newEnglish, hexcode)}");
            }

            var oldLabel = Label(oldEnglish, hexcode);
            var newLabel = Label(newEnglish, hexcode);

            if (oldLabel != newLabel) {
                relabeled.Add($"{emoji.Sequence}  {hexcode}  \"{oldLabel}\" -> \"{newLabel}\"");
            }
        }

        foreach (var (hexcode, emoji) in oldByHexcode) {
            if (!newByHexcode.ContainsKey(hexcode)) {
                removed.Add($"{emoji.Sequence}  {hexcode}  {Label(oldEnglish, hexcode)}");
            }
        }

        return new PackDiff {
            Added = [.. added.Order(StringComparer.Ordinal)],
            Removed = [.. removed.Order(StringComparer.Ordinal)],
            Relabeled = [.. relabeled.Order(StringComparer.Ordinal)],
            StructureChanged = [.. structureChanged.Order(StringComparer.Ordinal)],
            PreviousEmojibaseVersion = oldStructure.EmojibaseVersion,
        };
    }

    /// <summary>Writes the diff in a form that can be pasted into the CHANGELOG.</summary>
    public void Report(TextWriter writer) {
        writer.WriteLine();
        writer.WriteLine($"Changes against the packs already on disk (Emojibase {PreviousEmojibaseVersion}):");

        if (IsEmpty) {
            writer.WriteLine("  No emoji added, removed, relabeled or restructured.");
            return;
        }

        Section(writer, "Added", Added);
        Section(writer, "Removed", Removed);
        Section(writer, "Structure changed", StructureChanged);
        Section(writer, "Relabeled", Relabeled);

        if (!StructureChanged.IsEmpty) {
            writer.WriteLine();
            writer.WriteLine("  Structure changes can affect skin-tone slots and group membership.");
            writer.WriteLine("  Review the affected tests rather than regenerating and moving on.");
        }
    }

    private static void Section(TextWriter writer, string title, ImmutableArray<string> entries) {
        if (entries.IsEmpty) {
            return;
        }

        writer.WriteLine();
        writer.WriteLine($"  {title} ({entries.Length}):");

        // Long lists are truncated: a Unicode release relabeling hundreds of emoji should say so
        // rather than scroll the useful parts of the report off the screen.
        foreach (var entry in entries.Take(25)) {
            writer.WriteLine($"    {entry}");
        }

        if (entries.Length > 25) {
            writer.WriteLine($"    ... and {entries.Length - 25} more");
        }
    }

    private static string Label(StringPack strings, string hexcode) =>
        strings.Labels.TryGetValue(hexcode, out var label) ? label : "(no label)";
}
