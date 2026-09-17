using FluentAssertions;
using Oire.SharpMoji;
using Xunit;

namespace Oire.SharpMoji.Tests;

/// <summary>
/// Pins the identity of the embedded dataset.
/// </summary>
/// <remarks>
/// These are deliberately brittle. Regenerating the emoji data (<c>scripts/update-emoji-data.ps1</c>)
/// must update <see cref="SharpMojiData"/> and these expectations together, so a partial or
/// accidental refresh fails here rather than shipping. See <c>docs/SPEC.md</c> section 7.3.
/// </remarks>
public class SharpMojiDataTests {
    [Fact]
    public void EmbeddedDataset_IsTheExpectedEmojibaseRelease() {
        SharpMojiData.EmojibaseVersion.Should().Be("17.0.0");
        SharpMojiData.UnicodeVersion.Should().Be("17.0");
    }

    [Fact]
    public void EmojiCount_MatchesTheEmojibaseDataset() {
        // Verified against emojibase-data 17.0.0: every locale has exactly this many base records.
        SharpMojiData.EmojiCount.Should().Be(1949);
    }
}
