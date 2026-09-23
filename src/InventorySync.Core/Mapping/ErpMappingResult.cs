namespace InventorySync.Core.Mapping;

public sealed class ErpMappingResult<T>
{
    private ErpMappingResult()
    {
    }

    public bool IsSuccess { get; private init; }

    public T? Value { get; private init; }

    public IReadOnlyList<FieldError> Errors { get; private init; } = Array.Empty<FieldError>();

    public static ErpMappingResult<T> Success(T value)
    {
        return new ErpMappingResult<T> { IsSuccess = true, Value = value };
    }

    public static ErpMappingResult<T> Failure(IReadOnlyList<FieldError> errors)
    {
        return new ErpMappingResult<T> { IsSuccess = false, Errors = errors };
    }
}
