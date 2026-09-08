using Annap.CoffeeQrOrdering.Domain.Common;

namespace Annap.CoffeeQrOrdering.Domain.Entities;

public sealed class ChatSession : AuditableEntity
{
    public string TableCode { get; set; } = null!;
    public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAtUtc { get; set; }

    public List<ChatMessage> Messages { get; set; } = [];
}

