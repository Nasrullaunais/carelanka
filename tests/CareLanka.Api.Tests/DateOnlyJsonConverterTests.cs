using System.Text.Json;
using System.Text.Json.Serialization;
using CareLanka.Api.Common.Serialization;
using Xunit;

namespace CareLanka.Api.Tests;

/// <summary>
/// Dart has no date-only type, so the generated Flutter client sends every
/// <c>format: date</c> field as a full timestamp. These are the shapes that
/// have to keep working, and the one that has to keep failing.
/// </summary>
public sealed class DateOnlyJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new DateOnlyJsonConverter() }
    };

    private sealed class Body
    {
        public DateOnly? DateOfBirth { get; set; }
    }

    [Theory]
    [InlineData("1995-04-02")]
    [InlineData("1995-04-02T00:00:00.000")]
    [InlineData("1995-04-02T00:00:00.000Z")]
    [InlineData("1995-04-02T13:45:12.123456+05:30")]
    public void Every_shape_a_date_arrives_in_reads_as_the_same_day(string wire)
    {
        var body = JsonSerializer.Deserialize<Body>($"{{\"date_of_birth\":\"{wire}\"}}", Options);

        Assert.Equal(new DateOnly(1995, 4, 2), body!.DateOfBirth);
    }

    [Fact]
    public void A_null_date_stays_null_rather_than_becoming_the_epoch()
    {
        var body = JsonSerializer.Deserialize<Body>("{\"date_of_birth\":null}", Options);

        Assert.Null(body!.DateOfBirth);
    }

    [Fact]
    public void The_wire_keeps_the_date_only_form_the_spec_publishes()
    {
        var json = JsonSerializer.Serialize(
            new Body { DateOfBirth = new DateOnly(1995, 4, 2) }, Options);

        Assert.Equal("{\"date_of_birth\":\"1995-04-02\"}", json);
    }

    [Theory]
    [InlineData("not-a-date")]
    // Read by a general date parser this is 4 February, not 2 April. A wrong
    // date of birth that saves is worse than one that is refused.
    [InlineData("02/04/1995")]
    [InlineData("1995-04-02 00:00:00")]
    [InlineData("1995-4-2")]
    [InlineData("")]
    public void Something_that_is_not_a_date_is_still_refused(string wire)
    {
        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<Body>($"{{\"date_of_birth\":\"{wire}\"}}", Options));
    }
}
