namespace InventorySync.Core.Exceptions;

/// <summary>
/// Raised when an operation conflicts with the current state of an entity (including optimistic-concurrency conflicts).
/// </summary>
public class ConflictException : Exception
{
    /// <summary>
    /// Initializes a new instance of the ConflictException class.
    /// </summary>
    public ConflictException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the ConflictException class.
    /// </summary>
    public ConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
