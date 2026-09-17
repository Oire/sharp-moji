# SharpMoji samples

## SharpMoji.Samples.Console

A console demonstration of the library.

```bash
dotnet run --project samples/SharpMoji.Samples.Console
```

It doubles as the trimming and NativeAOT smoke test that CI runs on every push, so it must stay
free of reflection:

```bash
dotnet publish samples/SharpMoji.Samples.Console -c Release -p:PublishTrimmed=true
dotnet publish samples/SharpMoji.Samples.Console -c Release -p:PublishAot=true
```

Both must complete with no trim warnings. If a change introduces one, the fix belongs in the
library — usually a reflection-based `JsonSerializer` call that should go through the
source-generated context instead.
