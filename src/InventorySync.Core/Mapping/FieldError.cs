namespace InventorySync.Core.Mapping;

/// <summary>
/// A single field-level validation or mapping error.
/// </summary>
public sealed record FieldError(string FieldName, string Message);
