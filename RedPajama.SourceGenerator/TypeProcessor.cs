using System.Collections.Generic;
using System.CodeDom.Compiler;
using System.IO;
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
        private readonly HashSet<string> _processedTypes = new HashSet<string>();
        private readonly HashSet<string> _generatedMethods = new HashSet<string>();

        public void ProcessType(ITypeSymbol typeSymbol, TypeModelContextData contextData, IndentedTextWriter writer)
        {
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
                            ProcessPropertyType(property.Type, contextData, writer);
                        }
                    }

                    // Generate helper method for this type
                    string methodName = $"Build{GetLongName(typeSymbol)}Model";
                    
                    if (_generatedMethods.Add(methodName))
                    {
                        GenerateTypeHelperMethod(typeSymbol, typeName, writer);
                    }
                }
            }
        }

        private static string GetLongName(ITypeSymbol typeSymbol)
        {
            return typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace(".", "").Replace(":", "");
        }

        private void ProcessPropertyType(ITypeSymbol typeSymbol, TypeModelContextData contextData, IndentedTextWriter writer)
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
                ProcessPropertyType(arrayType.ElementType, contextData, writer);
                return;
            }

            // Handle generic collections
            if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                foreach (var typeArg in namedType.TypeArguments)
                {
                    ProcessPropertyType(typeArg, contextData, writer);
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
                string methodName = $"Build{GetLongName(typeSymbol)}Model";
                
                if (_generatedMethods.Add(methodName))
                {
                    GenerateTypeHelperMethod(typeSymbol, typeName, writer);
                }

                // Process properties
                foreach (var member in typeSymbol.GetMembers())
                {
                    if (member is IPropertySymbol property &&
                        property.GetMethod != null &&
                        property.GetMethod.DeclaredAccessibility == Accessibility.Public)
                    {
                        ProcessPropertyType(property.Type, contextData, writer);
                    }
                }
            }
            else if (typeSymbol.TypeKind == TypeKind.Enum)
            {
                // Generate enum helper
                string methodName = $"Build{GetLongName(typeSymbol)}EnumModel";
                
                if (_generatedMethods.Add(methodName))
                {
                    GenerateEnumHelperMethod(typeSymbol, writer);
                }
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
                        writer.WriteLine($"\"{EscapeString(description)}\"));");
                    else
                        writer.WriteLine("null));");
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
                    return "new IntegerTypeModel(\"int\")";
                case SpecialType.System_Int64:
                    return "new IntegerTypeModel(\"long\")";
                case SpecialType.System_Decimal:
                    return "new DecimalTypeModel(\"decimal\")";
                case SpecialType.System_Double:
                    return "new DecimalTypeModel(\"double\")";
                case SpecialType.System_Single:
                    return "new DecimalTypeModel(\"float\")";
                case SpecialType.System_Boolean:
                    return "new BoolTypeModel(\"bool\")";
            }

            // Handle other common types
            var typeName = typeSymbol.Name;

            if (typeName is "DateTime" or "DateTimeOffset")
            {
                return $"new DateTypeModel(\"{typeName}\")";
            }

            if (typeName == "Guid")
            {
                return "new GuidTypeModel(\"Guid\")";
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
                return "new StringTypeModel(\"string\")";
            }
            
            int? minLength = null;
            int? maxLength = null;
            string[] allowedValues = null;

            // Look for attributes
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
                .Select(attr => attr.ConstructorArguments[0].Value?.ToString())
                .FirstOrDefault();
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