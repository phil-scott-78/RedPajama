namespace RedPajama;

public class GbnfGeneratorSettings
{
    public int DefaultMinLength { get; init; } = 1;
    public int DefaultMaxLength { get; init; } = 512;
    public char OpeningDelimiter { get; init; } = '⟨';
    public char ClosingDelimiter { get; init; } = '⟩';
    public bool PrettyPrint { get; init; } = true;
    public string Indent { get; init; } = "    ";
}

public class GbnfGenerator(GbnfGeneratorSettings? settings = null)
{
    private readonly GbnfGeneratorSettings _settings = settings ?? new GbnfGeneratorSettings();
    
    private readonly HashSet<string> _generatedRules = [];
    private readonly List<string> _rules = [];
    
    public string Generate(BaseTypeModel rootType)
    {
        _rules.Clear();
        _generatedRules.Clear();
        
        var charRule =  """char ::= [^"\\\x7F\x00-\x1F""" + _settings.OpeningDelimiter + _settings.ClosingDelimiter + """] | [\\] (["\\bfnrt] | "u" [0-9a-fA-F]{4})""";
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
        if (stringTypeModel.MinLength != null || stringTypeModel.MaxLength != null)
        {
            var min = stringTypeModel.MinLength ?? _settings.DefaultMinLength;
            var max = stringTypeModel.MaxLength ?? _settings.DefaultMaxLength;
            lengthConstraint = $"{{{min},{(max == int.MaxValue ? "" : max.ToString())}}}";
        }
        
        return $"""
                "\"" char{lengthConstraint} "\"" space
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
        
        return $"\"[\" space ({itemRuleName} (\",\" space {itemRuleName})*)? \"]\" space";
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
            _rules.Add($"{kvRuleName} ::= \"\\\"{property.Name}\\\"\" space \":\" space {propertyValueRule}");
            propertyRules.Add(kvRuleName);
        }
        
        return $"\"{{\" space {string.Join(" \",\" space ", propertyRules)} \"}}\" space";
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