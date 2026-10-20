using Boilerplate.Application.Common.Persistence;
using Boilerplate.Application.Common.Security;
using Boilerplate.Domain.Auditing;

namespace Boilerplate.Application.Common.Auditing;

/// <summary>
/// Records security-relevant changes in the current tenant's audit log (ADR-0028). <see cref="Record"/> only adds a row
/// to the current unit of work: the use case's own <c>SaveChangesAsync</c> commits it together with the change.
/// </summary>
public sealed class AuditLog(IAppDbContext db, ICurrentUser currentUser, TimeProvider timeProvider)
{
    /// <param name="action">One of <see cref="AuditActions"/>.</param>
    /// <param name="targetId">The affected entity.</param>
    /// <param name="description">
    /// What happened, for people. Name roles, permissions and user ids; never e-mail addresses, tokens or secrets.
    /// </param>
    public void Record(string action, Guid targetId, string description) =>
        db.AuditLogEntries.Add(AuditLogEntry.Record(
            timeProvider.GetUtcNow(),
            currentUser.UserId,
            action,
            targetId,
            description));
}
