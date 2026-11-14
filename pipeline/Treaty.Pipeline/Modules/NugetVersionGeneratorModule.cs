#pragma warning disable CS0162 // Unreachable code detected

using Microsoft.Extensions.Logging;
using ModularPipelines;
using ModularPipelines.Context;

namespace Treaty.Pipeline.Modules;

public class NugetVersionGeneratorModule : Module<string>
{
    protected override async Task<string> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var gitVersionInformation = await context.Tools.Git.Versioning.GetVersioningInformationAsync(cancellationToken);

        if (gitVersionInformation.BranchName == "main")
        {
            return gitVersionInformation.SemVer!;
        }

        return $"{gitVersionInformation.Major}.{gitVersionInformation.Minor}.{gitVersionInformation.Patch}-{gitVersionInformation.PreReleaseLabel}.{gitVersionInformation.CommitsSinceVersionSource}";
    }

    protected override async Task<ModuleResult<string>?> OnAfterExecuteAsync(IModuleContext context, ModuleResult<string> result, CancellationToken cancellationToken)
    {
        context.Logger.LogInformation("NuGet Version: {Version}", result.ValueOrDefault);
        return await base.OnAfterExecuteAsync(context, result, cancellationToken);
    }
}
