using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Notifications;

namespace Harmonie.Application.Features.Notifications.GetWebPushPublicKey;

public sealed class GetWebPushPublicKeyHandler : IHandler<Unit, GetWebPushPublicKeyResponse>
{
    private readonly IWebPushPublicKeyProvider _publicKeyProvider;

    public GetWebPushPublicKeyHandler(IWebPushPublicKeyProvider publicKeyProvider)
    {
        _publicKeyProvider = publicKeyProvider;
    }

    public Task<ApplicationResponse<GetWebPushPublicKeyResponse>> HandleAsync(
        Unit request,
        CancellationToken cancellationToken = default)
    {
        var publicKey = _publicKeyProvider.GetPublicKey();
        if (string.IsNullOrWhiteSpace(publicKey))
        {
            return Task.FromResult(ApplicationResponse<GetWebPushPublicKeyResponse>.Fail(
                ApplicationErrorCodes.Notification.WebPushNotConfigured,
                "Web Push public key is not configured"));
        }

        return Task.FromResult(ApplicationResponse<GetWebPushPublicKeyResponse>.Ok(
            new GetWebPushPublicKeyResponse(publicKey)));
    }
}
