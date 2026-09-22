namespace CareLanka.Api.Services.Common;

public sealed class LoggingPushSender(ILogger<LoggingPushSender> logger) : IPushSender
{
    public Task<PushOutcome> SendAsync(string deviceToken, PushMessage message, CancellationToken ct = default)
    {
        logger.LogWarning("Push:CredentialsPath is not set, so nothing was sent to a phone: {Title}", message.Title);
        return Task.FromResult(PushOutcome.Failed);
    }
}
