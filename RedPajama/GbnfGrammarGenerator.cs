namespace RedPajama;

using System.Collections.Specialized;

/// <summary>
/// Configuration settings for the GBNF grammar generator.
/// </summary>
public class GbnfGeneratorSettings
{
    /// <summary>
    /// The minimum length for generated text sequences.
    /// </summary>
    /// <value>Default value is 1.</value>
    public int DefaultMinLength { get; init; } = 1;

    /// <summary>
    /// The maximum length for generated text sequences.
    /// </summary>
    /// <value>Default value is 512.</value>
    public int DefaultMaxLength { get; init; } = 512;

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
}

/// <summary>
/// Generates GBNF grammar rules based on the provided settings and root type.
/// </summary>
public class GbnfGenerator
{
    private readonly GbnfGeneratorSettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="GbnfGenerator"/> class with the specified settings.
    /// </summary>
    /// <param name="settings">The settings to use for the generator. If null, default settings will be used.</param>
    public GbnfGenerator(GbnfGeneratorSettings? settings = null)
    {
        _settings = settings ?? new GbnfGeneratorSettings();
    }

    /// <summary>
    /// Generates a GBNF grammar rule for the specified root type.
    /// </summary>
    /// <param name="rootType">The root type for which to generate the grammar rule.</param>
    /// <returns>A string containing the generated GBNF grammar rule.</returns>
    public string Generate(BaseTypeModel rootType)
    {
        var rules = new OrderedDictionary<string, string>();

        // Add basic rules that are always needed
        var charRule = $$"""char ::= [^"\\\x7F\x00-\x1F{{_settings.OpeningDelimiter}}{{_settings.ClosingDelimiter}}] | [\\] (["\\bfnrt] | "u" [0-9a-fA-F]{4})""";
        const string spaceRule = """space ::= | " " | "\n" [ \t]{0,20}""";

        rules["char"] = charRule;
        rules["space"] = spaceRule;

        // Generate the root rule
        var rootRule = GenerateTypeRule(rootType, "root", rules);
        
        rules.Insert(0, "root", $"root ::= {rootRule}");

        return string.Join("\n", rules.Values);
    }

    private string GenerateTypeRule(BaseTypeModel type, string ruleName, OrderedDictionary<string, string> rules)
    {
        return type switch
        {
            StringTypeModel stringTypeModel => GenerateStringRule(stringTypeModel, ruleName, rules),
            IntegerTypeModel => GenerateIntegerRule(),
            DecimalTypeModel => GenerateDecimalRule(),
            DateTypeModel => GenerateDateRule(),
            BoolTypeModel => GenerateBoolRule(),
            GuidTypeModel => GenerateGuidRule(),
            ArrayTypeModel arrayType => GenerateArrayRule(arrayType, ruleName, rules),
            TypeModel complexType => GenerateComplexTypeRule(complexType, ruleName, rules),
            EnumTypeModel enumType => GenerateEnumRule(enumType),
            _ => throw new ArgumentException($"Unsupported type: {type.GetType().Name}")
        };
    }

    private string GenerateFormatRule(string format)
    {
        switch (format)
        {
            case var f when f.StartsWith("gbnf:"):
                // Direct GBNF pattern injection
                var gbnfPattern = f["gbnf:".Length..];
                return $"{gbnfPattern}";
            
            // All your other cases for predefined formats...
        
            case "alpha-space":
                return """
                       "\"" [a-zA-Z ]{1,} "\"" space
                       """;
            case "alpha":
                return """
                       "\"" [a-zA-Z]+ "\"" space
                       """;

            case "alphanumeric":
                return """
                       "\"" [a-zA-Z0-9]+ "\"" space
                       """;

            case "lowercase":
                return """
                       "\"" [a-z]+ "\"" space
                       """;

            case "uppercase":
                return """
                       "\"" [A-Z]+ "\"" space
                       """;

            case "numeric":
                return """
                       "\"" [0-9]+ "\"" space
                       """;

            case "hex":
                return """
                       "\"" [0-9a-fA-F]+ "\"" space
                       """;

            case var _
                when format.Contains('#') || format.Contains('A') || format.Contains('a') || format.Contains('9'):
                // Pattern format style (e.g., "(###) ###-####" or "AA-999")
                return GeneratePatternBasedRule(format);

            default:
                // If format is not recognized, fall back to char-based rule with defaults
                var lengthConstraint = $$"""{{{_settings.DefaultMinLength}}, {{_settings.DefaultMaxLength}}}""";
                return $"""
                         "\"" char{lengthConstraint} "\"" space
                         """;
        }
    }

    // Helper method to translate pattern format strings to GBNF
    private string GeneratePatternBasedRule(string pattern)
    {
        var gbnfParts = new List<string>();
        gbnfParts.Add("\"\\\"\""); // Opening quote

        foreach (var c in pattern)
        {
            switch (c)
            {
                case '#': // Digit
                case '9': // Digit (alternative notation)
                    gbnfParts.Add("[0-9]");
                    break;

                case 'A': // Uppercase letter
                    gbnfParts.Add("[A-Z]");
                    break;

                case 'a': // Lowercase letter
                    gbnfParts.Add("[a-z]");
                    break;

                case '*': // Any letter or digit
                    gbnfParts.Add("[a-zA-Z0-9]");
                    break;

                case '?': // Any character
                    gbnfParts.Add("char");
                    break;

                default: // Literal character
                    // No need to escape special characters with backslash
                    // Just quote them directly
                    gbnfParts.Add($"\"{c}\"");
                    break;
            }
        }

        gbnfParts.Add("\"\\\"\""); // Closing quote
        gbnfParts.Add("space");

        return string.Join(" ", gbnfParts);
    }

    // Extension to the StringTypeModel to update GenerateStringRule
    private string GenerateStringRule(StringTypeModel stringTypeModel, string ruleName, OrderedDictionary<string, string> rules)
    {
        // Handle existing enumeration of allowed values
        if (stringTypeModel.AllowedValues.Length != 0)
        {
            var values = string.Join('|', stringTypeModel.AllowedValues
                .Select(v => $"""
                              "\"{v}\""
                              """));
            rules[$"{ruleName}"] = $"{ruleName} ::= ({values}) space";

            return ruleName;
        }

        // Handle format specification if provided
        if (!string.IsNullOrEmpty(stringTypeModel.Format))
        {
            string formatRule = GenerateFormatRule(stringTypeModel.Format);
            rules[$"{ruleName}"] = $"{ruleName} ::= {formatRule}";

            return ruleName;
        }

        // Default length-based rule (existing code)
        var lengthConstraint = $$"""{{{_settings.DefaultMinLength}}, {{_settings.DefaultMaxLength}}}""";
        if (stringTypeModel.MinLength == null && stringTypeModel.MaxLength == null)
        {
            return $"""
                    "\"" char{lengthConstraint} "\"" space
                    """;
        }

        var min = stringTypeModel.MinLength ?? _settings.DefaultMinLength;
        var max = stringTypeModel.MaxLength ?? _settings.DefaultMaxLength;
        lengthConstraint = $$"""
                             {{{min}},{{(max == int.MaxValue ? "" : max.ToString())}}}
                             """;

        return $"""
                "\"" char{lengthConstraint} "\"" space
                """;
    }

    private string GenerateBoolRule()
    {
        // Only support true or false values (without quotes)
        return """
               ("true" | "false") space
               """;
    }

    private string GenerateIntegerRule()
    {
        return """("-"? [0] | [1-9] [0-9]{0,15}) space""";
    }

    private string GenerateDecimalRule()
    {
        return """("-"? ([0] | [1-9] [0-9]{0,15}) ("." [0-9]{1,15})?) space""";
    }

    private string GenerateGuidRule()
    {
        return """
               "\"" [0-9a-fA-F]{8} "-" [0-9a-fA-F]{4} "-" [0-9a-fA-F]{4} "-" [0-9a-fA-F]{4} "-" [0-9a-fA-F]{12} "\"" space
               """;
    }

    private string GenerateDateRule()
    {
        return """
               "\"" [0-9]{4} "-" ([0][1-9]|[1][0-2]) "-" ([0][1-9]|[12][0-9]|[3][01]) "T" ([01][0-9]|[2][0-3]) ":" [0-5][0-9] ":" [0-5][0-9] ("." [0-9]{3})? ("Z"|([+-] ([01][0-9]|[2][0-3]) ":" [0-5][0-9])) "\"" space
               """;
    }

    private string GenerateArrayRule(ArrayTypeModel arrayType, string ruleName, OrderedDictionary<string, string> rules)
    {
        var itemRuleName = $"{ruleName}-item";
        var itemRule = GenerateTypeRule(arrayType.ArrayType, itemRuleName, rules);

        if (!rules.ContainsKey(itemRuleName))
        {
            rules[itemRuleName] = $"{itemRuleName} ::= {itemRule}";
        }

        return $"""
                "[" space ({itemRuleName} ("," space {itemRuleName})*)? "]" space
                """;
    }

    private string GenerateComplexTypeRule(TypeModel type, string ruleName, OrderedDictionary<string, string> rules)
    {
        var propertyRules = new List<string>();

        foreach (var property in type.Properties)
        {
            var propertyRuleName = $"{ruleName}-{property.Name.ToLowerInvariant()}";
            var propertyValueRule = GenerateTypeRule(property.PropertyType, propertyRuleName, rules);

            // Generate the key-value pair rule
            var kvRuleName = $"{propertyRuleName}-kv";
            rules[kvRuleName] = $"""{kvRuleName} ::= "\"{property.Name}\"" space ":" space {propertyValueRule}""";
            propertyRules.Add(kvRuleName);
        }

        return $$"""
                 "{" space {{string.Join(" \",\" space ", propertyRules)}} "}" space
                 """;
    }

    private string GenerateEnumRule(EnumTypeModel type)
    {
        var values = string.Join('|', type.EnumValues
            .Select(v => $"""
                          "\"{v}\""
                          """));

        return $"({values}) space";
    }
}