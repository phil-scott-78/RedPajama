using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Versioning;

namespace RedPajama;

/*
 * this class uses reflection to build the type model which gets passed to the gnbf and json generator.
 * kept those two things separate so that in the future a source generator could be used instead of reflection
 */

/// <summary>
/// This class uses reflection to build the type model which gets passed to the GNBf and JSON generator.
/// </summary>
/// <typeparam name="T">The type for which the model is being built.</typeparam>
[RequiresUnreferencedCode("TypeModelBuilder requires reflection. Use the source generator instead.")]
public class TypeModelBuilder<T>
{
    private readonly HashSet<Type> _visitedTypes = [];

    /// <summary>
    /// Builds the type model for the specified type.
    /// </summary>
    /// <returns>The built type model.</returns>
    public TypeModel Build()
    {
        var type = typeof(T);
        return (TypeModel)BuildTypeModel(type, null, null);
    }

    private BaseTypeModel BuildTypeModel(Type type, string? propertyPath, PropertyInfo? propertyInfo)
    {
        // Detect infinite loop by checking visited types
        if (!_visitedTypes.Add(type))
            throw new InvalidOperationException($"Cyclic reference detected for type: {type.Name}");

        try
        {
            if (type.IsArray)
                return CreateArrayTypeModel(type, propertyPath, propertyInfo);

            if (TryCreatePrimitiveTypeModel(type, propertyInfo, out var primitiveModel))
                return primitiveModel;

            if (type.IsEnum)
                return new EnumTypeModel(type.Name, Enum.GetNames(type));

            // Complex type with properties
            return CreateComplexTypeModel(type, propertyPath);
        }
        finally
        {
            _visitedTypes.Remove(type); // Ensure type is removed after processing
        }
    }

    private ArrayTypeModel CreateArrayTypeModel(Type type, string? propertyPath, PropertyInfo? propertyInfo)
    {
        var elementType = type.GetElementType()!;
        
        // Check if this is a string array with allowed values
        if (elementType == typeof(string) && propertyInfo != null)
        {
            var allowedValues = GetAllowedValues(propertyInfo);
            var minLength = GetMinLength(propertyInfo);
            var maxLength = GetMaxLength(propertyInfo);
            var format = GetFormat(propertyInfo);
            
            if (allowedValues is { Length: > 0 } || minLength != null || maxLength != null || !string.IsNullOrWhiteSpace(format))
            {
                // Create a StringTypeModel with the allowed values for the array elements
                var stringTypeModel = new StringTypeModel(
                    elementType.Name,
                    allowedValues,
                    minLength,
                    maxLength,
                    format
                );
                
                return new ArrayTypeModel(elementType.Name + "Array", stringTypeModel);
            }
        }
        
        // Handle other array types normally
        var arrayType = BuildTypeModel(elementType, propertyPath, null);
        return new ArrayTypeModel(elementType.Name + "Array", arrayType);
    }

    private static bool TryCreatePrimitiveTypeModel(Type type, PropertyInfo? propertyInfo, [NotNullWhen(true)] out BaseTypeModel? model)
    {
        if (type == typeof(string) && propertyInfo != null)
        {
            var allowedValues = GetAllowedValues(propertyInfo);
            var minLength = GetMinLength(propertyInfo);
            var maxLength = GetMaxLength(propertyInfo);
            var format = GetFormat(propertyInfo);

            model = new StringTypeModel(type.Name, allowedValues, minLength, maxLength, format);
            return true;
        }
        
        model = type switch
        {
            _ when type == typeof(string) => new StringTypeModel(type.Name),
            _ when type == typeof(int) || type == typeof(long) => new IntegerTypeModel(type.Name),
            _ when type == typeof(decimal) || type == typeof(float) || type == typeof(double) => new DecimalTypeModel(type.Name),
            _ when type == typeof(DateTime) || type == typeof(DateTimeOffset) => new DateTypeModel(type.Name),
            _ when type == typeof(bool) => new BoolTypeModel(type.Name),
            _ when type == typeof(Guid) => new GuidTypeModel(type.Name),
            _ => null
        };

        return model != null;
    }

    private TypeModel CreateComplexTypeModel(Type type, string? propertyPath)
    {
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

    private PropertyModel BuildPropertyModel(PropertyInfo propertyInfo, string propertyPath)
    {
        var description = GetDescription(propertyInfo);
        var type = propertyInfo.PropertyType;
        
        // Pass the propertyInfo to BuildTypeModel so attributes can be used
        var propertyType = BuildTypeModel(type, propertyPath, propertyInfo);

        return new PropertyModel(
            propertyInfo.Name,
            propertyType,
            description
        );
    }
    
    private static int? GetMinLength(PropertyInfo propertyInfo) => propertyInfo.GetCustomAttribute<MinLengthAttribute>()?.Length;

    private static int? GetMaxLength(PropertyInfo propertyInfo) => propertyInfo.GetCustomAttribute<MaxLengthAttribute>()?.Length;

    private static string? GetDescription(PropertyInfo propertyInfo) => propertyInfo.GetCustomAttribute<DescriptionAttribute>()?.Description;
    
    private static string? GetFormat(PropertyInfo propertyInfo) => propertyInfo.GetCustomAttribute<FormatAttribute>()?.Format;
    
    private static string[]? GetAllowedValues(PropertyInfo propertyInfo) => propertyInfo
        .GetCustomAttribute<AllowedValuesAttribute>()?.Values
        .Select(i => i?.ToString() ?? string.Empty)
        .ToArray();
}