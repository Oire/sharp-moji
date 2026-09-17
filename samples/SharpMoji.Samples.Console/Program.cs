using Oire.SharpMoji;

// This sample is also the trimming and NativeAOT smoke test (docs/SPEC.md section 9.4). It uses
// only the public API and exercises real work — decompressing packs, building indexes, resolving
// sequences — so publishing it proves the library still functions after trimming, not merely that
// it links. Keep it free of reflection.

Console.OutputEncoding = System.Text.Encoding.UTF8;

Console.WriteLine($"SharpMoji — Emojibase {SharpMojiData.EmojibaseVersion}, Unicode emoji {SharpMojiData.UnicodeVersion}");
Console.WriteLine();

var english = EmojiCatalog.English;

Console.WriteLine($"Languages embedded : {EmojiLocale.All.Length}");
Console.WriteLine($"Emoji              : {english.All.Length} pickable, {english.AllIncludingComponents.Length} including components");
Console.WriteLine($"Categories         : {english.Groups.Length}");
Console.WriteLine();

// The variation-selector problem, which bites hardest in practice: the stored sequence carries
// U+FE0F and a pasted one usually does not. Both must resolve to the same emoji.
Console.WriteLine("Lookup:");
Console.WriteLine($"  bare       {Describe(english.Find("\U0001F44D"))}");
Console.WriteLine($"  qualified  {Describe(english.Find("\U0001F44D️"))}");
Console.WriteLine($"  toned      {Describe(english.Find("\U0001F44D\U0001F3FD"))}");
Console.WriteLine($"  by hexcode {Describe(english.FindByHexcode("1F600"))}");
Console.WriteLine($"  emoticon   {Describe(english.FindByEmoticon(":D"))}");
Console.WriteLine();

Console.WriteLine("One emoji, several languages:");

foreach (var code in (string[])["en", "fr", "de", "uk", "ru", "ja"]) {
    var catalog = EmojiCatalog.Load(code);
    var emoji = catalog.FindByHexcode("1F44D")!;

    Console.WriteLine($"  {catalog.Locale.NativeName,-14} {emoji.Sequence} {emoji.Label}");
}

Console.WriteLine();
Console.WriteLine("Categories, named in Ukrainian:");

var ukrainian = EmojiCatalog.Load("uk");

foreach (var group in ukrainian.Groups.Where(g => !g.IsComponent).Take(5)) {
    Console.WriteLine($"  {group.Name,-26} {ukrainian.GetByGroup(group).Length,4} emoji");
}

Console.WriteLine();
Console.WriteLine("Skin-tone lookup and search arrive in later phases — see docs/SPEC.md.");

static string Describe(Emoji? emoji) =>
    emoji is null ? "(not found)" : $"{emoji.Sequence}  {emoji.Hexcode,-12} {emoji.Label}";
