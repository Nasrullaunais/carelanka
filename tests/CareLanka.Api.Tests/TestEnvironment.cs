namespace CareLanka.Api.Tests;

internal sealed class TestEnvironment : IDisposable
{
    private readonly string? _connectionString;
    private readonly string? _signingKey;

    private TestEnvironment()
    {
        _connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__CareLanka");
        _signingKey = Environment.GetEnvironmentVariable("Jwt__SigningKey");

        Environment.SetEnvironmentVariable(
            "ConnectionStrings__CareLanka", "Host=localhost;Database=carelanka_tests;Username=unused");
        Environment.SetEnvironmentVariable(
            "Jwt__SigningKey", "test-signing-key-that-is-at-least-32-characters");
    }

    public static TestEnvironment Use() => new();

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__CareLanka", _connectionString);
        Environment.SetEnvironmentVariable("Jwt__SigningKey", _signingKey);
    }
}
