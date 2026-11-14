using Microsoft.Extensions.Logging;
using ModularPipelines;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Options;

namespace Treaty.Pipeline.Modules;

public class RunTUnitTestsModule : Module<CommandResult>
{
    protected override async Task<CommandResult> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var rootDirectory = (await context.Tools.Git.Information.GetRequiredInfoAsync(cancellationToken)).Root;

        // First, build the solution in Release mode
        await context.Tools.DotNet.BuildAsync(new DotNetBuildOptions
        {
            ProjectSolution = rootDirectory.GetFile("Treaty.sln").Path,
            Configuration = "Release"
        }, cancellationToken: cancellationToken);

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
        return await context.Shell.RunAsync(
            new CommandLineToolOptions(testExePath),
            new CommandExecutionOptions
            {
                Logging = new CommandLoggingOptions
                {
                    Verbosity = CommandLogVerbosity.InputOnly,
                    ShowStandardError = true
                }
            },
            cancellationToken);
    }
}
