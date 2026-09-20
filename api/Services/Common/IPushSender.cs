namespace CareLanka.Api.Services.Common;

public sealed record PushMessage(
    string Title,
    string Body,
    string CollapseKey,
    IReadOnlyDictionary<string, string> Data);

public enum PushOutcome
{
    Delivered,
    TokenRejected,
    Failed
}

public interface IPushSender
{
    Task<PushOutcome> SendAsync(string deviceToken, PushMessage message, CancellationToken ct = default);
}
