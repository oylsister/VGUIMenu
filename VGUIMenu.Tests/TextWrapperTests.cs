using VGUIMenu.Rendering;
using Xunit;

namespace VGUIMenu.Tests;

public class TextWrapperTests
{
    [Fact]
    public void ShortText_Unchanged()
        => Assert.Equal("1. Defend Here", TextWrapper.Wrap("1. Defend Here", 32));

    [Fact]
    public void LongText_WrapsOnSpaces()
    {
        var result = TextWrapper.Wrap("1. This option text is much longer than thirty-two characters", 32);

        Assert.All(result.Split('\n'), line => Assert.True(line.Length <= 32, line));
        Assert.Equal("1. This option text is much\nlonger than thirty-two\ncharacters", result);
    }

    [Fact]
    public void ContinuationLines_AreIndented()
    {
        var result = TextWrapper.Wrap("1. This option text is much longer than thirty-two characters", 32, "    ");

        var lines = result.Split('\n');
        Assert.All(lines.Skip(1), line => Assert.StartsWith("    ", line));
        Assert.All(lines, line => Assert.True(line.Length <= 32, line));
    }

    [Fact]
    public void LongWord_IsHardBroken()
    {
        var word   = new string('x', 70);
        var result = TextWrapper.Wrap(word, 32);

        Assert.Equal([32, 32, 6], result.Split('\n').Select(l => l.Length));
    }

    [Fact]
    public void ExistingNewLines_AreKept()
        => Assert.Equal("ZMarket\nMy Current Setup:", TextWrapper.Wrap("ZMarket\nMy Current Setup:", 32));

    [Fact]
    public void Empty_ReturnsEmpty()
        => Assert.Equal(string.Empty, TextWrapper.Wrap(string.Empty, 32));
}
