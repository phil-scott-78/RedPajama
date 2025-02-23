using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;

namespace RedPajama;

/*
 * this class uses reflection to build the type model which gets passed to the gnbf and json generator.
 * kept those two things separate so that in the future a source generator could be used instead of reflection
 */

/// <summary>
/// This class uses reflection to build the type model which gets passed to the GNBf and JSON generator.
/// </summary>
/// <typeparam name="T">The type for which the model is being built.</typeparam>
public class TypeModelBuilder<T>
{
    private readonly Dictionary<string, string> _descriptions = new();
    private readonly Dictionary<string, string[]> _allowedValues = new();
    private readonly HashSet<Type> _visitedTypes = [];

    /// <summary>
    /// Adds a description to the specified property.
    /// </summary>
    /// <typeparam name="TProperty">The type of the property.</typeparam>
    /// <param name="propertySelector">An expression that selects the property.</param>
    /// <param name="description">The description to add.</param>
    /// <returns>The current instance of <see cref="TypeModelBuilder{T}"/>.</returns>
    public TypeModelBuilder<T> WithDescription<TProperty>(Expression<Func<T, TProperty>> propertySelector,
        string description)
    {
        var propertyPath = GetPropertyPath(propertySelector);
        _descriptions[propertyPath] = description;
        return this;
    }

    /// <summary>
    /// Adds allowed values to the specified property.
    /// </summary>
    /// <typeparam name="TProperty">The type of the property.</typeparam>
    /// <param name="propertySelector">An expression that selects the property.</param>
    /// <param name="allowedValues">The allowed values to add.</param>
    /// <returns>The current instance of <see cref="TypeModelBuilder{T}"/>.</returns>
    public TypeModelBuilder<T> WithAllowedValues<TProperty>(Expression<Func<T, TProperty>> propertySelector,
        string[] allowedValues)
    {
        var propertyPath = GetPropertyPath(propertySelector);
        _allowedValues[propertyPath] = allowedValues;
        return this;
    }

    /// <summary>
    /// Builds the type model for the specified type.
    /// </summary>
    /// <returns>The built type model.</returns>
    public TypeModel Build()
    {
        var type = typeof(T);
        return (TypeModel)BuildTypeModel(type, null);
    }

    private BaseTypeModel BuildTypeModel(Type type, string? propertyPath)
    {
        // Detect infinite loop by checking visited types
        if (!_visitedTypes.Add(type))
            throw new InvalidOperationException($"Cyclic reference detected for type: {type.Name}");

        try
        {
            if (type.IsArray)
            {
                var elementType = type.GetElementType()!;
                var arrayType = BuildTypeModel(elementType, propertyPath);
                return new ArrayTypeModel(elementType.Name + "Array", arrayType);
            }

            if (type == typeof(string))
            {
                return new StringTypeModel(type.Name);
            }

            if (type == typeof(int) || type == typeof(long))
                return new IntegerTypeModel(type.Name);

            if (type == typeof(decimal) || type == typeof(float) || type == typeof(double))
                return new DecimalTypeModel(type.Name);

            if (type == typeof(DateTime) || type==typeof(DateTimeOffset))
                return new DateTypeModel(type.Name);

            if (type.IsEnum)
                return new EnumTypeModel(type.Name, Enum.GetNames(type));

            var properties = type.GetProperties()
                .Where(p => p is { CanRead: true, GetMethod.IsPublic: true })
                .Select(p =>
                {
                    var currentPath = propertyPath == null ? p.Name : $"{propertyPath}.{p.Name}";
                    return BuildPropertyModel(p, currentPath);
                })
                .ToArray();

            return new TypeModel(type.Name, properties);
        }
        finally
        {
            _visitedTypes.Remove(type); // Ensure type is removed after processing
        }
    }

    private PropertyModel BuildPropertyModel(PropertyInfo propertyInfo, string propertyPath)
    {
        var description = GetDescription(propertyInfo, propertyPath);
        BaseTypeModel propertyType;

        var type = propertyInfo.PropertyType;

        if (type == typeof(string))
        {
            var allowedValues = GetAllowedValues(propertyInfo, propertyPath);
            var minLength = GetMinLength(propertyInfo);
            var maxLength = GetMaxLength(propertyInfo);

            propertyType = new StringTypeModel(type.Name, allowedValues, minLength, maxLength);
        }
        else if (type == typeof(int) || type == typeof(long))
        {
            propertyType = new IntegerTypeModel(type.Name);
        }
        else if (type == typeof(decimal) || type == typeof(float) ||
                 type == typeof(double))
        {
            propertyType = new DecimalTypeModel(type.Name);
        }
        else if (type == typeof(DateTime))
        {
            propertyType = new DateTypeModel(type.Name);
        }
        else
        {
            propertyType = BuildTypeModel(type, propertyPath);
        }

        return new PropertyModel(
            propertyInfo.Name,
            propertyType,
            description
        );
    }

    private int? GetMinLength(PropertyInfo propertyInfo)
    {
        var minLengthAttribute = propertyInfo.GetCustomAttribute<MinLengthAttribute>();
        return minLengthAttribute?.Length;
    }

    private int? GetMaxLength(PropertyInfo propertyInfo)
    {
        var minLengthAttribute = propertyInfo.GetCustomAttribute<MaxLengthAttribute>();
        return minLengthAttribute?.Length;
    }

    private string? GetDescription(PropertyInfo propertyInfo, string propertyPath)
    {
        if (_descriptions.TryGetValue(propertyPath, out var configuredDescription))
            return configuredDescription;

        var descriptionAttribute = propertyInfo.GetCustomAttribute<DescriptionAttribute>();
        return descriptionAttribute?.Description;
    }

    private string[]? GetAllowedValues(PropertyInfo propertyInfo, string propertyPath)
    {
        if (_allowedValues.TryGetValue(propertyPath, out var configuredValues))
            return configuredValues;

        var allowedValuesAttribute = propertyInfo.GetCustomAttribute<AllowedValuesAttribute>();
        return allowedValuesAttribute?.Values.Select(i => i?.ToString() ?? string.Empty).ToArray();
    }

    private static string GetPropertyPath<TProperty>(Expression<Func<T, TProperty>> propertySelector)
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