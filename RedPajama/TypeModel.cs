using System.Linq.Expressions;

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
/// Represents a complex type model with properties.
/// </summary>
public class TypeModel : BaseTypeModel
{
    /// <summary>
    /// Gets the properties of the type model.
    /// </summary>
    internal PropertyModel[] Properties { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeModel"/> class.
    /// </summary>
    /// <param name="name">The name of the type model.</param>
    /// <param name="properties">The properties of the type model.</param>
    public TypeModel(string name, IEnumerable<PropertyModel> properties) : base(name)
    {
        Properties = properties.ToArray();
    }

    /// <summary>
    /// Adds a description to the specified property and returns a new TypeModel instance.
    /// </summary>
    /// <typeparam name="T">The type for which the model is being built.</typeparam>
    /// <typeparam name="TProperty">The type of the property.</typeparam>
    /// <param name="propertySelector">An expression that selects the property.</param>
    /// <param name="description">The description to add.</param>
    /// <returns>A new TypeModel with the updated description.</returns>
    public TypeModel WithDescription<T, TProperty>(Expression<Func<T, TProperty>> propertySelector, string description)
    {
        var propertyPath = GetPropertyPath(propertySelector);
        return UpdateProperty(propertyPath, (property, _) => new PropertyModel(property.Name, property.PropertyType, description));
    }

    /// <summary>
    /// Adds allowed values to the specified property and returns a new TypeModel instance.
    /// </summary>
    /// <typeparam name="T">The type for which the model is being built.</typeparam>
    /// <typeparam name="TProperty">The type of the property.</typeparam>
    /// <param name="propertySelector">An expression that selects the property.</param>
    /// <param name="allowedValues">The allowed values to add.</param>
    /// <returns>A new TypeModel with the updated allowed values.</returns>
    public TypeModel WithAllowedValues<T, TProperty>(Expression<Func<T, TProperty>> propertySelector, TProperty[] allowedValues)
    {
        var propertyPath = GetPropertyPath(propertySelector);
        return UpdateProperty(propertyPath, (property, _) => 
        {
            if (property.PropertyType is StringTypeModel stringTypeModel)
            {
                var updatedStringType = stringTypeModel.WithAllowedValues(allowedValues.Cast<string>().ToArray());
                return new PropertyModel(property.Name, updatedStringType, property.Description);
            }
            return property;
        });
    }
    
    private TypeModel UpdateProperty(string propertyPath, Func<PropertyModel, string[], PropertyModel> updateFunc)
    {
        var parts = propertyPath.Split('.');
        var propertyName = parts[0];
        
        // Create new property array
        var newProperties = new PropertyModel[Properties.Length];
        var propertyFound = false;
        
        for (var i = 0; i < Properties.Length; i++)
        {
            var property = Properties[i];
            
            if (property.Name == propertyName)
            {
                propertyFound = true;
                
                if (parts.Length == 1)
                {
                    // Direct property of this type, update
                    newProperties[i] = updateFunc(property, parts);
                }
                else if (property.PropertyType is TypeModel nestedTypeModel)
                {
                    // Nested property path, recurse and update nested type model
                    var nestedPath = string.Join('.', parts.Skip(1));
                    var updatedNestedModel = nestedTypeModel.UpdateProperty(nestedPath, updateFunc);
                    newProperties[i] = new PropertyModel(property.Name, updatedNestedModel, property.Description);
                }
                else
                {
                    // Can't update a path on a non-TypeModel
                    newProperties[i] = property;
                }
            }
            else
            {
                // Not the property we're looking for, copy as is
                newProperties[i] = property;
            }
        }
        
        // If we didn't find the property, just return the original TypeModel
        return propertyFound 
            ? new TypeModel(Name, newProperties) 
            : this;
    }

    private static string GetPropertyPath<T, TProperty>(Expression<Func<T, TProperty>> propertySelector)
    {
        if (propertySelector.Body is not MemberExpression memberExpression)
            throw new ArgumentException("Invalid property expression", nameof(propertySelector));

        var parts = new List<string>();
        var expression = memberExpression;
        while (expression != null)
        {
            parts.Add(expression.Member.Name);
            expression = expression.Expression as MemberExpression;
        }

        parts.Reverse();
        return string.Join(".", parts);
    }
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

/// <summary>
/// Represents a string type model.
/// </summary>
/// <param name="name">The name of the type model.</param>
/// <param name="allowedValues">The allowed values for the string type.</param>
/// <param name="minLength">The minimum length of the string.</param>
/// <param name="maxLength">The maximum length of the string.</param>
/// <param name="format">The format string of the string.</param>
public class StringTypeModel(string name, string[]? allowedValues = null, int? minLength = null, int? maxLength = null, string? format = null) : BaseTypeModel(name)
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

    /// <summary>
    /// Gets the format of the string.
    /// </summary>
    public string? Format { get; } = format;

    /// <summary>
    /// Creates a new instance of StringTypeModel with the specified allowed values.
    /// </summary>
    /// <param name="allowedValues">The allowed values for the string type.</param>
    /// <returns>A new instance of StringTypeModel with the updated allowed values.</returns>
    internal StringTypeModel WithAllowedValues(string[] allowedValues) =>
        new(Name, allowedValues, MinLength, MaxLength, Format);
}

/// <summary>
/// Represents an integer type model.
/// </summary>
/// <param name="name">The name of the type model.</param>
public class IntegerTypeModel(string name) : BaseTypeModel(name); // int, long, etc

/// <summary>
/// Represents a decimal type model.
/// </summary>
/// <param name="name">The name of the type model.</param>
public class DecimalTypeModel(string name) : BaseTypeModel(name); // decimal, float, double, etc

/// <summary>
/// Represents a date type model.
/// </summary>
/// <param name="name">The name of the type model.</param>
public class DateTypeModel(string name) : BaseTypeModel(name); // ISO 8601 dates

/// <summary>
/// Represents a bool type model.
/// </summary>
/// <param name="name">The name of the type model.</param>
public class BoolTypeModel(string name) : BaseTypeModel(name);

/// <summary>
/// Represents a Guid type model.
/// </summary>
/// <param name="name">The name of the type model.</param>
public class GuidTypeModel(string name) : BaseTypeModel(name);

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
