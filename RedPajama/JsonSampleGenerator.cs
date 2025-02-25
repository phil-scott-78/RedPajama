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
            BoolTypeModel => AsTemplate($"\"true or value value of {propName}\""),
            StringTypeModel => AsTemplate($"\"string value of {propName}\""),
            IntegerTypeModel => AsTemplate($"integer value of {propName}"),
            DecimalTypeModel => AsTemplate($"decimal value of {propName}"),
            DateTypeModel => AsTemplate($"\"ISO 8601 format date value of {propName}\""),
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
        return type switch
        {
            StringTypeModel => AsTemplate($"\"{propName}_{index}\""),
            IntegerTypeModel => AsTemplate($"{propName}_{index}"),
            DecimalTypeModel => AsTemplate($"{propName}_{index}"),
            BoolTypeModel => AsTemplate($"{propName}_{index}"),
            DateTypeModel => AsTemplate($"\"{propName}_{index}\""),
            EnumTypeModel => AsTemplate($"\"{propName}_{index}\""),
            _ => $"{propName}_{index}"
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
