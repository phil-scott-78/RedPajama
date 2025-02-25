namespace RedPajama;

/// <summary>
/// Represents the base type model.
/// </summary>
/// <param name="name">The name of the type model.</param>
public abstract class BaseTypeModel(string name)
{
    /// <summary>
    /// Gets the name of the type model.
    /// </summary>
    public string Name { get; } = name;
}

/// <summary>
/// Represents a string type model.
/// </summary>
/// <param name="name">The name of the type model.</param>
/// <param name="allowedValues">The allowed values for the string type.</param>
/// <param name="minLength">The minimum length of the string.</param>
/// <param name="maxLength">The maximum length of the string.</param>
public class StringTypeModel(string name, string[]? allowedValues = null, int? minLength = null, int? maxLength = null) : BaseTypeModel(name)
{
    /// <summary>
    /// Gets the allowed values for the string type.
    /// </summary>
    public string[] AllowedValues { get; } = allowedValues?.ToArray() ?? [];

    /// <summary>
    /// Gets the minimum length of the string.
    /// </summary>
    public int? MinLength { get; } = minLength;

    /// <summary>
    /// Gets the maximum length of the string.
    /// </summary>
    public int? MaxLength { get; } = maxLength;
}
/// <summary>
/// Represents an integer type model.
/// </summary>
/// <param name="name">The name of the type model.</param>
public class IntegerTypeModel(string name) : BaseTypeModel(name) // int, long, etc
{
}

/// <summary>
/// Represents a decimal type model.
/// </summary>
/// <param name="name">The name of the type model.</param>
public class DecimalTypeModel(string name) : BaseTypeModel(name) // decimal, float, double, etc
{
}

/// <summary>
/// Represents a bool type model.
/// </summary>
/// <param name="name">The name of the type model.</param>
public class BoolTypeModel(string name) : BaseTypeModel(name) // decimal, float, double, etc
{
}

/// <summary>
/// Represents a bool type model.
/// </summary>
/// <param name="name">The name of the type model.</param>
public class GuidTypeModel(string name) : BaseTypeModel(name) // decimal, float, double, etc
{
}

/// <summary>
/// Represents a date type model.
/// </summary>
/// <param name="name">The name of the type model.</param>
public class DateTypeModel(string name) : BaseTypeModel(name) // ISO 8601 dates
{
}

/// <summary>
/// Represents an array type model.
/// </summary>
/// <param name="name">The name of the type model.</param>
/// <param name="arrayType">The type of the array elements.</param>
public class ArrayTypeModel(string name, BaseTypeModel arrayType) : BaseTypeModel(name)
{
    /// <summary>
    /// Gets the type of the array elements.
    /// </summary>
    public BaseTypeModel ArrayType { get; } = arrayType;
}

/// <summary>
/// Represents a complex type model with properties.
/// </summary>
/// <param name="name">The name of the type model.</param>
/// <param name="properties">The properties of the type model.</param>
public class TypeModel(string name, IEnumerable<PropertyModel> properties) : BaseTypeModel(name)
{
    /// <summary>
    /// Gets the properties of the type model.
    /// </summary>
    public PropertyModel[] Properties { get; } = [.. properties];
}

/// <summary>
/// Represents an enumeration type model.
/// </summary>
/// <param name="name">The name of the type model.</param>
/// <param name="enumValues">The enumeration values.</param>
public class EnumTypeModel(string name, IEnumerable<string> enumValues) : BaseTypeModel(name)
{
    /// <summary>
    /// Gets the enumeration values.
    /// </summary>
    public string[] EnumValues { get; } = enumValues.ToArray();
}

/// <summary>
/// Represents a property model.
/// </summary>
/// <param name="name">The name of the property.</param>
/// <param name="propertyType">The type of the property.</param>
/// <param name="description">The description of the property.</param>
public class PropertyModel(string name, BaseTypeModel propertyType, string? description = null)
{
    /// <summary>
    /// Gets the name of the property.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the type of the property.
    /// </summary>
    public BaseTypeModel PropertyType { get; } = propertyType;

    /// <summary>
    /// Gets the description of the property.
    /// </summary>
    public string Description { get; } = description ?? string.Empty;
}