using ThixTunnel.Core.Business;
using ThixTunnel.Core.Infrastructure;

namespace ThixTunnel.Tests;

/// <summary>Every test gets an empty data folder of its own and no machine policies.</summary>
public sealed class TestData : IDisposable
{
    public TestData()
    {
        Directory = Path.Combine(Path.GetTempPath(), "thixtunnel-tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Directory);
        AppPaths.UseDirectoryForTests(Directory);
        AccessPolicy.UsePolicyReaderForTests(_ => false);
        DataStore.Reset();
    }

    public string Directory { get; }

    public void Dispose()
    {
        DataStore.Reset();
        AccessPolicy.UsePolicyReaderForTests(null);
        AppPaths.UseDirectoryForTests(null);
        try
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

/// <summary>Tests that share static state (data folder, policies) must not run in parallel.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SharedStateCollection
{
    public const string Name = "shared state";
}
