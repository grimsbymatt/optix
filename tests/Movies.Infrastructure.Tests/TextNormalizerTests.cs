using Movies.Core.Text;

namespace Movies.Infrastructure.Tests;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("Pokémon", "pokemon")]
    [InlineData("  The BATMAN  ", "the batman")]
    [InlineData("Mamá o papá", "mama o papa")]
    [InlineData("100% Wolf", "100% wolf")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void Normalize_folds_case_and_strips_accents(string? input, string expected) =>
        Assert.Equal(expected, TextNormalizer.Normalize(input));
}
