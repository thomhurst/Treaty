using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularPipelines;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Options;
using Treaty.Pipeline.Settings;

namespace Treaty.Pipeline.Modules;

[DependsOn<PackagePathsParserModule>]
public class UploadPackagesToNugetModule(IOptions<NuGetSettings> nuGetSettings) : Module<CommandResult[]>
{
    private readonly IOptions<NuGetSettings> _nuGetSettings = nuGetSettings;

    protected override void Configure(ModuleConfigurationBuilder module) => module
        .WithSkipWhen(async (context, cancellationToken) =>
        {
            var gitVersionInfo = await context.Tools.Git.Versioning.GetVersioningInformationAsync(cancellationToken);

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

            return SkipDecision.DoNotSkip;
        });

    protected override async Task OnBeforeExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var packagePaths = await context.GetModule<PackagePathsParserModule>();

        foreach (var packagePath in packagePaths.Value!)
        {
            context.Logger.LogInformation("[NuGet.org] Uploading {File}", packagePath);
        }

        await base.OnBeforeExecuteAsync(context, cancellationToken);
    }

    protected override async Task<CommandResult[]> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var packagePaths = await context.GetModule<PackagePathsParserModule>();
        var results = new List<CommandResult>();

        foreach (var file in packagePaths.Value!)
        {
            var result = await context.Tools.DotNet.NuGet.PushAsync(new DotNetNuGetPushOptions([file.Path])
            {
                Source = "https://api.nuget.org/v3/index.json",
                ApiKey = _nuGetSettings.Value.ApiKey
            }, cancellationToken: cancellationToken);

            results.Add(result);
        }

        return [.. results];
    }
}
