namespace CareLanka.Api.Services.Common;

public interface IPasswordService
{
    string Hash(string password);

    bool Verify(string storedHash, string password, out bool needsRehash);
}
