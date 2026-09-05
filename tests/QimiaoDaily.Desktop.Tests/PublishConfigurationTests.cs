using Xunit;

namespace QimiaoDaily.Desktop.Tests;

public sealed class PublishConfigurationTests
{
    [Fact]
    public void WindowsSingleFilePublish_DoesNotEmitDebugSymbols()
    {
        var project = FindProjectFile();
        var xml = File.ReadAllText(project);
        var sharedProps = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(project)!)!)!, "Directory.Build.props"));

        Assert.Contains("<PublishSingleFile>true</PublishSingleFile>", xml, StringComparison.Ordinal);
        Assert.Contains("<DebugType>None</DebugType>", sharedProps, StringComparison.Ordinal);
        Assert.Contains("<DebugSymbols>false</DebugSymbols>", sharedProps, StringComparison.Ordinal);
    }

    private static string FindProjectFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "QimiaoDaily.Desktop", "QimiaoDaily.Desktop.csproj");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate the desktop project file.");
    }
}
