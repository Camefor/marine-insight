using System.Runtime.Versioning;

namespace MarineInsight.Infrastructure.Tests;

public sealed class TestAssemblySmokeTests
{
    [Fact]
    public void TestAssemblyTargetsDotNetTen()
    {
        var targetFramework = typeof(TestAssemblySmokeTests)
            .Assembly
            .GetCustomAttributes(inherit: false)
            .OfType<TargetFrameworkAttribute>()
            .Single()
            .FrameworkName;

        Assert.Equal(".NETCoreApp,Version=v10.0", targetFramework);
    }
}
