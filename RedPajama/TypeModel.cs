namespace RedPajama;

public abstract class BaseTypeModel(string name)
{
    public string Name { get; } = name;
}

public class StringTypeModel(string name, string[]? allowedValues = null, int? minLength = null, int? maxLength = null) : BaseTypeModel(name)
{
    public string[] AllowedValues { get; } = allowedValues?.ToArray() ?? [];
    public int? MinLength { get; } = minLength;
    public int? MaxLength { get; } = maxLength;
}
public class IntegerTypeModel(string name) : BaseTypeModel(name); // int, long, etc
public class DecimalTypeModel(string name) : BaseTypeModel(name); // decimal, float, double, etc
public class DateTypeModel(string name) : BaseTypeModel(name); // ISO 8601 dates

public class ArrayTypeModel(string name, BaseTypeModel arrayType) : BaseTypeModel(name)
{
    public BaseTypeModel ArrayType { get; } = arrayType;
}

public class TypeModel(string name, IEnumerable<PropertyModel> properties) : BaseTypeModel(name)
{
    public PropertyModel[] Properties { get; } = properties.ToArray();
}

public class EnumTypeModel(string name, IEnumerable<string> enumValues) : BaseTypeModel(name)
{
    public string[] EnumValues { get; } = enumValues.ToArray();
}

public class PropertyModel(string name, BaseTypeModel propertyType, string? description = null)
{
    public string Name { get; } = name;
    public BaseTypeModel PropertyType { get; } = propertyType;
    public string Description { get; } = description ?? string.Empty;
}