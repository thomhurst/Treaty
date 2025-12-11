using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Models;
using ModularPipelines.Modules;
using Treaty.Pipeline.Settings;

namespace Treaty.Pipeline.Modules;

[DependsOn<PackagePathsParserModule>]
public class UploadPackagesToNugetModule(IOptions<NuGetSettings> nuGetSettings) : Module<CommandResult[]>
{
    private readonly IOptions<NuGetSettings> _nuGetSettings = nuGetSettings;

    protected override async Task OnBeforeExecute(IPipelineContext context)
    {
        var packagePaths = await GetModule<PackagePathsParserModule>();

        foreach (var packagePath in packagePaths.Value!)
        {
            context.Logger.LogInformation("[NuGet.org] Uploading {File}", packagePath);
        }

        await base.OnBeforeExecute(context);
    }

    protected override async Task<SkipDecision> ShouldSkip(IPipelineContext context)
    {
        var gitVersionInfo = await context.Git().Versioning.GetGitVersioningInformation();

        if (gitVersionInfo.BranchName != "main")
        {
            context.Logger.LogInformation("Skipping NuGet publish - not on main branch (current: {Branch})", gitVersionInfo.BranchName);
            return SkipDecision.Skip("Not on main branch");
        }

        var publishPackages = Environment.GetEnvironmentVariable("PUBLISH_PACKAGES");
        if (!string.Equals(publishPackages, "true", StringComparison.OrdinalIgnoreCase))
        {
            context.Logger.LogInformation("Skipping NuGet publish - PUBLISH_PACKAGES is not set to true");
            return SkipDecision.Skip("PUBLISH_PACKAGES is not set to true");
        }

        return await base.ShouldSkip(context);
    }

    protected override async Task<CommandResult[]?> ExecuteAsync(IPipelineContext context, CancellationToken cancellationToken)
    {
        var packagePaths = await GetModule<PackagePathsParserModule>();
        var results = new List<CommandResult>();

        foreach (var file in packagePaths.Value!)
        {
            var result = await context.DotNet().Nuget.Push(new DotNetNugetPushOptions(file)
            {
                Source = "https://api.nuget.org/v3/index.json",
                ApiKey = _nuGetSettings.Value.ApiKey
            }, cancellationToken);

            results.Add(result);
        }

        return [.. results];
    }
}
