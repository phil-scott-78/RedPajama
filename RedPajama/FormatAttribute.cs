namespace RedPajama;

/// <summary>
/// Specifies how a string must be formatted. 
/// </summary>
/// <param name="format"></param>
[AttributeUsage(AttributeTargets.Property)]
public class FormatAttribute(string format) : Attribute
{
/// <summary>
/// Specifies how a string must be formatted. Format can be specified using predefined formats or custom patterns.
/// </summary>
/// <remarks>
/// This attribute supports several types of format specifications:
/// 
/// <para><b>Predefined Formats:</b></para>
/// <list type="bullet">
///   <item><term>alpha</term><description>Validates strings that contain only alphabetic characters (a-z, A-Z)</description></item>
///   <item><term>alpha-space</term><description>Validates strings that contain only alphabetic characters and spaces (a-z, A-Z, space)</description></item>
///   <item><term>alphanumeric</term><description>Validates strings that contain only alphanumeric characters (a-z, A-Z, 0-9)</description></item>
///   <item><term>lowercase</term><description>Validates strings that contain only lowercase letters (a-z)</description></item>
///   <item><term>uppercase</term><description>Validates strings that contain only uppercase letters (A-Z)</description></item>
///   <item><term>numeric</term><description>Validates strings that contain only digits (0-9)</description></item>
///   <item><term>hex</term><description>Validates strings that contain only hexadecimal characters (0-9, a-f, A-F)</description></item>
/// </list>
/// 
/// <para><b>Pattern-Based Formats:</b></para>
/// <para>You can define custom patterns using the following placeholder characters:</para>
/// <list type="table">
///   <item>
///     <term>#</term>
///     <description>Represents a digit (0-9)</description>
///   </item>
///   <item>
///     <term>9</term>
///     <description>Alternate notation for a digit (0-9)</description>
///   </item>
///   <item>
///     <term>A</term>
///     <description>Represents an uppercase letter (A-Z)</description>
///   </item>
///   <item>
///     <term>a</term>
///     <description>Represents a lowercase letter (a-z)</description>
///   </item>
///   <item>
///     <term>*</term>
///     <description>Represents any alphanumeric character (a-z, A-Z, 0-9)</description>
///   </item>
///   <item>
///     <term>?</term>
///     <description>Represents any character</description>
///   </item>
/// </list>
/// <para>All other characters in the pattern are treated as literals and must match exactly.</para>
/// 
/// <para><b>Examples:</b></para>
/// <list type="bullet">
///   <item><description><c>(###) ###-####</c> - US phone number with formatting</description></item>
///   <item><description><c>AA-####</c> - Two uppercase letters followed by a hyphen and four digits</description></item>
///   <item><description><c>a*****</c> - A lowercase letter followed by exactly 5 alphanumeric characters</description></item>
/// </list>
/// 
/// <para><b>Advanced Usage:</b></para>
/// <para>For more complex validation requirements, you can use raw GBNF syntax
/// by prefixing your pattern with "gbnf:"</para>
/// <example>
/// <code>
/// [Format("gbnf:\"\\\"\" [a-zA-Z ]{1,} \"\\\"\" space")]
/// public string Name { get; set; }
/// </code>
/// </example>
/// <para>Using the "gbnf:" prefix allows you to specify the exact GBNF grammar pattern
/// to use for validation, giving you full control over the validation rules.
/// Note that you must understand GBNF syntax to use this feature effectively.</para>
/// </remarks>
    public string Format { get; } = format;
}