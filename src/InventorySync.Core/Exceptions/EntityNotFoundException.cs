namespace InventorySync.Core.Exceptions;

/// <summary>
/// Raised when a requested entity does not exist.
/// </summary>
public class EntityNotFoundException : Exception
{
    /// <summary>
    /// Initializes a new instance of the EntityNotFoundException class.
    /// </summary>
    public EntityNotFoundException(string message)
        : base(message)
    {
    }
}
