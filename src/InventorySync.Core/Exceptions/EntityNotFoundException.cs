namespace InventorySync.Core.Exceptions;

public class EntityNotFoundException : Exception
{
    public EntityNotFoundException(string message)
        : base(message)
    {
    }
}
