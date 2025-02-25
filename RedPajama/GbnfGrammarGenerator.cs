namespace RedPajama;

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
    
    private readonly HashSet<string> _generatedRules = [];
    private readonly List<string> _rules = [];

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
        _rules.Clear();
        _generatedRules.Clear();
        
        var charRule =
            $$"""char ::= [^"\\\x7F\x00-\x1F{{_settings.OpeningDelimiter}}{{_settings.ClosingDelimiter}}] | [\\] (["\\bfnrt] | "u" [0-9a-fA-F]{4})""";
        const string spaceRule = """space ::= | " " | "\n" [ \t]{0,20}""";

        // Add basic rules that are always needed
        _rules.Add(charRule);
        _rules.Add(spaceRule);
        
        // Generate the root rule
        var rootRule = GenerateTypeRule(rootType, "root");
        _rules.Add($"root ::= {rootRule}");
        
        return string.Join("\n", _rules);
    }
    
    private string GenerateTypeRule(BaseTypeModel type, string ruleName)
    {
        return type switch
        {
            StringTypeModel stringTypeModel=> GenerateStringRule(stringTypeModel, ruleName),
            IntegerTypeModel => GenerateIntegerRule(),
            DecimalTypeModel => GenerateDecimalRule(),
            DateTypeModel => GenerateDateRule(),
            BoolTypeModel => GenerateBoolRule(),
            ArrayTypeModel arrayType => GenerateArrayRule(arrayType, ruleName),
            TypeModel complexType => GenerateComplexTypeRule(complexType, ruleName),
            EnumTypeModel enumType => GenerateEnumRule(enumType),
            _ => throw new ArgumentException($"Unsupported type: {type.GetType().Name}")
        };
    }
    
    private string GenerateStringRule(StringTypeModel stringTypeModel, string ruleName)
    {
        if (stringTypeModel.AllowedValues.Length != 0)
        {
            var values = string.Join('|', stringTypeModel.AllowedValues
                .Select(v => $"""
                              "\"{v}\""
                              """));
            _rules.Add($"{ruleName} ::= ({values}) space");
            return ruleName;
        }

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
    
    private string GenerateDateRule()
    {
        return """
               "\"" [0-9]{4} "-" ([0][1-9]|[1][0-2]) "-" ([0][1-9]|[12][0-9]|[3][01]) "T" ([01][0-9]|[2][0-3]) ":" [0-5][0-9] ":" [0-5][0-9] ("." [0-9]{3})? ("Z"|([+-] ([01][0-9]|[2][0-3]) ":" [0-5][0-9])) "\"" space
               """;
    }
    
    private string GenerateArrayRule(ArrayTypeModel arrayType, string ruleName)
    {
        var itemRuleName = $"{ruleName}-item";
        var itemRule = GenerateTypeRule(arrayType.ArrayType, itemRuleName);
        
        if (!_generatedRules.Contains(itemRuleName))
        {
            _rules.Add($"{itemRuleName} ::= {itemRule}");
            _generatedRules.Add(itemRuleName);
        }
        
        return $"""
                "[" space ({itemRuleName} ("," space {itemRuleName})*)? "]" space
                """;
    }
    
    private string GenerateComplexTypeRule(TypeModel type, string ruleName)
    {
        var propertyRules = new List<string>();
        
        foreach (var property in type.Properties)
        {
            var propertyRuleName = $"{ruleName}-{property.Name.ToLowerInvariant()}";
            var propertyValueRule = GenerateTypeRule(property.PropertyType, propertyRuleName);
            
            // Generate the key-value pair rule
            var kvRuleName = $"{propertyRuleName}-kv";
            _rules.Add($"""{kvRuleName} ::= "\"{property.Name}\"" space ":" space {propertyValueRule}""");
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