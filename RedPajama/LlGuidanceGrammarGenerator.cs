namespace RedPajama
{
    /// <summary>
    /// Configuration settings for the LLguidance grammar generator.
    /// </summary>
    public class LlGuidanceGeneratorSettings
    {
        /// <summary>
        /// The minimum length for generated text sequences.
        /// </summary>
        public int DefaultMinLength { get; init; } = 1;

        /// <summary>
        /// The maximum length for generated text sequences.
        /// </summary>
        public int DefaultMaxLength { get; init; } = 512;

        /// <summary>
        /// The opening delimiter character for grammar rules.
        /// </summary>
        public char OpeningDelimiter { get; init; } = '⟨';

        /// <summary>
        /// The closing delimiter character for grammar rules.
        /// </summary>
        public char ClosingDelimiter { get; init; } = '⟩';
    }

    /// <summary>
    /// Generates LLGuidance  grammar rules based on the provided settings and root type.
    /// </summary>
    public class LlGuidanceGenerator
    {
        private readonly LlGuidanceGeneratorSettings _settings;
        private readonly HashSet<string> _generatedRules = new();
        private readonly List<string> _rules = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="LlGuidanceGenerator"/> class.
        /// </summary>
        /// <param name="settings">The settings to use for the generator. If null, default settings are used.</param>
        public LlGuidanceGenerator(LlGuidanceGeneratorSettings? settings = null)
        {
            _settings = settings ?? new LlGuidanceGeneratorSettings();
        }

        /// <summary>
        /// Generates a complete LLGuidance  grammar for the given root type.
        /// </summary>
        /// <param name="rootType">The root type to generate the grammar from.</param>
        /// <returns>A string containing the complete LLGuidance grammar.</returns>
        public string Generate(BaseTypeModel rootType)
        {
            _rules.Clear();
            _generatedRules.Clear();

            // Add header (if needed) and the two basic tokens.
            const string header = "%llguidance {}\n\n";

            // The CHAR token – note the regex is built from the settings’ delimiters.
            var charRule =
                $$"""
                  CHAR: /[^"\\\x7F\x00-\x1F{{_settings.OpeningDelimiter}}{{_settings.ClosingDelimiter}}]/
                       | /[\\]/ (/["\\bfnrt]/ | "u" /[0-9a-fA-F]/{4,4})
                  """;
            // The SPACE token
            const string spaceRule =
                """
                SPACE: ""
                     | " "
                     | "\n" /[ \t]/{0,20}
                """;
            _rules.Add(charRule);
            _rules.Add(spaceRule);

            // Generate the root rule from the model.
            var rootRuleBody = GenerateTypeRule(rootType, "root");
            // In Lark the start rule is named 'start'
            _rules.Add($"start: {rootRuleBody}");

            return header + string.Join("\n", _rules);
        }

        /// <summary>
        /// Dispatches to the appropriate rule generator based on the type.
        /// </summary>
        private string GenerateTypeRule(BaseTypeModel type, string ruleName)
        {
            return type switch
            {
                StringTypeModel stringTypeModel => GenerateStringRule(stringTypeModel, ruleName),
                IntegerTypeModel => GenerateIntegerRule(),
                DecimalTypeModel => GenerateDecimalRule(),
                DateTypeModel => GenerateDateRule(),
                ArrayTypeModel arrayType => GenerateArrayRule(arrayType, ruleName),
                TypeModel complexType => GenerateComplexTypeRule(complexType, ruleName),
                EnumTypeModel enumType => GenerateEnumRule(enumType),
                _ => throw new ArgumentException($"Unsupported type: {type.GetType().Name}")
            };
        }

        /// <summary>
        /// Generates a rule for string types.
        /// </summary>
        private string GenerateStringRule(StringTypeModel stringTypeModel, string ruleName)
        {
            if (stringTypeModel.AllowedValues.Length != 0)
            {
                // Allowed values become a series of literal alternatives.
                var values = string.Join(" | ", stringTypeModel.AllowedValues
                    .Select(v => $"""
                                  "\"{v}\""
                                  """));
                _rules.Add($"{LarkRuleName(ruleName)}: ({values}) SPACE");
                return LarkRuleName(ruleName);
            }

            var lengthConstraint = $"{{{_settings.DefaultMinLength},{_settings.DefaultMaxLength}}}";
            if (stringTypeModel.MinLength != null || stringTypeModel.MaxLength != null)
            {
                var min = stringTypeModel.MinLength ?? _settings.DefaultMinLength;
                var max = stringTypeModel.MaxLength ?? _settings.DefaultMaxLength;
                lengthConstraint = $"{{{min},{(max == int.MaxValue ? "" : max.ToString())}}}";
            }
            // A string is defined as a quoted sequence of CHAR tokens.
            return $"""
                    "\"" CHAR{lengthConstraint} "\"" SPACE
                    """;
        }

        /// <summary>
        /// Generates a rule for integer types.
        /// </summary>
        private string GenerateIntegerRule()
        {
            // Lark integer: optional "-" then either /[0]/ or /[1-9]/ followed by /[0-9]/{0,15}
            return """
                   ("-"? /[0]/ | /[1-9]/ /[0-9]/{0,15}) SPACE
                   """;
        }

        /// <summary>
        /// Generates a rule for decimal types.
        /// </summary>
        private string GenerateDecimalRule()
        {
            return """
                   "-"? (/[0]/ | /[1-9]/ /[0-9]/{0,15}) ("." /[0-9]/{1,15})? SPACE
                   """;
        }

        /// <summary>
        /// Generates a rule for date types.
        /// </summary>
        private string GenerateDateRule()
        {
            // This follows the example Lark rule for dates.
            return """
                   "\"" /[0-9]/{4,4} "-" (/[0]/ /[1-9]/ | /[1]/ /[0-2]/) "-" (/[0]/ /[1-9]/ | /[12]/ /[0-9]/ | /[3]/ /[01]/) "T" (/[01]/ /[0-9]/ | /[2]/ /[0-3]/) ":" /[0-5]/ /[0-9]/ ":" /[0-5]/ /[0-9]/ ("." /[0-9]/{3,3})? ("Z" | /[+-]/ (/[01]/ /[0-9]/ | /[2]/ /[0-3]/) ":" /[0-5]/ /[0-9]/) "\"" SPACE
                   """;
        }

        /// <summary>
        /// Generates a rule for array types.
        /// </summary>
        private string GenerateArrayRule(ArrayTypeModel arrayType, string ruleName)
        {
            // Define a sub-rule for the array’s items.
            var itemRuleName = ruleName + "_item";
            var itemRule = GenerateTypeRule(arrayType.ArrayType, itemRuleName);
            if (!_generatedRules.Contains(itemRuleName))
            {
                _rules.Add($"{LarkRuleName(itemRuleName)}: {itemRule}");
                _generatedRules.Add(itemRuleName);
            }
            // An array is a bracketed, comma-separated list.
            return $"""
                    "[" SPACE ({LarkRuleName(itemRuleName)} ("," SPACE {LarkRuleName(itemRuleName)})*)? "]" SPACE
                    """;
        }

        /// <summary>
        /// Generates a rule for complex (object) types.
        /// </summary>
        private string GenerateComplexTypeRule(TypeModel type, string ruleName)
        {
            var propertyRules = new List<string>();
            foreach (var property in type.Properties)
            {
                // Create a sub-rule for each property.
                var propertyRuleName = ruleName + "_" + property.Name.ToLowerInvariant();
                var propertyValueRule = GenerateTypeRule(property.PropertyType, propertyRuleName);
                var kvRuleName = propertyRuleName + "_kv";
                _rules.Add($"{LarkRuleName(kvRuleName)}: \"\\\"{property.Name}\\\"\" SPACE \":\" SPACE {propertyValueRule}");
                propertyRules.Add(LarkRuleName(kvRuleName));
            }
            // The complex type is represented as a JSON–like object.
            var joinedPropertyRules = string.Join(" \",\" SPACE ", propertyRules);
            return $$"""
                     "{" SPACE {{joinedPropertyRules}} "}" SPACE
                     """;
        }

        /// <summary>
        /// Generates a rule for enum types.
        /// </summary>
        private string GenerateEnumRule(EnumTypeModel type)
        {
            var values = string.Join(" | ", type.EnumValues.Select(v => $"""
                                                                         "\"{v}\""
                                                                         """));
            return $"({values}) SPACE";
        }

        /// <summary>
        /// Converts a rule name into a Lark-compatible name.
        /// 'root' is mapped to 'start' (the entry rule) and all other names are uppercased with underscores.
        /// </summary>
        private string LarkRuleName(string ruleName)
        {
            return ruleName == "root"
                ? "start"
                : ruleName.ToUpperInvariant().Replace("-", "_");
        }
    }
}