using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Performance;

// Performance tests read process-wide GC counters and wall-clock time, so they must not share the
// process with other running collections. DisableParallelization runs each of these alone, after
// the parallel collections finish (PerformanceIsolationTests enforces this for new collections).
[CollectionDefinition("Performance", DisableParallelization = true)] public class PerformanceCollection { }
[CollectionDefinition("PerformanceRunner", DisableParallelization = true)] public class PerformanceRunnerCollection { }
[CollectionDefinition("PerformanceReport", DisableParallelization = true)] public class PerformanceReportCollection { }
[CollectionDefinition("LoadTesting", DisableParallelization = true)] public class LoadTestingCollection { }
[CollectionDefinition("QuickBenchmark", DisableParallelization = true)] public class QuickBenchmarkCollection { }
