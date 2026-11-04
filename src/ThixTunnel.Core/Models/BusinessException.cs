namespace ThixTunnel.Core.Models;

/// <summary>Thrown by the business layer when input is invalid or an action is not allowed.</summary>
public sealed class BusinessException(string message, string? field = null) : Exception(message)
{
    /// <summary>The field the message belongs to (property name), if any.</summary>
    public string? Field { get; } = field;
}
