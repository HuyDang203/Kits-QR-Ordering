using Annap.CoffeeQrOrdering.Domain.Common;

namespace Annap.CoffeeQrOrdering.Domain.Entities;

public sealed class ChatMessage : AuditableEntity
{
    public Guid ChatSessionId { get; set; }
    public ChatSession ChatSession { get; set; } = null!;

    public ChatRole Role { get; set; } = ChatRole.User;
    public string Content { get; set; } = null!;
}

