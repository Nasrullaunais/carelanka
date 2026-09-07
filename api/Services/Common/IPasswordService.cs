namespace CareLanka.Api.Services.Common;

/// <summary>Hashing and verifying passwords. One implementation, used by both identities.</summary>
public interface IPasswordService
{
    string Hash(string password);

    /// <summary>
    /// True when the password matches. <paramref name="needsRehash"/> is true when the
    /// stored hash used an older work factor and should be replaced on this successful
    /// sign-in.
    /// </summary>
    bool Verify(string storedHash, string password, out bool needsRehash);
}
