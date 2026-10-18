using System.Xml.Linq;

namespace DownKyi.Architecture.Tests;

public sealed class PagerArchitectureTests
{
    [Fact]
    public void PagerArrowButtonsBindToNavigationCommands()
    {
        var repositoryRoot = FindRepositoryRoot();
        var document = XDocument.Load(Path.Combine(
            repositoryRoot,
            "src", "DownKyi.Desktop", "CustomControl", "CustomPager.axaml"));
        XNamespace avalonia = "https://github.com/avaloniaui";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        string? CommandFor(string buttonName) => document
            .Descendants(avalonia + "Button")
            .Single(button => (string?)button.Attribute(xaml + "Name") == buttonName)
            .Attribute("Command")?.Value;

        Assert.Equal("{Binding PreviousCommand}", CommandFor("NamePrevious"));
        Assert.Equal("{Binding NextCommand}", CommandFor("NameNext"));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DownKyi.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the DownKyi repository root.");
    }
}
