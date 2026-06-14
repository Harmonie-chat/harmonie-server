using Harmonie.Application.Interfaces.Notifications;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Services.Notifications;

public interface IMessageNotificationPolicy
{
    IReadOnlySet<UserId> SelectRecipients(MessageNotificationContext context);
}
