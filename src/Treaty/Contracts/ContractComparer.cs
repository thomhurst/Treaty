namespace Treaty.Contracts;

/// <summary>
/// Compares two contracts to detect changes between versions.
/// </summary>
public static class ContractComparer
{
    /// <summary>
    /// Compares two contracts and returns a diff describing all changes.
    /// </summary>
    /// <param name="oldContract">The baseline (old) contract.</param>
    /// <param name="newContract">The new contract to compare.</param>
    /// <returns>A diff containing all detected changes.</returns>
    public static ContractDiff Compare(ContractDefinition oldContract, ContractDefinition newContract)
    {
        ArgumentNullException.ThrowIfNull(oldContract);
        ArgumentNullException.ThrowIfNull(newContract);

        var changes = new List<ContractChange>();

        // Compare endpoints
        CompareEndpoints(oldContract, newContract, changes);

        return new ContractDiff(oldContract.Name, newContract.Name, changes);
    }

    private static void CompareEndpoints(ContractDefinition oldContract, ContractDefinition newContract, List<ContractChange> changes)
    {
        var oldEndpoints = oldContract.Endpoints.ToDictionary(
            e => NormalizeEndpointKey(e.PathTemplate, e.Method),
            e => e);

        var newEndpoints = newContract.Endpoints.ToDictionary(
            e => NormalizeEndpointKey(e.PathTemplate, e.Method),
            e => e);

        // Find removed endpoints (BREAKING)
        foreach (var (key, oldEndpoint) in oldEndpoints)
        {
            if (!newEndpoints.ContainsKey(key))
            {
                changes.Add(new ContractChange(
                    ChangeSeverity.Breaking,
                    ContractChangeType.EndpointRemoved,
                    $"Endpoint removed: {oldEndpoint}",
                    Path: oldEndpoint.PathTemplate,
                    Method: oldEndpoint.Method,
                    Location: ChangeLocation.Endpoint));
            }
        }

        // Find added endpoints (INFO)
        foreach (var (key, newEndpoint) in newEndpoints)
        {
            if (!oldEndpoints.ContainsKey(key))
            {
                changes.Add(new ContractChange(
                    ChangeSeverity.Info,
                    ContractChangeType.EndpointAdded,
                    $"Endpoint added: {newEndpoint}",
                    Path: newEndpoint.PathTemplate,
                    Method: newEndpoint.Method,
                    Location: ChangeLocation.Endpoint));
            }
        }

        // Compare matching endpoints
        foreach (var (key, oldEndpoint) in oldEndpoints)
        {
            if (newEndpoints.TryGetValue(key, out var newEndpoint))
            {
                CompareEndpointDetails(oldEndpoint, newEndpoint, changes);
            }
        }
    }

    private static void CompareEndpointDetails(EndpointContract oldEndpoint, EndpointContract newEndpoint, List<ContractChange> changes)
    {
        // Compare response expectations
        CompareResponseExpectations(oldEndpoint, newEndpoint, changes);

        // Compare request expectations
        CompareRequestExpectations(oldEndpoint, newEndpoint, changes);

        // Compare headers
        CompareHeaders(oldEndpoint, newEndpoint, changes);
    }

    private static void CompareResponseExpectations(
        EndpointContract oldEndpoint,
        EndpointContract newEndpoint,
        List<ContractChange> changes)
    {
        var oldResponses = oldEndpoint.ResponseExpectations.ToDictionary(r => r.StatusCode, r => r);
        var newResponses = newEndpoint.ResponseExpectations.ToDictionary(r => r.StatusCode, r => r);

        // Find removed status codes (BREAKING for success codes)
        foreach (var (statusCode, oldResponse) in oldResponses)
        {
            if (!newResponses.ContainsKey(statusCode))
            {
                var severity = statusCode >= 200 && statusCode < 300 ? ChangeSeverity.Breaking : ChangeSeverity.Warning;
                changes.Add(new ContractChange(
                    severity,
                    ContractChangeType.ResponseStatusCodeRemoved,
                    $"Response status code {statusCode} removed from {oldEndpoint}",
                    Path: oldEndpoint.PathTemplate,
                    Method: oldEndpoint.Method,
                    Location: ChangeLocation.StatusCode,
                    OldValue: statusCode.ToString()));
            }
        }

        // Find added status codes (INFO)
        foreach (var (statusCode, newResponse) in newResponses)
        {
            if (!oldResponses.ContainsKey(statusCode))
            {
                changes.Add(new ContractChange(
                    ChangeSeverity.Info,
                    ContractChangeType.ResponseStatusCodeAdded,
                    $"Response status code {statusCode} added to {newEndpoint}",
                    Path: newEndpoint.PathTemplate,
                    Method: newEndpoint.Method,
                    Location: ChangeLocation.StatusCode,
                    NewValue: statusCode.ToString()));
            }
        }

        // Compare matching response expectations
        foreach (var (statusCode, oldResponse) in oldResponses)
        {
            if (newResponses.TryGetValue(statusCode, out var newResponse))
            {
                CompareResponseBodies(oldEndpoint, oldResponse, newResponse, changes);
                CompareResponseHeaders(oldEndpoint, oldResponse, newResponse, changes);
            }
        }
    }

    private static void CompareResponseBodies(
        EndpointContract endpoint,
        ResponseExpectation oldResponse,
        ResponseExpectation newResponse,
        List<ContractChange> changes)
    {
        // Compare body validators if both have them
        if (oldResponse.BodyValidator == null && newResponse.BodyValidator == null)
            return;

        if (oldResponse.BodyValidator == null && newResponse.BodyValidator != null)
        {
            changes.Add(new ContractChange(
                ChangeSeverity.Info,
                ContractChangeType.ResponseFieldAdded,
                $"Response body schema added to {endpoint} (status {oldResponse.StatusCode})",
                Path: endpoint.PathTemplate,
                Method: endpoint.Method,
                Location: ChangeLocation.ResponseBody));
            return;
        }

        if (oldResponse.BodyValidator != null && newResponse.BodyValidator == null)
        {
            changes.Add(new ContractChange(
                ChangeSeverity.Warning,
                ContractChangeType.ResponseFieldRemoved,
                $"Response body schema removed from {endpoint} (status {oldResponse.StatusCode})",
                Path: endpoint.PathTemplate,
                Method: endpoint.Method,
                Location: ChangeLocation.ResponseBody));
            return;
        }

        // Both have validators - compare schemas by type if available
        var oldType = oldResponse.BodyValidator?.ExpectedType;
        var newType = newResponse.BodyValidator?.ExpectedType;

        if (oldType != null && newType != null && oldType != newType)
        {
            changes.Add(new ContractChange(
                ChangeSeverity.Breaking,
                ContractChangeType.ResponseFieldTypeChanged,
                $"Response body type changed from {oldType.Name} to {newType.Name} for {endpoint} (status {oldResponse.StatusCode})",
                Path: endpoint.PathTemplate,
                Method: endpoint.Method,
                Location: ChangeLocation.ResponseBody,
                OldValue: oldType.Name,
                NewValue: newType.Name));
        }
        else
        {
            // Fall back to schema type name comparison (for OpenAPI schemas)
            var oldSchemaType = oldResponse.BodyValidator?.SchemaTypeName;
            var newSchemaType = newResponse.BodyValidator?.SchemaTypeName;

            if (oldSchemaType != null && newSchemaType != null && oldSchemaType != newSchemaType)
            {
                changes.Add(new ContractChange(
                    ChangeSeverity.Breaking,
                    ContractChangeType.ResponseFieldTypeChanged,
                    $"Response body type changed from {oldSchemaType} to {newSchemaType} for {endpoint} (status {oldResponse.StatusCode})",
                    Path: endpoint.PathTemplate,
                    Method: endpoint.Method,
                    Location: ChangeLocation.ResponseBody,
                    OldValue: oldSchemaType,
                    NewValue: newSchemaType));
            }
        }
    }

    private static void CompareResponseHeaders(
        EndpointContract endpoint,
        ResponseExpectation oldResponse,
        ResponseExpectation newResponse,
        List<ContractChange> changes)
    {
        var oldHeaders = oldResponse.ExpectedHeaders;
        var newHeaders = newResponse.ExpectedHeaders;

        // Find removed headers (WARNING - clients might expect them)
        foreach (var (name, oldHeader) in oldHeaders)
        {
            if (!newHeaders.ContainsKey(name))
            {
                changes.Add(new ContractChange(
                    ChangeSeverity.Warning,
                    ContractChangeType.ResponseHeaderRemoved,
                    $"Response header '{name}' removed from {endpoint} (status {oldResponse.StatusCode})",
                    Path: endpoint.PathTemplate,
                    Method: endpoint.Method,
                    Location: ChangeLocation.ResponseHeader,
                    FieldName: name));
            }
        }

        // Find added headers (INFO)
        foreach (var (name, newHeader) in newHeaders)
        {
            if (!oldHeaders.ContainsKey(name))
            {
                changes.Add(new ContractChange(
                    ChangeSeverity.Info,
                    ContractChangeType.ResponseHeaderAdded,
                    $"Response header '{name}' added to {endpoint} (status {newResponse.StatusCode})",
                    Path: endpoint.PathTemplate,
                    Method: endpoint.Method,
                    Location: ChangeLocation.ResponseHeader,
                    FieldName: name));
            }
        }
    }

    private static void CompareRequestExpectations(
        EndpointContract oldEndpoint,
        EndpointContract newEndpoint,
        List<ContractChange> changes)
    {
        var oldRequest = oldEndpoint.RequestExpectation;
        var newRequest = newEndpoint.RequestExpectation;

        // Handle cases where request expectation is added or removed
        if (oldRequest == null && newRequest != null)
        {
            if (newRequest.IsRequired)
            {
                changes.Add(new ContractChange(
                    ChangeSeverity.Breaking,
                    ContractChangeType.RequestFieldAdded,
                    $"Required request body added to {newEndpoint}",
                    Path: newEndpoint.PathTemplate,
                    Method: newEndpoint.Method,
                    Location: ChangeLocation.RequestBody));
            }
            else
            {
                changes.Add(new ContractChange(
                    ChangeSeverity.Info,
                    ContractChangeType.RequestFieldAdded,
                    $"Optional request body added to {newEndpoint}",
                    Path: newEndpoint.PathTemplate,
                    Method: newEndpoint.Method,
                    Location: ChangeLocation.RequestBody));
            }
            return;
        }

        if (oldRequest != null && newRequest == null)
        {
            changes.Add(new ContractChange(
                ChangeSeverity.Info,
                ContractChangeType.RequestFieldRemoved,
                $"Request body removed from {oldEndpoint}",
                Path: oldEndpoint.PathTemplate,
                Method: oldEndpoint.Method,
                Location: ChangeLocation.RequestBody));
            return;
        }

        if (oldRequest == null || newRequest == null)
            return;

        // Compare request body types
        var oldType = oldRequest.BodyValidator?.ExpectedType;
        var newType = newRequest.BodyValidator?.ExpectedType;

        if (oldType != null && newType != null && oldType != newType)
        {
            changes.Add(new ContractChange(
                ChangeSeverity.Breaking,
                ContractChangeType.RequestFieldTypeChanged,
                $"Request body type changed from {oldType.Name} to {newType.Name} for {oldEndpoint}",
                Path: oldEndpoint.PathTemplate,
                Method: oldEndpoint.Method,
                Location: ChangeLocation.RequestBody,
                OldValue: oldType.Name,
                NewValue: newType.Name));
        }

        // Check if request became required
        if (!oldRequest.IsRequired && newRequest.IsRequired)
        {
            changes.Add(new ContractChange(
                ChangeSeverity.Breaking,
                ContractChangeType.RequestFieldMadeRequired,
                $"Request body became required for {newEndpoint}",
                Path: newEndpoint.PathTemplate,
                Method: newEndpoint.Method,
                Location: ChangeLocation.RequestBody));
        }
        else if (oldRequest.IsRequired && !newRequest.IsRequired)
        {
            changes.Add(new ContractChange(
                ChangeSeverity.Info,
                ContractChangeType.RequestFieldMadeOptional,
                $"Request body became optional for {newEndpoint}",
                Path: newEndpoint.PathTemplate,
                Method: newEndpoint.Method,
                Location: ChangeLocation.RequestBody));
        }
    }

    private static void CompareHeaders(
        EndpointContract oldEndpoint,
        EndpointContract newEndpoint,
        List<ContractChange> changes)
    {
        var oldHeaders = oldEndpoint.ExpectedHeaders;
        var newHeaders = newEndpoint.ExpectedHeaders;

        // Find removed required headers (INFO - server no longer requires)
        foreach (var (name, oldHeader) in oldHeaders)
        {
            if (!newHeaders.ContainsKey(name))
            {
                changes.Add(new ContractChange(
                    ChangeSeverity.Info,
                    ContractChangeType.RequestHeaderRemoved,
                    $"Request header '{name}' no longer required for {oldEndpoint}",
                    Path: oldEndpoint.PathTemplate,
                    Method: oldEndpoint.Method,
                    Location: ChangeLocation.RequestHeader,
                    FieldName: name));
            }
        }

        // Find added required headers (BREAKING if required)
        foreach (var (name, newHeader) in newHeaders)
        {
            if (!oldHeaders.ContainsKey(name))
            {
                var severity = newHeader.IsRequired ? ChangeSeverity.Breaking : ChangeSeverity.Info;
                changes.Add(new ContractChange(
                    severity,
                    ContractChangeType.RequestHeaderAdded,
                    $"Request header '{name}' {(newHeader.IsRequired ? "required" : "added")} for {newEndpoint}",
                    Path: newEndpoint.PathTemplate,
                    Method: newEndpoint.Method,
                    Location: ChangeLocation.RequestHeader,
                    FieldName: name));
            }
        }
    }

    private static string NormalizeEndpointKey(string pathTemplate, HttpMethod method)
    {
        // Normalize path parameters to a consistent format
        var normalizedPath = System.Text.RegularExpressions.Regex.Replace(
            pathTemplate,
            @"\{[^}]+\}",
            "{param}");

        return $"{method.Method.ToUpperInvariant()} {normalizedPath}";
    }
}
