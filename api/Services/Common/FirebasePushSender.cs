using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;

namespace CareLanka.Api.Services.Common;

public sealed class FirebasePushSender : IPushSender
{
    public const string AndroidChannelId = "dispatch_alerts";

    private readonly FirebaseMessaging _messaging;
    private readonly ILogger<FirebasePushSender> _logger;

    public FirebasePushSender(IOptions<PushOptions> options, ILogger<FirebasePushSender> logger)
    {
        _logger = logger;
        var app = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions
        {
            Credential = CredentialFactory.FromFile<ServiceAccountCredential>(options.Value.CredentialsPath).ToGoogleCredential()
        });
        _messaging = FirebaseMessaging.GetMessaging(app);
    }

    public async Task<PushOutcome> SendAsync(string deviceToken, PushMessage message, CancellationToken ct = default)
    {
        var request = new Message
        {
#pragma warning disable CS0618 // Fid needs the phone to send installation ids, which it does not yet.
            Token = deviceToken,
#pragma warning restore CS0618
            Notification = new Notification { Title = message.Title, Body = message.Body },
            Data = message.Data.ToDictionary(pair => pair.Key, pair => pair.Value),
            Android = new AndroidConfig
            {
                Priority = Priority.High,
                CollapseKey = message.CollapseKey,
                Notification = new AndroidNotification { ChannelId = AndroidChannelId, Tag = message.CollapseKey }
            }
        };

        try
        {
            await _messaging.SendAsync(request, ct);
            return PushOutcome.Delivered;
        }
        catch (FirebaseMessagingException exception)
            when (exception.MessagingErrorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument)
        {
            return PushOutcome.TokenRejected;
        }
        catch (FirebaseMessagingException exception)
        {
            _logger.LogWarning(exception, "Firebase did not accept the message ({Code}).", exception.MessagingErrorCode);
            return PushOutcome.Failed;
        }
    }
}
