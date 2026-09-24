namespace InventorySync.Core.Mapping;

/// <summary>
/// Outcome of mapping one ERP wire record to a domain entity: either a value or a list of field errors.
/// </summary>
public sealed class ErpMappingResult<T>
{
    private ErpMappingResult()
    {
    }

    /// <summary>
    /// Gets or sets the is success.
    /// </summary>
    public bool IsSuccess { get; private init; }

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public T? Value { get; private init; }

    /// <summary>
    /// Gets or sets the errors.
    /// </summary>
    public IReadOnlyList<FieldError> Errors { get; private init; } = Array.Empty<FieldError>();

    /// <summary>
    /// Creates a successful mapping result with the given value.
    /// </summary>
    public static ErpMappingResult<T> Success(T value)
    {
        return new ErpMappingResult<T> { IsSuccess = true, Value = value };
    }

    /// <summary>
    /// Creates a failed mapping result with the given field errors.
    /// </summary>
    public static ErpMappingResult<T> Failure(IReadOnlyList<FieldError> errors)
    {
        return new ErpMappingResult<T> { IsSuccess = false, Errors = errors };
    }
}
