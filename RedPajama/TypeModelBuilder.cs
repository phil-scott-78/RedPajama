using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
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
    private readonly HashSet<Type> _visitedTypes = [];

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
                return CreateArrayTypeModel(type, propertyPath);

            if (TryCreatePrimitiveTypeModel(type, out var primitiveModel))
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

    private ArrayTypeModel CreateArrayTypeModel(Type type, string? propertyPath)
    {
        var elementType = type.GetElementType()!;
        var arrayType = BuildTypeModel(elementType, propertyPath);
        return new ArrayTypeModel(elementType.Name + "Array", arrayType);
    }

    private static bool TryCreatePrimitiveTypeModel(Type type, [NotNullWhen(true)] out BaseTypeModel? model)
    {
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
        
        BaseTypeModel propertyType;
        
        if (type == typeof(string))
        {
            var allowedValues = GetAllowedValues(propertyInfo);
            var minLength = GetMinLength(propertyInfo);
            var maxLength = GetMaxLength(propertyInfo);

            propertyType = new StringTypeModel(type.Name, allowedValues, minLength, maxLength);
        }
        else if (TryCreatePrimitiveTypeModel(type, out var primitiveModel))
        {
            propertyType = primitiveModel;
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

    private static int? GetMinLength(PropertyInfo propertyInfo) => propertyInfo.GetCustomAttribute<MinLengthAttribute>()?.Length;

    private static int? GetMaxLength(PropertyInfo propertyInfo) => propertyInfo.GetCustomAttribute<MaxLengthAttribute>()?.Length;

    private static string? GetDescription(PropertyInfo propertyInfo) => propertyInfo.GetCustomAttribute<DescriptionAttribute>()?.Description;
    
    private static string[]? GetAllowedValues(PropertyInfo propertyInfo) => propertyInfo
        .GetCustomAttribute<AllowedValuesAttribute>()?.Values
        .Select(i => i?.ToString() ?? string.Empty)
        .ToArray();
}