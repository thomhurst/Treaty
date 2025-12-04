namespace Treaty.Contracts;

/// <summary>
/// Represents a provider state that must be established before an interaction.
/// Provider states allow consumers to specify prerequisites for their tests,
/// such as "a user with id 123 exists" or "the product catalog is empty".
/// </summary>
public sealed class ProviderState
{
    /// <summary>
    /// Gets the name of the provider state.
    /// </summary>
    /// <example>
    /// "a user with id 123 exists"
    /// </example>
    public string Name { get; }

    /// <summary>
    /// Gets the parameters associated with this provider state.
    /// </summary>
    /// <example>
    /// { "id": 123, "name": "John Doe" }
    /// </example>
    public IReadOnlyDictionary<string, object> Parameters { get; }

    /// <summary>
    /// Creates a new provider state with the specified name and optional parameters.
    /// </summary>
    /// <param name="name">The state name.</param>
    /// <param name="parameters">Optional parameters for the state.</param>
    public ProviderState(string name, IReadOnlyDictionary<string, object>? parameters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Parameters = parameters ?? new Dictionary<string, object>();
    }

    /// <summary>
    /// Creates a provider state from a name and an anonymous object or dictionary containing parameters.
    /// </summary>
    /// <param name="name">The state name.</param>
    /// <param name="parameters">An anonymous object or dictionary containing parameters.</param>
    /// <returns>A new provider state.</returns>
    public static ProviderState Create(string name, object? parameters = null)
    {
        if (parameters == null)
            return new ProviderState(name);

        // Handle dictionary directly
        if (parameters is IReadOnlyDictionary<string, object> readOnlyDict)
            return new ProviderState(name, readOnlyDict);

        if (parameters is IDictionary<string, object> dict)
            return new ProviderState(name, new Dictionary<string, object>(dict, StringComparer.OrdinalIgnoreCase));

        // Handle anonymous objects via reflection
        var resultDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in parameters.GetType().GetProperties())
        {
            var value = prop.GetValue(parameters);
            if (value != null)
            {
                resultDict[prop.Name] = value;
            }
        }

        return new ProviderState(name, resultDict);
    }

    /// <summary>
    /// Gets a parameter value by name.
    /// </summary>
    /// <typeparam name="T">The expected type of the parameter.</typeparam>
    /// <param name="name">The parameter name.</param>
    /// <returns>The parameter value.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the parameter is not found.</exception>
    /// <exception cref="InvalidCastException">Thrown when the parameter cannot be cast to the expected type.</exception>
    public T GetParameter<T>(string name)
    {
        if (!Parameters.TryGetValue(name, out var value))
        {
            throw new KeyNotFoundException(
                $"Provider state '{Name}' does not have a parameter named '{name}'. " +
                $"Available parameters: {string.Join(", ", Parameters.Keys)}");
        }

        return (T)Convert.ChangeType(value, typeof(T));
    }

    /// <summary>
    /// Tries to get a parameter value by name.
    /// </summary>
    /// <typeparam name="T">The expected type of the parameter.</typeparam>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The parameter value if found.</param>
    /// <returns>True if the parameter was found and converted successfully.</returns>
    public bool TryGetParameter<T>(string name, out T? value)
    {
        value = default;
        if (!Parameters.TryGetValue(name, out var rawValue))
            return false;

        try
        {
            value = (T)Convert.ChangeType(rawValue, typeof(T));
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public override string ToString() => Parameters.Count > 0
        ? $"{Name} [{string.Join(", ", Parameters.Select(p => $"{p.Key}={p.Value}"))}]"
        : Name;
}
