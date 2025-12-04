namespace Treaty.Contracts;

/// <summary>
/// Represents an expectation for a query parameter.
/// </summary>
public sealed class QueryParameterExpectation
{
    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets whether this parameter is required.
    /// </summary>
    public bool IsRequired { get; }

    /// <summary>
    /// Gets the expected type of the parameter value.
    /// </summary>
    public QueryParameterType Type { get; }

    /// <summary>
    /// Gets the expected value pattern (regex). Null means any value is accepted.
    /// </summary>
    public string? ValuePattern { get; }

    internal QueryParameterExpectation(string name, bool isRequired, QueryParameterType type, string? valuePattern)
    {
        Name = name;
        IsRequired = isRequired;
        Type = type;
        ValuePattern = valuePattern;
    }

    /// <summary>
    /// Creates a required query parameter expectation.
    /// </summary>
    public static QueryParameterExpectation Required(string name, QueryParameterType type = QueryParameterType.String)
        => new(name, true, type, null);

    /// <summary>
    /// Creates an optional query parameter expectation.
    /// </summary>
    public static QueryParameterExpectation Optional(string name, QueryParameterType type = QueryParameterType.String)
        => new(name, false, type, null);
}

/// <summary>
/// The type of a query parameter value.
/// </summary>
public enum QueryParameterType
{
    /// <summary>String value.</summary>
    String,
    /// <summary>Integer value.</summary>
    Integer,
    /// <summary>Number (decimal) value.</summary>
    Number,
    /// <summary>Boolean value.</summary>
    Boolean,
    /// <summary>Array of values.</summary>
    Array
}
