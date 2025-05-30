﻿using System.Collections.Generic;
using System.CodeDom.Compiler;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace RedPajama.SourceGenerator
{
    /// <summary>
    /// Helper class to process a type and its dependencies
    /// </summary>
    internal class TypeProcessor
    {
        private readonly HashSet<string> _processedTypes = new();
        private readonly HashSet<string> _generatedMethods = new();

        public void ProcessType(ITypeSymbol typeSymbol, IndentedTextWriter writer)
        {
            var typeFullName = typeSymbol.ToDisplayString();
            var typeName = typeSymbol.Name;

            // Process the type if it hasn't been processed yet
            if (!_processedTypes.Add(typeFullName)) return;
            
            // For complex types, process their properties recursively
            if (typeSymbol.TypeKind is not (TypeKind.Class or TypeKind.Struct)) return;
            
            foreach (var member in typeSymbol.GetMembers())
            {
                if (member is IPropertySymbol { GetMethod: { DeclaredAccessibility: Accessibility.Public } } property)
                {
                    ProcessPropertyType(property.Type, writer, property);
                }
            }

            // Generate helper method for this type
            var methodName = $"Build{GetLongName(typeSymbol)}Model";
                    
            if (_generatedMethods.Add(methodName))
            {
                GenerateTypeHelperMethod(typeSymbol, typeName, writer);
            }
        }

        private static string GetLongName(ITypeSymbol typeSymbol)
        {
            return typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace(".", "").Replace(":", "");
        }

        private void ProcessPropertyType(ITypeSymbol typeSymbol, IndentedTextWriter writer, IPropertySymbol propertySymbol = null)
        {
            while (true)
            {
                var typeFullName = typeSymbol.ToDisplayString();

                // Skip primitives and already processed types
                if (IsPrimitiveType(typeSymbol) || !_processedTypes.Add(typeFullName))
                {
                    return;
                }

                switch (typeSymbol)
                {
                    // Handle arrays and collections
                    case IArrayTypeSymbol arrayType:
                        // Handle string arrays with allowed values separately
                        if (arrayType.ElementType.SpecialType == SpecialType.System_String && 
                            propertySymbol != null && 
                            HasAllowedValuesAttribute(propertySymbol))
                        {
                            return;
                        }
                        
                        typeSymbol = arrayType.ElementType;
                        continue;
                    // Handle generic collections
                    case INamedTypeSymbol { IsGenericType: true } namedType:
                    {
                        // Handle string collections with allowed values
                        if (namedType.TypeArguments.Length == 1 && 
                            namedType.TypeArguments[0].SpecialType == SpecialType.System_String &&
                            propertySymbol != null && 
                            HasAllowedValuesAttribute(propertySymbol) &&
                            IsCollectionType(namedType))
                        {
                            return;
                        }
                        
                        foreach (var typeArg in namedType.TypeArguments)
                        {
                            ProcessPropertyType(typeArg, writer);
                        }

                        // If it's a collection, we're done
                        if (IsCollectionType(namedType))
                        {
                            return;
                        }

                        break;
                    }
                }

                switch (typeSymbol.TypeKind)
                {
                    // For complex types, process their properties recursively
                    case TypeKind.Class or TypeKind.Struct:
                    {
                        // Generate helper method for this type
                        var typeName = typeSymbol.Name;
                        var methodName = $"Build{GetLongName(typeSymbol)}Model";

                        if (_generatedMethods.Add(methodName))
                        {
                            GenerateTypeHelperMethod(typeSymbol, typeName, writer);
                        }

                        // Process properties
                        foreach (var member in typeSymbol.GetMembers())
                        {
                            if (member is IPropertySymbol { GetMethod: { DeclaredAccessibility: Accessibility.Public } } property)
                            {
                                ProcessPropertyType(property.Type, writer, property);
                            }
                        }

                        break;
                    }
                    case TypeKind.Enum:
                    {
                        // Generate enum helper
                        var methodName = $"Build{GetLongName(typeSymbol)}EnumModel";

                        if (_generatedMethods.Add(methodName))
                        {
                            GenerateEnumHelperMethod(typeSymbol, writer);
                        }

                        break;
                    }
                }

                break;
            }
        }

        /// <summary>
        /// Generates a helper method for building a type model
        /// </summary>
        private void GenerateTypeHelperMethod(ITypeSymbol typeSymbol, string typeName, IndentedTextWriter writer)
        {
            var longName = GetLongName(typeSymbol);
            writer.WriteLine($"private TypeModel Build{longName}Model()");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("var properties = new List<PropertyModel>();");
            writer.WriteLine();

            // For a class or struct, add all properties
            if (typeSymbol.TypeKind is TypeKind.Class or TypeKind.Struct)
            {
                foreach (var member in typeSymbol.GetMembers())
                {
                    if (member is not IPropertySymbol { GetMethod: { DeclaredAccessibility: Accessibility.Public } } property) 
                        continue;
                    
                    writer.WriteLine($"// Property: {property.Name}");
                    writer.WriteLine("properties.Add(new PropertyModel(");
                    writer.Indent++;
                    writer.WriteLine($"\"{property.Name}\",");
                    writer.WriteLine($"{GetPropertyTypeCode(property)},");

                    var description = GetDescriptionFromAttributes(property);
                    if (!string.IsNullOrEmpty(description))
                    {
                        writer.WriteLine($"\"{EscapeString(description)}\"));");
                    }
                    else
                    {
                        writer.WriteLine("null));");
                    }
                    
                    writer.Indent--;
                }
            }

            writer.WriteLine();
            writer.WriteLine($"return new TypeModel(\"{typeName}\", properties);");
            writer.Indent--;
            writer.WriteLine("}");
        }

        /// <summary>
        /// Generates a helper method for building an enum type model
        /// </summary>
        private void GenerateEnumHelperMethod(ITypeSymbol enumType, IndentedTextWriter writer)
        {
            var longName = GetLongName(enumType);
            var typeName = enumType.Name;

            writer.WriteLine($"private EnumTypeModel Build{longName}EnumModel()");
            writer.WriteLine("{");
            writer.Indent++;

            // Get enum values
            var enumValues = enumType.GetMembers()
                .Where(m => m.Kind == SymbolKind.Field && m.Name != "value__")
                .Select(m => $"\"{m.Name}\"");

            writer.WriteLine($"return new EnumTypeModel(\"{typeName}\", new[] {{{string.Join(", ", enumValues)}}});");
            writer.Indent--;
            writer.WriteLine("}");
        }

        /// <summary>
        /// Gets the builder code for a type model builder method
        /// </summary>
        public string GetBuilderCode(ITypeSymbol typeSymbol)
        {
            return $"return Build{GetLongName(typeSymbol)}Model();";
        }

        private string GetPropertyTypeCode(IPropertySymbol property)
        {
            // Handle arrays and collections with string elements
            if (property.Type is IArrayTypeSymbol { ElementType: { SpecialType: SpecialType.System_String } })
            {
                // If property has AllowedValues attribute, apply it to the element type
                if (HasAllowedValuesAttribute(property))
                {
                    var elementStringTypeModel = GetStringTypeModelCode(property);
                    return $"new ArrayTypeModel(\"stringArray\", {elementStringTypeModel})";
                }
            }
            else if (property.Type is INamedTypeSymbol { IsGenericType: true } namedType && 
                     IsCollectionType(namedType) && 
                     namedType.TypeArguments.Length == 1 && 
                     namedType.TypeArguments[0].SpecialType == SpecialType.System_String)
            {
                // If property has AllowedValues attribute, apply it to the element type
                if (HasAllowedValuesAttribute(property))
                {
                    var elementStringTypeModel = GetStringTypeModelCode(property);
                    return $"new ArrayTypeModel(\"stringArray\", {elementStringTypeModel})";
                }
            }
            
            return GetTypeCode(property.Type, property);
        }

        private string GetNormalizedTypeNameForModel(ITypeSymbol typeSymbol)
        {
            if (typeSymbol == null) return "object"; // Fallback for safety

            switch (typeSymbol.SpecialType)
            {
                case SpecialType.System_String: return "string";
                case SpecialType.System_Boolean: return "bool";   // Ensures "bool" for Boolean
                case SpecialType.System_Int16:   // short
                case SpecialType.System_Int32:   // int
                    return "int";                // Ensures "int" for Int32
                case SpecialType.System_Int64:   // long
                    return "long";
                case SpecialType.System_Decimal: return "decimal";
                case SpecialType.System_Single:  // float
                    return "float";
                case SpecialType.System_Double:  // double
                    return "double";
                case SpecialType.System_DateTime:
                    return "Date";               // Ensures "Date" for DateTime
                // Add other special types if they need specific model names
            }

            // Fallback for types not covered by SpecialType or needing specific name handling
            // (e.g., Guid, DateTimeOffset, enums, complex types)
            string name = typeSymbol.Name;
            switch (name)
            {
                case "DateTimeOffset":
                    return "Date"; // Align DateTimeOffset with DateTime
                case "Guid":
                    return "Guid";
                default:
                    // For enums, other complex types, or any unhandled primitives,
                    // use the type's .NET name. This matches TypeModelBuilder's fallback.
                    return name;
            }
        }

        private string GetTypeCode(ITypeSymbol typeSymbol, IPropertySymbol property)
        {
            var originalTypeName = typeSymbol.Name; // Keep for specific cases if needed below

            // Use normalized names for the model's "Name" property
            var modelName = GetNormalizedTypeNameForModel(typeSymbol);

            switch (typeSymbol.SpecialType)
            {
                case SpecialType.System_String:
                    return GetStringTypeModelCode(property); // Assumes GetStringTypeModelCode uses "string" internally
                case SpecialType.System_Int16: // Added short for completeness
                case SpecialType.System_Int32:
                    return $"new IntegerTypeModel(\"{modelName}\")"; // Uses "int"
                case SpecialType.System_Int64:
                    return $"new IntegerTypeModel(\"{modelName}\")"; // Uses "long"
                case SpecialType.System_Decimal:
                    return $"new DecimalTypeModel(\"{modelName}\")"; // Uses "decimal"
                case SpecialType.System_Double:
                    return $"new DecimalTypeModel(\"{modelName}\")"; // Uses "double"
                case SpecialType.System_Single:
                    return $"new DecimalTypeModel(\"{modelName}\")"; // Uses "float"
                case SpecialType.System_Boolean:
                    return $"new BoolTypeModel(\"{modelName}\")"; // Uses "bool"
                case SpecialType.System_DateTime: // Already handled by GetNormalizedTypeNameForModel to "Date"
                     return $"new DateTypeModel(\"{modelName}\")"; // Uses "Date"

            }

            // Handle by name for types not covered by SpecialType or needing specific logic
            switch (originalTypeName) // Using originalTypeName here for direct .NET type name matching
            {
                case "DateTimeOffset": // GetNormalizedTypeNameForModel handles this to "Date"
                    return $"new DateTypeModel(\"{modelName}\")"; // Uses "Date"
                case "Guid": // GetNormalizedTypeNameForModel handles this to "Guid"
                    return $"new GuidTypeModel(\"{modelName}\")"; // Uses "Guid"
            }

            switch (typeSymbol)
            {
                case IArrayTypeSymbol arrayType:
                {
                    var elementType = arrayType.ElementType;
                    var elementTypeCode = GetTypeCode(elementType, null); // Recursive call for element type model
                    var arrayElementModelName = GetNormalizedTypeNameForModel(elementType); // Normalized name for "int" in "intArray"
                    var minItems = GetMinLengthAttribute(property);
                    var maxItems = GetMaxLengthAttribute(property);
                    var minItemsStr = minItems.HasValue ? minItems.Value.ToString() : "null";
                    var maxItemsStr = maxItems.HasValue ? maxItems.Value.ToString() : "null";
                    
                    // Special handling for string arrays that might have specific attributes via GetPropertyTypeCode
                    if (elementType.SpecialType == SpecialType.System_String && HasAllowedValuesAttribute(property) && property != null)
                    {
                        // GetPropertyTypeCode likely handles the "stringArray" name and StringTypeModel correctly
                        return GetPropertyTypeCode(property); 
                    }

                    return $"new ArrayTypeModel(\"{arrayElementModelName}Array\", {elementTypeCode}, {minItemsStr}, {maxItemsStr})";
                }
                case INamedTypeSymbol { IsGenericType: true } namedType when IsCollectionType(namedType) && namedType.TypeArguments.Length == 1:
                {
                    var elementTypeArgument = namedType.TypeArguments[0];
                    var elementTypeCode = GetTypeCode(elementTypeArgument, null);
                    var collectionElementModelName = GetNormalizedTypeNameForModel(elementTypeArgument);
                    var minItems = GetMinLengthAttribute(property);
                    var maxItems = GetMaxLengthAttribute(property);
                    var minItemsStr = minItems.HasValue ? minItems.Value.ToString() : "null";
                    var maxItemsStr = maxItems.HasValue ? maxItems.Value.ToString() : "null";

                    // Special handling for string collections similar to string arrays
                    if (elementTypeArgument.SpecialType == SpecialType.System_String && HasAllowedValuesAttribute(property) && property != null)
                    {
                         // GetPropertyTypeCode likely handles the "stringArray" name and StringTypeModel correctly
                        return GetPropertyTypeCode(property);
                    }

                    return $"new ArrayTypeModel(\"{collectionElementModelName}Array\", {elementTypeCode}, {minItemsStr}, {maxItemsStr})";
                }
            }
            if (typeSymbol.TypeKind == TypeKind.Enum)
            {
                // Enum model name is taken directly from enum type name, e.g., "TestEnum"
                // This matches TypeModelBuilder which uses enumType.Name.
                return $"Build{GetLongName(typeSymbol)}EnumModel()"; 
            }

            // For complex types (classes/structs), the model name is the type's own name.
            // This matches TypeModelBuilder which uses type.Name for complex types.
            return $"Build{GetLongName(typeSymbol)}Model()";
        }

        private string GetStringTypeModelCode(IPropertySymbol property = null)
        {
            if (property == null)
            {
                return "new StringTypeModel(\"string\")";
            }
            
            int? minLength = null;
            int? maxLength = null;
            string format = null;
            string[] allowedValues = null;

            // Look for attributes
            foreach (var attr in property.GetAttributes().Where(attr => attr.AttributeClass != null))
            {
                switch (attr.AttributeClass!.Name)
                {
                    case "MinLengthAttribute" when attr.ConstructorArguments.Length > 0:
                        minLength = attr.ConstructorArguments[0].Value as int?;
                        break;
                    case "MaxLengthAttribute" when attr.ConstructorArguments.Length > 0:
                        maxLength = attr.ConstructorArguments[0].Value as int?;
                        break;
                    case "FormatAttribute" when attr.ConstructorArguments.Length > 0:
                        format = attr.ConstructorArguments[0].Value as string;
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

            if (minLength.HasValue || maxLength.HasValue || allowedValues != null || format != null)
            {
                var sb = new StringBuilder();
                sb.Append("new StringTypeModel(\"string\"");

                sb.Append(allowedValues != null
                    ? $", new[] {{{string.Join(", ", allowedValues.Select(v => $"\"{EscapeString(v)}\""))}}}"
                    : ", null");

                sb.Append($", {(minLength.HasValue ? minLength.ToString() : "null")}");
                sb.Append($", {(maxLength.HasValue ? maxLength.ToString() : "null")}");
                sb.Append($", {(!string.IsNullOrWhiteSpace(format) ? $"\"{EscapeString(format)}\"" : "null")})");
                return sb.ToString();
            }

            return "new StringTypeModel(\"string\")";
        }

        // Helper methods to extract attribute values (assuming they exist or would be added)
        private static int? GetMinLengthAttribute(IPropertySymbol property)
        {
            if (property == null) return null;
            var minLengthAttr = property.GetAttributes().FirstOrDefault(ad => ad.AttributeClass?.Name == "MinLengthAttribute");
            return minLengthAttr?.ConstructorArguments.FirstOrDefault().Value as int?;
        }

        private static int? GetMaxLengthAttribute(IPropertySymbol property)
        {
            if (property == null) return null;
            var maxLengthAttr = property.GetAttributes().FirstOrDefault(ad => ad.AttributeClass?.Name == "MaxLengthAttribute");
            return maxLengthAttr?.ConstructorArguments.FirstOrDefault().Value as int?;
        }
        
        private static bool HasAllowedValuesAttribute(IPropertySymbol property)
        {
            return property.GetAttributes()
                .Any(attr => attr.AttributeClass?.Name == "AllowedValuesAttribute");
        }

        private static string GetDescriptionFromAttributes(IPropertySymbol property)
        {
            return property.GetAttributes()
                .Where(attr => attr.AttributeClass?.Name == "DescriptionAttribute")
                .Where(attr => attr.ConstructorArguments.Length > 0)
                .Select(attr => attr.ConstructorArguments[0].Value?.ToString())
                .FirstOrDefault();
        }

        private static string EscapeString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return input
                .Replace("\\", @"\\")
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