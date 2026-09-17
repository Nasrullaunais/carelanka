using CareLanka.Api.Services.Patient;
using Xunit;

namespace CareLanka.Api.Tests;

public sealed class MaskedIdentityTests
{
    [Theory]
    [InlineData("Lochana Dahanayake", "L•••••a D••••••••e")]
    [InlineData("Jo Perera", "J• P••••a")]
    [InlineData("A", "A")]
    [InlineData("  Nimal   Silva  ", "N•••l S•••a")]
    public void A_name_keeps_only_its_first_and_last_letters(string name, string expected)
        => Assert.Equal(expected, MaskedIdentity.Name(name));

    [Theory]
    [InlineData("0771234567", "•••••••567")]
    [InlineData("12", "••")]
    public void A_phone_keeps_only_its_last_three_digits(string phone, string expected)
        => Assert.Equal(expected, MaskedIdentity.Phone(phone));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_record_with_no_phone_masks_to_nothing_rather_than_to_bullets(string? phone)
        => Assert.Null(MaskedIdentity.Phone(phone));

    [Fact]
    public void Masking_never_leaves_a_whole_word_readable()
    {
        const string name = "Dahanayake";

        var masked = MaskedIdentity.Name(name);

        Assert.DoesNotContain(name, masked);
        Assert.Equal(name.Length, masked.Length);
    }
}
