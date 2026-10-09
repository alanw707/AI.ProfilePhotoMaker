using System.Reflection;
using FluentAssertions;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Performance;

/// <summary>
/// Memory and timing measurements are process-wide, so a performance class running beside other
/// test collections measures their allocations too (the flaky "memory reduction" failures).
/// Every collection a performance class joins must be defined with DisableParallelization.
/// </summary>
public class PerformanceIsolationTests
{
    [Fact]
    public void EveryPerformanceCollectionRunsAlone()
    {
        var types = typeof(PerformanceIsolationTests).Assembly.GetTypes();
        static string? NameOf<TAttr>(Type t) => t.GetCustomAttributesData()
            .FirstOrDefault(d => d.AttributeType == typeof(TAttr))?.ConstructorArguments.FirstOrDefault().Value as string;

        var definitions = types
            .Where(t => NameOf<CollectionDefinitionAttribute>(t) != null)
            .ToDictionary(t => NameOf<CollectionDefinitionAttribute>(t)!,
                t => t.GetCustomAttribute<CollectionDefinitionAttribute>()!.DisableParallelization);

        var used = types
            .Where(t => t.Namespace == typeof(PerformanceIsolationTests).Namespace)
            .Select(NameOf<CollectionAttribute>)
            .OfType<string>()
            .Distinct()
            .ToList();

        used.Should().NotBeEmpty();
        foreach (var name in used)
            definitions.Should().ContainKey(name).WhoseValue.Should().BeTrue($"collection '{name}' measures process-wide memory/time");
    }
}
