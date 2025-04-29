namespace RedPajama;

/// <summary>
/// Settings for the JSON sample generator.
/// </summary>
public class JsonSampleGeneratorSettings
{
    /// <summary>
    /// The opening delimiter character for grammar rules.
    /// </summary>
    /// <value>Default value is '⟨' (U+27E8).</value>
    public char OpeningDelimiter { get; init; } = '⟨';

    /// <summary>
    /// The closing delimiter character for grammar rules.
    /// </summary>
    /// <value>Default value is '⟩' (U+27E9).</value>
    public char ClosingDelimiter { get; init; } = '⟩';

    /// <summary>
    /// Gets a value indicating whether to pretty print the JSON.
    /// </summary>
    public bool PrettyPrint { get; init; } = true;

    /// <summary>
    /// Gets the string used for indentation in pretty printed JSON.
    /// </summary>
    public string Indent { get; init; } = "    ";
}

/// <summary>
/// Generates JSON samples based on the provided type models and settings.
/// </summary>
public class JsonSampleGenerator
{
    private readonly JsonSampleGeneratorSettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonSampleGenerator"/> class.
    /// </summary>
    /// <param name="settings">The settings for the JSON sample generator. If null, default settings will be used.</param>
    public JsonSampleGenerator(JsonSampleGeneratorSettings? settings = null)
    {
        _settings = settings ?? new JsonSampleGeneratorSettings();
    }

    /// <summary>
    /// Generates a JSON sample based on the provided type model.
    /// </summary>
    /// <param name="model">The type model to generate the JSON sample for.</param>
    /// <returns>A JSON sample as a string.</returns>
    public string Generate(TypeModel model)
    {
        return GenerateForType(model, 0);
    }

    /// <summary>
    /// Sample instructions to use to nudge the LLM to output the JSON with template content replaced.
    /// </summary>
    /// <returns></returns>
    public string SampleInstructions()
    {
        return
            $"Replace all placeholders, {AsTemplate("...")}, in the format with the actual values extracted from the text. Do not return placeholders in the final output.";
    }

    private string GenerateForType(BaseTypeModel type, int indentLevel, string? propName = null)
    {
        propName ??= type.Name;

        return type switch
        {
            TypeModel complexType => GenerateComplexType(complexType, indentLevel),
            ArrayTypeModel arrayType => GenerateArray(arrayType, propName, indentLevel),
            EnumTypeModel enumType => GenerateEnum(enumType),
            BoolTypeModel => AsTemplate($"true or false"),
            StringTypeModel stringTypeModel => "\"" + AsTemplate(GetFormatDescription(stringTypeModel, propName)) + "\"",
            IntegerTypeModel => AsTemplate($"integer value"),
            DecimalTypeModel => AsTemplate($"decimal value"),
            GuidTypeModel => AsTemplate($"GUID value in standard format"),
            DateTypeModel => AsTemplate($"ISO 8601 date value (YYYY-MM-DDThh:mm:ss.sssZ)"),
            _ => throw new ArgumentException($"Unsupported type: {type.GetType().Name}")
        };
    }


    private string GenerateComplexType(TypeModel type, int indentLevel)
    {
        var prettyPrint = _settings.PrettyPrint;
        var indent = _settings.Indent;


        if (type.Properties.Length == 0)
            return "{}";

        var newLine = prettyPrint ? "\n" : "";
        var indent1 = prettyPrint ? string.Concat(Enumerable.Repeat(indent, indentLevel)) : "";
        var innerIndent = prettyPrint ? string.Concat(Enumerable.Repeat(indent, indentLevel + 1)) : "";

        var properties = type.Properties.Select((prop, index) =>
        {
            var valueStr = GenerateForProperty(prop, indentLevel + 1);
            var commentStr = GenerateComment(prop);
            var comma = index < type.Properties.Length - 1 ? "," : "";

            return $"{innerIndent}\"{prop.Name}\": {valueStr}{comma}{commentStr}";
        });

        return $"{{{newLine}{string.Join(newLine, properties)}{newLine}{indent1}}}";
    }

    private string GenerateArray(ArrayTypeModel type, string propName, int indentLevel)
    {
        // Generate first element with _1 suffix if it's a string with allowed values
        var firstElement = GenerateArrayElement(type.ArrayType, 1, propName, indentLevel);

        // Generate second element with _2 placeholder
        var secondElement = GenerateArrayPlaceholder(type.ArrayType, "2", propName);

        // Generate N placeholder for additional elements
        var nElement = GenerateArrayPlaceholder(type.ArrayType, "N", propName);

        return $"[{firstElement}, {secondElement}, {nElement}]";
    }

    private string GenerateArrayElement(BaseTypeModel type, int index, string propName, int indentLevel)
    {
        var value = GenerateForType(type, indentLevel, propName);
        if (value.StartsWith("\"" + _settings.OpeningDelimiter) && value.EndsWith(_settings.ClosingDelimiter + "\""))
        {
            // For string types with allowed values, add the index suffix
            return value.Insert(value.Length - 2, $"_{index}");
        }

        if (value.StartsWith(_settings.OpeningDelimiter) && value.EndsWith(_settings.ClosingDelimiter))
        {
            // For string types with allowed values, add the index suffix
            return value.Insert(value.Length - 1, $"_{index}");
        }

        return value;
    }

    private string GenerateArrayPlaceholder(BaseTypeModel type, string index, string propName)
    {
        string indexedPropName = $"{propName}_{index}";

        return type switch
        {
            StringTypeModel stringTypeModel =>
                stringTypeModel.Format != null
                    ? AsTemplate($"\"{GetFormatDescription(stringTypeModel, indexedPropName)}\"")
                    : AsTemplate($"\"{indexedPropName}\""),
            IntegerTypeModel => AsTemplate($"{indexedPropName}"),
            DecimalTypeModel => AsTemplate($"{indexedPropName}"),
            BoolTypeModel => AsTemplate($"{indexedPropName}"),
            DateTypeModel => AsTemplate($"\"{indexedPropName}\""),
            EnumTypeModel => AsTemplate($"\"{propName}_{index}\""),
            GuidTypeModel => AsTemplate($"\"{indexedPropName}\""),
            _ => $"{indexedPropName}"
        };
    }

    private string GenerateEnum(EnumTypeModel type)
    {
        var values = string.Join("|", type.EnumValues);
        return AsTemplate($"\"{values}\"");
    }

    private string GenerateForProperty(PropertyModel prop, int indentLevel)
    {
        if (prop.PropertyType is not StringTypeModel { AllowedValues.Length: > 0 } stringTypeModel)
        {
            return GenerateForType(prop.PropertyType, indentLevel, prop.Name);
        }

        // If we have allowed values, generate them as an enum-style list
        var values = string.Join("|", stringTypeModel.AllowedValues);
        return AsTemplate($"\"{values}\"");
    }

    private string GetFormatDescription(StringTypeModel stringTypeModel, string propName)
    {
        if (string.IsNullOrEmpty(stringTypeModel.Format))
            return $"string value";

        // Extract the format
        var format = stringTypeModel.Format;

        // Handle the different format types
        if (format.StartsWith("gbnf:"))
        {
            // For raw GBNF, we'll provide a simplified description
            return $"string value in custom format";
        }

        switch (format)
        {
            // Predefined formats
            case "email":
                return $"email address (e.g., user@example.com)";

            case "guid":
            case "uuid":
                return $"UUID (e.g., 123e4567-e89b-12d3-a456-426614174000)";

            case "date":
                return $"date in YYYY-MM-DD format (e.g., 2023-12-31)";

            case "time":
                return $"time in HH:MM:SS format (e.g., 14:30:45)";

            case "phone-us":
                return $"US phone number in (XXX) XXX-XXXX format";

            case "alpha":
                return $"alphabetic string (letters only)";

            case "alpha-space":
                return $"string containing only letters and spaces";

            case "alphanumeric":
                return $"alphanumeric string (letters and numbers only)";

            case "lowercase":
                return $"lowercase string";

            case "uppercase":
                return $"uppercase string";

            case "numeric":
                return $"string of digits ";

            case "hex":
                return $"hexadecimal string";

            default:
                // For pattern-based formats
                if (format.Contains('#') || format.Contains('A') || format.Contains('a') || format.Contains('9'))
                {
                    return $"string in the format: {format}";
                }

                // Default case if we don't recognize the format
                return $"string value in {format} format";
        }
    }

    private string GenerateComment(PropertyModel propertyModel)
    {
        var description = propertyModel.Description;
        if (string.IsNullOrWhiteSpace(description))
            return "";


        return $" // {description}";
    }


    private string AsTemplate(string input) => input.StartsWith('\"') || input.EndsWith('\"')
        ? $"\"{_settings.OpeningDelimiter}{input.Substring(1, input.Length - 2)}{_settings.ClosingDelimiter}\""
        : $"{_settings.OpeningDelimiter}{input}{_settings.ClosingDelimiter}";
}