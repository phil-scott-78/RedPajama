using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
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
            if (type.IsArray || IsCollectionType(type))
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

    private static bool IsCollectionType(Type type)
    {
        // Skip string - it implements IEnumerable<char> but we don't want to treat it as a collection
        if (type == typeof(string))
            return false;

        // Check if type implements IEnumerable<T> (works for all collection types)
        if (type.IsGenericType &&
            type.GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
        {
            return true;
        }

        // For array types and non-generic collections
        return type.IsArray ||
               typeof(IList).IsAssignableFrom(type) ||
               typeof(ICollection).IsAssignableFrom(type);
    }

    private ArrayTypeModel CreateArrayTypeModel(Type type, string? propertyPath, PropertyInfo? propertyInfo)
    {
        Type elementType;

        if (type.IsArray)
        {
            // For arrays, get element type directly
            elementType = type.GetElementType()!;
        }
        else if (type.IsGenericType)
        {
            // For generic collections, get the type argument
            elementType = type.GetGenericArguments()[0];
        }
        else
        {
            // For non-generic collections, use object as element type
            elementType = typeof(object);
        }

        int? minItems = null;
        int? maxItems = null;
        if (propertyInfo != null)
        {
            minItems = GetMinLength(propertyInfo);
            maxItems = GetMaxLength(propertyInfo);
        }

        // Check if this is a string array with allowed values
        if (elementType == typeof(string) && propertyInfo != null)
        {
            var allowedValues = GetAllowedValues(propertyInfo);
            var format = GetFormat(propertyInfo);

            if (allowedValues is { Length: > 0 } || !string.IsNullOrWhiteSpace(format))
            {
                // Create a StringTypeModel with the allowed values for the array elements
                var stringTypeModel = new StringTypeModel(
                    GetModelTypeName(elementType), // Use GetModelTypeName
                    allowedValues,
                    null,
                    null,
                    format
                );

                return new ArrayTypeModel(GetModelTypeName(elementType) + "Array", stringTypeModel, minItems, maxItems);
            }
        }

        // Handle other array types normally
        var arrayType = BuildTypeModel(elementType, propertyPath, null);
        // Use GetModelTypeName for the array's element type part of the name
        return new ArrayTypeModel(GetModelTypeName(elementType) + "Array", arrayType, minItems, maxItems);
    }

    private static string GetModelTypeName(Type type)
    {
        if (type == typeof(string)) return "string";
        if (type == typeof(int)) return "int";
        if (type == typeof(long)) return "long"; // Added for completeness
        if (type == typeof(decimal)) return "decimal";
        if (type == typeof(float)) return "float"; // Added for completeness
        if (type == typeof(double)) return "double"; // Added for completeness
        if (type == typeof(bool)) return "bool";
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset)) return "Date"; // Standardize to "Date"
        if (type == typeof(Guid)) return "Guid"; // Explicitly Guid
        // For enums and other complex types, use their .NET name for now,
        // as the source generator likely does something similar or uses the direct name.
        return type.Name;
    }

    private static bool TryCreatePrimitiveTypeModel(Type type, PropertyInfo? propertyInfo,
        [NotNullWhen(true)] out BaseTypeModel? model)
    {
        var modelTypeName = GetModelTypeName(type); // Get standardized name

        if (type == typeof(string) && propertyInfo != null)
        {
            var allowedValues = GetAllowedValues(propertyInfo);
            var minLength = GetMinLength(propertyInfo);
            var maxLength = GetMaxLength(propertyInfo);
            var format = GetFormat(propertyInfo);

            model = new StringTypeModel(modelTypeName, allowedValues, minLength, maxLength, format);
            return true;
        }

        // Use modelTypeName for all primitive types
        model = type switch
        {
            _ when type == typeof(string) => new StringTypeModel(modelTypeName),
            _ when type == typeof(int) || type == typeof(long) => new IntegerTypeModel(modelTypeName),
            _ when type == typeof(decimal) || type == typeof(float) || type == typeof(double) => new DecimalTypeModel(modelTypeName),
            _ when type == typeof(DateTime) || type == typeof(DateTimeOffset) => new DateTypeModel(modelTypeName),
            _ when type == typeof(bool) => new BoolTypeModel(modelTypeName),
            _ when type == typeof(Guid) => new GuidTypeModel(modelTypeName),
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
        // Use GetModelTypeName for complex types as well, for consistency, though it defaults to type.Name
        return new TypeModel(GetModelTypeName(type), properties);
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

    private static int? GetMinLength(PropertyInfo propertyInfo) =>
        propertyInfo.GetCustomAttribute<MinLengthAttribute>()?.Length;

    private static int? GetMaxLength(PropertyInfo propertyInfo) =>
        propertyInfo.GetCustomAttribute<MaxLengthAttribute>()?.Length;

    private static string? GetDescription(PropertyInfo propertyInfo) =>
        propertyInfo.GetCustomAttribute<DescriptionAttribute>()?.Description;

    private static string? GetFormat(PropertyInfo propertyInfo) =>
        propertyInfo.GetCustomAttribute<FormatAttribute>()?.Format;

    private static string[]? GetAllowedValues(PropertyInfo propertyInfo) => propertyInfo
        .GetCustomAttribute<AllowedValuesAttribute>()?.Values
        .Select(i => i?.ToString() ?? string.Empty)
        .ToArray();
}