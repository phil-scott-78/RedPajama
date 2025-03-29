using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace RedPajama.SourceGen
{
    /// <summary>
    /// Helper class to process a type and its dependencies
    /// </summary>
    internal class TypeProcessor
    {
        private readonly HashSet<string> _processedTypes = new HashSet<string>();
        private readonly Dictionary<string, string> _typeHelperMethods = new Dictionary<string, string>();

        public TypeProcessingResult ProcessType(ITypeSymbol typeSymbol, TypeModelContextData contextData)
        {
            var helperMethods = new List<string>();
            var typeFullName = typeSymbol.ToDisplayString();
            var typeName = typeSymbol.Name;

            // Process the type if it hasn't been processed yet
            if (_processedTypes.Add(typeFullName))
            {
                // For complex types, process their properties recursively
                if (typeSymbol.TypeKind == TypeKind.Class || typeSymbol.TypeKind == TypeKind.Struct)
                {
                    foreach (var member in typeSymbol.GetMembers())
                    {
                        if (member is IPropertySymbol { GetMethod: { DeclaredAccessibility: Accessibility.Public } } property)
                        {
                            ProcessPropertyType(property.Type, contextData);
                        }
                    }

                    // Generate helper method for this type
                    var helperMethod = GenerateTypeHelperMethod(typeSymbol, typeName);
                    if (!string.IsNullOrEmpty(helperMethod))
                    {
                        _typeHelperMethods[typeFullName] = helperMethod;
                    }
                }
            }

            // Get all helper methods
            helperMethods.AddRange(_typeHelperMethods.Values);

            // Generate the main builder code
            var builderCode = $"            return Build{GetLongName(typeSymbol)}Model();";

            return new TypeProcessingResult(helperMethods, builderCode);
        }

        private static string GetLongName(ITypeSymbol typeSymbol)
        {
            return typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace(".", "").Replace(":", "");
        }

        private void ProcessPropertyType(ITypeSymbol typeSymbol, TypeModelContextData contextData)
        {
            var typeFullName = typeSymbol.ToDisplayString();

            // Skip primitives and already processed types
            if (IsPrimitiveType(typeSymbol) || !_processedTypes.Add(typeFullName))
            {
                return;
            }

            // Handle arrays and collections
            if (typeSymbol is IArrayTypeSymbol arrayType)
            {
                ProcessPropertyType(arrayType.ElementType, contextData);
                return;
            }

            // Handle generic collections
            if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                foreach (var typeArg in namedType.TypeArguments)
                {
                    ProcessPropertyType(typeArg, contextData);
                }

                // If it's a collection, we're done
                if (IsCollectionType(namedType))
                {
                    return;
                }
            }

            // For complex types, process their properties recursively
            if (typeSymbol.TypeKind is TypeKind.Class or TypeKind.Struct)
            {
                // Generate helper method for this type
                var typeName = typeSymbol.Name;
                var helperMethod = GenerateTypeHelperMethod(typeSymbol, typeName);

                if (!string.IsNullOrEmpty(helperMethod))
                {
                    _typeHelperMethods[typeFullName] = helperMethod;
                }

                // Process properties
                foreach (var member in typeSymbol.GetMembers())
                {
                    if (member is IPropertySymbol property &&
                        property.GetMethod != null &&
                        property.GetMethod.DeclaredAccessibility == Accessibility.Public)
                    {
                        ProcessPropertyType(property.Type, contextData);
                    }
                }
            }
            else if (typeSymbol.TypeKind == TypeKind.Enum)
            {
                // Generate enum helper
                var enumHelperMethod = GenerateEnumHelperMethod(typeSymbol);
                _typeHelperMethods[typeFullName] = enumHelperMethod;
            }
        }

        private string GenerateTypeHelperMethod(ITypeSymbol typeSymbol, string typeName)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"        private static TypeModel Build{typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace(".", "").Replace(":", "")}Model()");
            sb.AppendLine("        {");
            sb.AppendLine($"            var properties = new List<PropertyModel>();");

            // For a class or struct, add all properties
            if (typeSymbol.TypeKind is TypeKind.Class or TypeKind.Struct)
            {
                foreach (var member in typeSymbol.GetMembers())
                {
                    if (member is not IPropertySymbol { GetMethod: { DeclaredAccessibility: Accessibility.Public } } property) continue;
                    sb.AppendLine($"            // Property: {property.Name}");
                    sb.AppendLine($"            properties.Add(new PropertyModel(");
                    sb.AppendLine($"                \"{property.Name}\",");
                    sb.AppendLine($"                {GetPropertyTypeCode(property)},");

                    var description = GetDescriptionFromAttributes(property);
                    sb.AppendLine(!string.IsNullOrEmpty(description)
                        ? $"                \"{EscapeString(description)}\"));"
                        : $"                null));");
                }
            }

            sb.AppendLine($"            return new TypeModel(\"{typeName}\", properties);");
            sb.AppendLine("        }");

            return sb.ToString();
        }

        private string GenerateEnumHelperMethod(ITypeSymbol enumType)
        {
            var sb = new StringBuilder();
            var typeName = enumType.Name;

            sb.AppendLine($"        private static EnumTypeModel Build{GetLongName(enumType)}EnumModel()");
            sb.AppendLine("        {");

            // Get enum values
            var enumValues = enumType.GetMembers()
                .Where(m => m.Kind == SymbolKind.Field && m.Name != "value__")
                .Select(m => $"\"{m.Name}\"");

            sb.AppendLine(
                $"            return new EnumTypeModel(\"{typeName}\", new[] {{{string.Join(", ", enumValues)}}});");
            sb.AppendLine("        }");

            return sb.ToString();
        }

        private string GetPropertyTypeCode(IPropertySymbol property)
        {
            return GetTypeCode(property.Type, property);
        }

        private string GetTypeCode(ITypeSymbol typeSymbol, IPropertySymbol property)
        {
            // Handle primitive types
            switch (typeSymbol.SpecialType)
            {
                case SpecialType.System_String:
                    return GetStringTypeModelCode(property);
                case SpecialType.System_Int32:
                    return $"new IntegerTypeModel(\"int\")";
                case SpecialType.System_Int64:
                    return $"new IntegerTypeModel(\"long\")";
                case SpecialType.System_Decimal:
                    return $"new DecimalTypeModel(\"decimal\")";
                case SpecialType.System_Double:
                    return $"new DecimalTypeModel(\"double\")";
                case SpecialType.System_Single:
                    return $"new DecimalTypeModel(\"float\")";
                case SpecialType.System_Boolean:
                    return $"new BoolTypeModel(\"bool\")";
            }

            // Handle other common types
            var typeName = typeSymbol.Name;

            if (typeName is "DateTime" or "DateTimeOffset")
            {
                return $"new DateTypeModel(\"{typeName}\")";
            }

            if (typeName == "Guid")
            {
                return $"new GuidTypeModel(\"Guid\")";
            }

            // Handle arrays
            if (typeSymbol is IArrayTypeSymbol arrayType)
            {
                var elementTypeName = arrayType.ElementType.Name;
                return $"new ArrayTypeModel(\"{elementTypeName}Array\", {GetTypeCode(arrayType.ElementType, null)})";
            }

            // Handle collections (List<T>, IEnumerable<T>, etc.)
            if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                if (IsCollectionType(namedType) && namedType.TypeArguments.Length == 1)
                {
                    var elementType = namedType.TypeArguments[0];
                    var elementTypeName = elementType.Name;
                    return $"new ArrayTypeModel(\"{elementTypeName}Array\", {GetTypeCode(elementType, null)})";
                }
            }

            // Handle enums
            if (typeSymbol.TypeKind == TypeKind.Enum)
            {
                return $"Build{GetLongName(typeSymbol)}EnumModel()";
            }

            // For complex types, call their helper method
            return $"Build{GetLongName(typeSymbol)}Model()";
        }

        private string GetStringTypeModelCode(IPropertySymbol property = null)
        {
            if (property == null)
            {
                return $"new StringTypeModel(\"string\")";
            }
            
            int? minLength = null;
            int? maxLength = null;
            string[] allowedValues = null;

            // Look for MinLength attribute
            foreach (var attr in property.GetAttributes())
            {
                if (attr.AttributeClass == null) continue;
                switch (attr.AttributeClass.Name)
                {
                    case "MinLengthAttribute" when attr.ConstructorArguments.Length > 0:
                        minLength = attr.ConstructorArguments[0].Value as int?;
                        break;
                    case "MaxLengthAttribute" when attr.ConstructorArguments.Length > 0:
                        maxLength = attr.ConstructorArguments[0].Value as int?;
                        break;
                    case "AllowedValuesAttribute":
                    {
                        // Try to get allowed values from the attribute
                        if (attr.ConstructorArguments.Length > 0 &&
                            attr.ConstructorArguments[0].Kind == TypedConstantKind.Array)
                        {
                            var values = attr.ConstructorArguments[0].Values;
                            allowedValues = values
                                .Select(v => v.Value?.ToString())
                                .Where(v => v != null)
                                .ToArray();
                        }

                        break;
                    }
                }
            }

            if (minLength.HasValue || maxLength.HasValue || allowedValues != null)
            {
                var sb = new StringBuilder();
                sb.Append("new StringTypeModel(\"string\"");

                if (allowedValues != null)
                {
                    sb.Append($", new[] {{{string.Join(", ", allowedValues.Select(v => $"\"{EscapeString(v)}\""))}}}");
                }
                else
                {
                    sb.Append(", null");
                }

                sb.Append($", {(minLength.HasValue ? minLength.ToString() : "null")}");
                sb.Append($", {(maxLength.HasValue ? maxLength.ToString() : "null")})");

                return sb.ToString();
            }

            return "new StringTypeModel(\"string\")";
        }

        private static string GetDescriptionFromAttributes(IPropertySymbol property)
        {
            return property.GetAttributes()
                .Where(attr => attr.AttributeClass?.Name == "DescriptionAttribute")
                .Where(attr => attr.ConstructorArguments.Length > 0)
                .Select(attr => attr.ConstructorArguments[0].Value?.ToString()).FirstOrDefault();
        }

        private static string EscapeString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return input
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        private static bool IsPrimitiveType(ITypeSymbol typeSymbol)
        {
            return typeSymbol.SpecialType == SpecialType.System_String ||
                   typeSymbol.SpecialType == SpecialType.System_Int32 ||
                   typeSymbol.SpecialType == SpecialType.System_Int64 ||
                   typeSymbol.SpecialType == SpecialType.System_Decimal ||
                   typeSymbol.SpecialType == SpecialType.System_Double ||
                   typeSymbol.SpecialType == SpecialType.System_Single ||
                   typeSymbol.SpecialType == SpecialType.System_Boolean ||
                   typeSymbol.Name == "DateTime" ||
                   typeSymbol.Name == "DateTimeOffset" ||
                   typeSymbol.Name == "Guid";
        }

        private static bool IsCollectionType(INamedTypeSymbol typeSymbol)
        {
            var collectionTypes = new[]
            {
                "IEnumerable", "ICollection", "IList", "List", "Collection",
                "HashSet", "SortedSet", "ISet", "IReadOnlyCollection", "IReadOnlyList"
            };

            return collectionTypes.Contains(typeSymbol.Name);
        }
    }
}