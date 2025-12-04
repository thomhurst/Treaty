namespace Treaty.Matching;

/// <summary>
/// Defines the types of matchers available for contract validation.
/// </summary>
public enum MatcherType
{
    /// <summary>Matches any valid GUID/UUID.</summary>
    Guid,

    /// <summary>Matches any string value.</summary>
    String,

    /// <summary>Matches any non-empty string.</summary>
    NonEmptyString,

    /// <summary>Matches valid email format.</summary>
    Email,

    /// <summary>Matches valid URI format.</summary>
    Uri,

    /// <summary>Matches string against a regex pattern.</summary>
    Regex,

    /// <summary>Matches by CLR type.</summary>
    Type,

    /// <summary>Matches integer values with optional range.</summary>
    Integer,

    /// <summary>Matches decimal values with optional range.</summary>
    Decimal,

    /// <summary>Matches boolean values.</summary>
    Boolean,

    /// <summary>Matches valid DateTime strings.</summary>
    DateTime,

    /// <summary>Matches valid DateOnly strings.</summary>
    DateOnly,

    /// <summary>Matches valid TimeOnly strings.</summary>
    TimeOnly,

    /// <summary>Matches arrays where each item matches the example.</summary>
    EachLike,

    /// <summary>Matches any value (always passes).</summary>
    Any,

    /// <summary>Matches nested objects.</summary>
    Object,

    /// <summary>Matches only null values.</summary>
    Null,

    /// <summary>Matches one of a set of specified values.</summary>
    OneOf
}
