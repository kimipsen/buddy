using buddy.Features.Privacy;

namespace buddy.Features.Users;

public static class GetAccountDeletionPreviewHandler
{
    public static Task<AccountDeletionPreview> Handle(GetAccountDeletionPreview query, UserErasure erasure, CancellationToken cancellationToken) =>
        erasure.PreviewAsync(query.UserId, cancellationToken);
}
