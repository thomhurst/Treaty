using Treaty.Matching.Matchers;

namespace Treaty.Matching;

/// <summary>
/// Static factory for creating flexible matchers used in contract validation.
/// Matchers allow specifying value patterns instead of exact values.
/// </summary>
/// <example>
/// <code>
/// var contract = Treaty.DefineContract()
///     .ForEndpoint("/users/{id}")
///         .WithMethod(HttpMethod.Get)
///         .ExpectingResponse(r => r
///             .WithStatus(200)
///             .WithJsonBody(new {
///                 id = Match.Guid(),
///                 name = Match.NonEmptyString(),
///                 email = Match.Email(),
///                 status = Match.OneOf("active", "inactive"),
///                 createdAt = Match.DateTime()
///             }))
///     .Build();
/// </code>
/// </example>
public static class Match
{
    /// <summary>
    /// Matches any valid GUID/UUID string.
    /// </summary>
    /// <returns>A matcher that validates GUID format.</returns>
    public static IMatcher Guid() => new GuidMatcher();

    /// <summary>
    /// Matches any string value (including empty).
    /// </summary>
    /// <returns>A matcher that accepts any string.</returns>
    public static IMatcher String() => new StringMatcher();

    /// <summary>
    /// Matches any non-null, non-empty string.
    /// </summary>
    /// <returns>A matcher that requires a non-empty string.</returns>
    public static IMatcher NonEmptyString() => new NonEmptyStringMatcher();

    /// <summary>
    /// Matches a valid email address format.
    /// </summary>
    /// <returns>A matcher that validates email format.</returns>
    public static IMatcher Email() => new EmailMatcher();

    /// <summary>
    /// Matches a valid absolute URI.
    /// </summary>
    /// <returns>A matcher that validates URI format.</returns>
    public static IMatcher Uri() => new UriMatcher();

    /// <summary>
    /// Matches a string against a regex pattern.
    /// </summary>
    /// <param name="pattern">The regex pattern to match against.</param>
    /// <returns>A matcher that validates against the pattern.</returns>
    public static IMatcher Regex(string pattern) => new RegexMatcher(pattern);

    /// <summary>
    /// Matches any value of the specified CLR type.
    /// </summary>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <returns>A matcher that validates type compatibility.</returns>
    public static IMatcher Type<T>() => new TypeMatcher(typeof(T));

    /// <summary>
    /// Matches any value of the same type as the example.
    /// </summary>
    /// <param name="example">An example value whose type will be matched.</param>
    /// <returns>A matcher that validates type compatibility.</returns>
    public static IMatcher Type(object example) => new TypeMatcher(example.GetType(), example);

    /// <summary>
    /// Matches an integer value, optionally within a range.
    /// </summary>
    /// <param name="min">Optional minimum value (inclusive).</param>
    /// <param name="max">Optional maximum value (inclusive).</param>
    /// <returns>A matcher that validates integer values.</returns>
    public static IMatcher Integer(long? min = null, long? max = null) => new IntegerMatcher(min, max);

    /// <summary>
    /// Matches a decimal/floating-point value, optionally within a range.
    /// </summary>
    /// <param name="min">Optional minimum value (inclusive).</param>
    /// <param name="max">Optional maximum value (inclusive).</param>
    /// <returns>A matcher that validates decimal values.</returns>
    public static IMatcher Decimal(decimal? min = null, decimal? max = null) => new DecimalMatcher(min, max);

    /// <summary>
    /// Matches any boolean value (true or false).
    /// </summary>
    /// <returns>A matcher that accepts any boolean.</returns>
    public static IMatcher Boolean() => new BooleanMatcher();

    /// <summary>
    /// Matches a valid ISO 8601 date-time string.
    /// </summary>
    /// <returns>A matcher that validates date-time format.</returns>
    public static IMatcher DateTime() => new DateTimeMatcher();

    /// <summary>
    /// Matches a valid ISO 8601 date string (date only, no time).
    /// </summary>
    /// <returns>A matcher that validates date format.</returns>
    public static IMatcher DateOnly() => new DateOnlyMatcher();

    /// <summary>
    /// Matches a valid ISO 8601 time string (time only, no date).
    /// </summary>
    /// <returns>A matcher that validates time format.</returns>
    public static IMatcher TimeOnly() => new TimeOnlyMatcher();

    /// <summary>
    /// Matches an array where each item matches the example structure.
    /// </summary>
    /// <param name="example">An example item that each array element should match.</param>
    /// <param name="minCount">Minimum number of items required (default 1).</param>
    /// <returns>A matcher that validates array items.</returns>
    public static IMatcher EachLike(object example, int minCount = 1) => new EachLikeMatcher(example, minCount);

    /// <summary>
    /// Matches any value (always passes validation).
    /// Use this for properties you don't care about validating.
    /// </summary>
    /// <returns>A matcher that accepts any value.</returns>
    public static IMatcher Any() => new AnyMatcher();

    /// <summary>
    /// Matches a nested object with its own matcher schema.
    /// </summary>
    /// <param name="schema">An anonymous object with matchers defining the nested structure.</param>
    /// <returns>A matcher that validates nested objects.</returns>
    public static IMatcher Object(object schema) => new ObjectMatcher(schema);

    /// <summary>
    /// Matches only null values.
    /// </summary>
    /// <returns>A matcher that requires null.</returns>
    public static IMatcher Null() => new NullMatcher();

    /// <summary>
    /// Matches one of a set of specified values.
    /// </summary>
    /// <param name="values">The allowed values.</param>
    /// <returns>A matcher that validates against the set of values.</returns>
    public static IMatcher OneOf(params object[] values) => new OneOfMatcher(values);
}
