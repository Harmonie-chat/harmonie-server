using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Notifications;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Notifications.RegisterWebPushDevice;

public sealed class RegisterWebPushDeviceHandler : IAuthenticatedHandler<RegisterWebPushDeviceRequest, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly INotificationDeviceRepository _notificationDeviceRepository;

    public RegisterWebPushDeviceHandler(
        IUserRepository userRepository,
        INotificationDeviceRepository notificationDeviceRepository)
    {
        _userRepository = userRepository;
        _notificationDeviceRepository = notificationDeviceRepository;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        RegisterWebPushDeviceRequest request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(currentUserId, cancellationToken);
        if (user is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.User.NotFound,
                "User was not found");
        }

        DateTime? expiresAtUtc = null;
        if (request.ExpirationTime.HasValue)
        {
            expiresAtUtc = DateTimeOffset
                .FromUnixTimeMilliseconds(request.ExpirationTime.Value)
                .UtcDateTime;
        }

        await _notificationDeviceRepository.UpsertWebPushAsync(
            new WebPushNotificationDeviceRegistration(
                currentUserId,
                request.Endpoint,
                request.Keys.P256dh,
                request.Keys.Auth,
                expiresAtUtc),
            cancellationToken);

        return ApplicationResponse<bool>.Ok(true);
    }
}
