using Harmonie.Application.Interfaces.Notifications;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Services.Notifications;

public sealed class MessageNotificationPolicy : IMessageNotificationPolicy
{
    public IReadOnlySet<UserId> SelectRecipients(MessageNotificationContext context)
    {
        var candidateRecipientUserIds = context.CandidateRecipientUserIds;
        IEnumerable<UserId> selectedRecipientUserIds = context.Target switch
        {
            MessageNotificationTarget.Conversation => candidateRecipientUserIds,
            MessageNotificationTarget.Channel => context.MentionedUserIds.Where(candidateRecipientUserIds.Contains),
            _ => Enumerable.Empty<UserId>()
        };

        return selectedRecipientUserIds
            .Where(userId => userId != context.AuthorUserId)
            .ToHashSet();
    }
}
