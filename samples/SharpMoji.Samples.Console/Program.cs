using Oire.SharpMoji;

// This sample is also the trimming and NativeAOT smoke test (docs/SPEC.md section 9.4). Keep it
// free of reflection so `dotnet publish -p:PublishAot=true` stays warning-clean.

Console.OutputEncoding = System.Text.Encoding.UTF8;

Console.WriteLine("SharpMoji");
Console.WriteLine($"  Emojibase release : {SharpMojiData.EmojibaseVersion}");
Console.WriteLine($"  Unicode emoji     : {SharpMojiData.UnicodeVersion}");
Console.WriteLine($"  Base emoji        : {SharpMojiData.EmojiCount}");
Console.WriteLine();
Console.WriteLine("The catalog API is not implemented yet — see docs/SPEC.md.");
