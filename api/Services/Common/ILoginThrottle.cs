namespace CareLanka.Api.Services.Common;

public interface ILoginThrottle
{
    void EnsureNotLockedOut(string accountKey);

    void RecordFailure(string accountKey);

    void RecordSuccess(string accountKey);
}
