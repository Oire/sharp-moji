using Oire.SharpMoji;
using Oire.SharpMoji.Data;

// This sample is also the trimming and NativeAOT smoke test (docs/SPEC.md section 9.4). Keep it
// free of reflection so `dotnet publish -p:PublishAot=true` stays warning-clean.

Console.OutputEncoding = System.Text.Encoding.UTF8;

Console.WriteLine("SharpMoji");
Console.WriteLine($"  Emojibase release : {SharpMojiData.EmojibaseVersion}");
Console.WriteLine($"  Unicode emoji     : {SharpMojiData.UnicodeVersion}");
Console.WriteLine($"  Base emoji        : {SharpMojiData.EmojiCount}");
Console.WriteLine();
// Exercises the embedded packs so the AOT smoke test proves the data really decompresses and
// deserializes, not merely that the binary links.
var locales = EmbeddedPackReader.AvailableLocales();

Console.WriteLine($"  Locales embedded  : {locales.Length}");

if (EmbeddedPackReader.TryReadStrings("uk", out var uk)) {
    Console.WriteLine($"  Ukrainian 1F44D   : {uk!.Labels["1F44D"]}");
    Console.WriteLine($"  Ukrainian group 2 : {uk.Groups[1]}");
}

Console.WriteLine();
Console.WriteLine("The catalog API is not implemented yet — see docs/SPEC.md.");
