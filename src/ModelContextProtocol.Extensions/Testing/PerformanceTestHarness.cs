using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ModelContextProtocol.Extensions.Testing
{
    /// <summary>
    /// Performance testing utilities for MCP components
    /// </summary>
    public static class PerformanceTestHarness
    {
        /// <summary>
        /// Measures the performance of an operation
        /// </summary>
        public static async Task<PerformanceResult> MeasureAsync<T>(
            Func<Task<T>> operation,
            int iterations = 100,
            int warmupIterations = 10)
        {
            // Warmup phase
            for (int i = 0; i < warmupIterations; i++)
            {
                await operation();
            }

            // Force garbage collection before measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var results = new List<TimeSpan>();
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < iterations; i++)
            {
                var iterationSw = Stopwatch.StartNew();
                await operation();
                iterationSw.Stop();
                results.Add(iterationSw.Elapsed);
            }

            sw.Stop();

            return new PerformanceResult
            {
                TotalTime = sw.Elapsed,
                Iterations = iterations,
                AverageTime = TimeSpan.FromTicks(results.Sum(r => r.Ticks) / results.Count),
                MedianTime = results.OrderBy(r => r.Ticks).Skip(results.Count / 2).First(),
                MinTime = results.Min(),
                MaxTime = results.Max(),
                StandardDeviation = CalculateStandardDeviation(results),
                Percentile95 = results.OrderBy(r => r.Ticks).Skip((int)(results.Count * 0.95)).First(),
                Percentile99 = results.OrderBy(r => r.Ticks).Skip((int)(results.Count * 0.99)).First()
            };
        }

        /// <summary>
        /// Measures the performance of a synchronous operation
        /// </summary>
        public static PerformanceResult Measure<T>(
            Func<T> operation,
            int iterations = 100,
            int warmupIterations = 10)
        {
            // Warmup phase
            for (int i = 0; i < warmupIterations; i++)
            {
                operation();
            }

            // Force garbage collection before measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var results = new List<TimeSpan>();
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < iterations; i++)
            {
                var iterationSw = Stopwatch.StartNew();
                operation();
                iterationSw.Stop();
                results.Add(iterationSw.Elapsed);
            }

            sw.Stop();

            return new PerformanceResult
            {
                TotalTime = sw.Elapsed,
                Iterations = iterations,
                AverageTime = TimeSpan.FromTicks(results.Sum(r => r.Ticks) / results.Count),
                MedianTime = results.OrderBy(r => r.Ticks).Skip(results.Count / 2).First(),
                MinTime = results.Min(),
                MaxTime = results.Max(),
                StandardDeviation = CalculateStandardDeviation(results),
                Percentile95 = results.OrderBy(r => r.Ticks).Skip((int)(results.Count * 0.95)).First(),
                Percentile99 = results.OrderBy(r => r.Ticks).Skip((int)(results.Count * 0.99)).First()
            };
        }

        /// <summary>
        /// Measures throughput of an operation
        /// </summary>
        public static async Task<ThroughputResult> MeasureThroughputAsync<T>(
            Func<Task<T>> operation,
            TimeSpan duration,
            int maxConcurrency = 1)
        {
            var stopwatch = Stopwatch.StartNew();
            var completedOperations = 0;
            var errors = 0;
            var tasks = new List<Task>();

            for (int i = 0; i < maxConcurrency; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    while (stopwatch.Elapsed < duration)
                    {
                        try
                        {
                            await operation();
                            Interlocked.Increment(ref completedOperations);
                        }
                        catch
                        {
                            Interlocked.Increment(ref errors);
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            return new ThroughputResult
            {
                Duration = stopwatch.Elapsed,
                CompletedOperations = completedOperations,
                Errors = errors,
                OperationsPerSecond = completedOperations / stopwatch.Elapsed.TotalSeconds,
                ErrorRate = errors / (double)(completedOperations + errors)
            };
        }

        /// <summary>
        /// Measures memory usage of an operation
        /// </summary>
        public static async Task<MemoryResult> MeasureMemoryAsync<T>(
            Func<Task<T>> operation,
            int iterations = 10)
        {
            // Force garbage collection before measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var initialMemory = GC.GetTotalMemory(false);
            var peakMemory = initialMemory;

            for (int i = 0; i < iterations; i++)
            {
                await operation();
                var currentMemory = GC.GetTotalMemory(false);
                if (currentMemory > peakMemory)
                    peakMemory = currentMemory;
            }

            // Force garbage collection after operations
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var finalMemory = GC.GetTotalMemory(false);

            return new MemoryResult
            {
                InitialMemory = initialMemory,
                PeakMemory = peakMemory,
                FinalMemory = finalMemory,
                MemoryIncrease = finalMemory - initialMemory,
                PeakIncrease = peakMemory - initialMemory
            };
        }

        private static TimeSpan CalculateStandardDeviation(List<TimeSpan> values)
        {
            var average = values.Sum(v => v.Ticks) / (double)values.Count;
            var sumOfSquares = values.Sum(v => Math.Pow(v.Ticks - average, 2));
            var variance = sumOfSquares / values.Count;
            return TimeSpan.FromTicks((long)Math.Sqrt(variance));
        }
    }

    /// <summary>
    /// Result of a performance measurement
    /// </summary>
    public record PerformanceResult
    {
        public TimeSpan TotalTime { get; init; }
        public int Iterations { get; init; }
        public TimeSpan AverageTime { get; init; }
        public TimeSpan MedianTime { get; init; }
        public TimeSpan MinTime { get; init; }
        public TimeSpan MaxTime { get; init; }
        public TimeSpan StandardDeviation { get; init; }
        public TimeSpan Percentile95 { get; init; }
        public TimeSpan Percentile99 { get; init; }

        public double OperationsPerSecond => Iterations / TotalTime.TotalSeconds;

        public override string ToString()
        {
            return $"Iterations: {Iterations}, " +
                   $"Average: {AverageTime.TotalMilliseconds:F2}ms, " +
                   $"Median: {MedianTime.TotalMilliseconds:F2}ms, " +
                   $"Min: {MinTime.TotalMilliseconds:F2}ms, " +
                   $"Max: {MaxTime.TotalMilliseconds:F2}ms, " +
                   $"95th: {Percentile95.TotalMilliseconds:F2}ms, " +
                   $"99th: {Percentile99.TotalMilliseconds:F2}ms, " +
                   $"Ops/sec: {OperationsPerSecond:F2}";
        }
    }

    /// <summary>
    /// Result of a throughput measurement
    /// </summary>
    public record ThroughputResult
    {
        public TimeSpan Duration { get; init; }
        public int CompletedOperations { get; init; }
        public int Errors { get; init; }
        public double OperationsPerSecond { get; init; }
        public double ErrorRate { get; init; }

        public override string ToString()
        {
            return $"Duration: {Duration.TotalSeconds:F2}s, " +
                   $"Operations: {CompletedOperations}, " +
                   $"Errors: {Errors}, " +
                   $"Ops/sec: {OperationsPerSecond:F2}, " +
                   $"Error Rate: {ErrorRate:P2}";
        }
    }

    /// <summary>
    /// Result of a memory measurement
    /// </summary>
    public record MemoryResult
    {
        public long InitialMemory { get; init; }
        public long PeakMemory { get; init; }
        public long FinalMemory { get; init; }
        public long MemoryIncrease { get; init; }
        public long PeakIncrease { get; init; }

        public string InitialMemoryFormatted => FormatBytes(InitialMemory);
        public string PeakMemoryFormatted => FormatBytes(PeakMemory);
        public string FinalMemoryFormatted => FormatBytes(FinalMemory);
        public string MemoryIncreaseFormatted => FormatBytes(MemoryIncrease);
        public string PeakIncreaseFormatted => FormatBytes(PeakIncrease);

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }

        public override string ToString()
        {
            return $"Initial: {InitialMemoryFormatted}, " +
                   $"Peak: {PeakMemoryFormatted}, " +
                   $"Final: {FinalMemoryFormatted}, " +
                   $"Increase: {MemoryIncreaseFormatted}, " +
                   $"Peak Increase: {PeakIncreaseFormatted}";
        }
    }
}
