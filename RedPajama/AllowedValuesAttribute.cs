namespace RedPajama;

/// <summary>
/// Specifies the allowed values for a property.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class AllowedValuesAttribute : Attribute
{
    /// <summary>
    /// Gets the allowed values.
    /// </summary>
    public string[] Values { get; }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="AllowedValuesAttribute"/> class.
    /// </summary>
    /// <param name="values">The allowed values.</param>
    public AllowedValuesAttribute(params string[] values)
    {
        Values = values;
    }
}
