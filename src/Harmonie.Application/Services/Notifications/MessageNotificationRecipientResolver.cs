using Harmonie.Application.Interfaces.Notifications;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Services.Notifications;

public sealed class MessageNotificationRecipientResolver
{
    public IReadOnlySet<UserId> Resolve(MessageNotificationContext context)
    {
        return context.CandidateRecipientUserIds
            .Where(userId => userId != context.AuthorUserId)
            .ToHashSet();
    }
}
