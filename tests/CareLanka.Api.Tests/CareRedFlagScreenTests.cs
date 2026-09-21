using CareLanka.Api.Agents.Patient;
using Xunit;

namespace CareLanka.Api.Tests;

public sealed class CareRedFlagScreenTests
{
    [Theory]
    [InlineData("I have chest pain since this morning")]
    [InlineData("I CAN'T BREATHE properly")]
    [InlineData("There is severe bleeding from the wound")]
    [InlineData("I feel suicidal today")]
    [InlineData("She had a seizure an hour ago")]
    public void A_red_flag_keyword_anywhere_in_the_text_matches(string text)
        => Assert.True(CareRedFlagScreen.Matches(text));

    [Theory]
    [InlineData("My headache is worse today and it hurts more when I lie flat.")]
    [InlineData("I feel a bit tired but otherwise fine.")]
    [InlineData("")]
    public void Ordinary_text_does_not_match(string text)
        => Assert.False(CareRedFlagScreen.Matches(text));

    [Fact]
    public void The_match_is_case_insensitive()
        => Assert.True(CareRedFlagScreen.Matches("Chest Pain started an hour ago"));
}
