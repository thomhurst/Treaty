using Microsoft.Extensions.Logging;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Enums;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Models;
using ModularPipelines.Modules;
using ModularPipelines.Options;

namespace Treaty.Pipeline.Modules;

public class RunTUnitTestsModule : Module<CommandResult>
{
    protected override async Task<CommandResult?> ExecuteAsync(IPipelineContext context, CancellationToken cancellationToken)
    {
        var rootDirectory = context.Git().RootDirectory!;

        // First, build the solution in Release mode
        await context.DotNet().Build(new DotNetBuildOptions
        {
            ProjectSolution = rootDirectory.GetFile("Treaty.sln").Path,
            Configuration = Configuration.Release
        }, cancellationToken);

        // Find the test executable - on Linux it won't have .exe extension
        var testExeFolder = rootDirectory
            .GetFolder("tests")
            .GetFolder("Treaty.Tests")
            .GetFolder("bin")
            .GetFolder("Release")
            .GetFolder("net10.0");

        var testExePath = OperatingSystem.IsWindows()
            ? testExeFolder.GetFile("Treaty.Tests.exe").Path
            : testExeFolder.GetFile("Treaty.Tests").Path;

        context.Logger.LogInformation("Running TUnit tests from: {Path}", testExePath);

        // Run the TUnit test executable directly
        return await context.Command.ExecuteCommandLineTool(
            new CommandLineToolOptions(testExePath)
            {
                CommandLogging = CommandLogging.Input | CommandLogging.Error
            },
            cancellationToken);
    }
}
