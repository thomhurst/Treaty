using ModularPipelines;
using ModularPipelines.Context;

namespace Treaty.Pipeline.Modules;

public class PackageFilesRemovalModule : Module
{
    protected override async Task ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var rootDirectory = (await context.Tools.Git.Information.GetRequiredInfoAsync(cancellationToken)).Root;
        var packageFiles = rootDirectory.GetFiles(path => path.Extension is ".nupkg" or ".snupkg");

        foreach (var packageFile in packageFiles)
        {
            packageFile.Delete();
        }
    }
}
