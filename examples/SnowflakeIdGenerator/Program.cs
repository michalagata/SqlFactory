using System;
using System.Diagnostics;
using System.Linq;
using AnubisWorks.SQLFactory.DistributedId;

namespace SQLFactory.Examples.SnowflakeIdGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Snowflake ID Generator Example ===\n");

            // Example 1: Basic ID Generation
            BasicIdGeneration();

            // Example 2: Parse ID Metadata
            ParseIdMetadata();

            // Example 3: Performance Test
            PerformanceTest();

            // Example 4: Multi-Worker Scenario
            MultiWorkerScenario();

            // Example 5: Clock Drift Detection
            ClockDriftExample();

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        static void BasicIdGeneration()
        {
            Console.WriteLine("=== Example 1: Basic ID Generation ===");

            // Configure generator with datacenter ID 1, worker ID 5
            var config = new SnowflakeConfig(datacenterId: 1, workerId: 5);
            var generator = new SnowflakeIdGenerator(config);

            Console.WriteLine($"Configuration:");
            Console.WriteLine($"  Datacenter ID: {config.DatacenterId}");
            Console.WriteLine($"  Worker ID: {config.WorkerId}");
            Console.WriteLine($"  Epoch: {new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)}\n");

            // Generate 5 unique IDs
            Console.WriteLine("Generated IDs:");
            for (int i = 1; i <= 5; i++)
            {
                long id = generator.NextId();
                Console.WriteLine($"  ID {i}: {id}");
            }

            Console.WriteLine();
        }

        static void ParseIdMetadata()
        {
            Console.WriteLine("=== Example 2: Parse ID Metadata ===");

            var config = new SnowflakeConfig(datacenterId: 2, workerId: 10);
            var generator = new SnowflakeIdGenerator(config);

            // Generate ID
            long id = generator.NextId();
            Console.WriteLine($"Generated ID: {id}\n");

            // Parse metadata
            var (timestamp, datacenterId, workerId, sequence) = generator.ParseId(id);

            Console.WriteLine("Parsed Metadata:");
            Console.WriteLine($"  Timestamp: {timestamp:yyyy-MM-dd HH:mm:ss.fff} UTC");
            Console.WriteLine($"  Datacenter ID: {datacenterId}");
            Console.WriteLine($"  Worker ID: {workerId}");
            Console.WriteLine($"  Sequence: {sequence}");

            Console.WriteLine();
        }

        static void PerformanceTest()
        {
            Console.WriteLine("=== Example 3: Performance Test ===");

            var config = new SnowflakeConfig(datacenterId: 1, workerId: 1);
            var generator = new SnowflakeIdGenerator(config);

            const int count = 100000;
            var ids = new long[count];

            Console.WriteLine($"Generating {count:N0} IDs...");

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                ids[i] = generator.NextId();
            }
            sw.Stop();

            Console.WriteLine($"\nResults:");
            Console.WriteLine($"  Time: {sw.ElapsedMilliseconds:N0} ms");
            Console.WriteLine($"  Throughput: {count / sw.Elapsed.TotalSeconds:N0} IDs/second");

            // Verify uniqueness
            bool allUnique = ids.Length == ids.Distinct().Count();
            Console.WriteLine($"  All IDs unique: {(allUnique ? "✓" : "✗")}");

            // Verify monotonic increase
            bool monotonic = true;
            for (int i = 1; i < ids.Length; i++)
            {
                if (ids[i] <= ids[i - 1])
                {
                    monotonic = false;
                    break;
                }
            }
            Console.WriteLine($"  Monotonically increasing: {(monotonic ? "✓" : "✗")}");

            Console.WriteLine();
        }

        static void MultiWorkerScenario()
        {
            Console.WriteLine("=== Example 4: Multi-Worker Scenario ===");
            Console.WriteLine("Simulating distributed system with 3 workers\n");

            // Simulate 3 different servers/workers
            var generator1 = new SnowflakeIdGenerator(new SnowflakeConfig(1, 1));
            var generator2 = new SnowflakeIdGenerator(new SnowflakeConfig(1, 2));
            var generator3 = new SnowflakeIdGenerator(new SnowflakeConfig(1, 3));

            Console.WriteLine("Worker 1 (Orders Service):");
            for (int i = 0; i < 3; i++)
            {
                Console.WriteLine($"  Order ID: {generator1.NextId()}");
            }

            Console.WriteLine("\nWorker 2 (Payments Service):");
            for (int i = 0; i < 3; i++)
            {
                Console.WriteLine($"  Payment ID: {generator2.NextId()}");
            }

            Console.WriteLine("\nWorker 3 (Shipping Service):");
            for (int i = 0; i < 3; i++)
            {
                Console.WriteLine($"  Shipment ID: {generator3.NextId()}");
            }

            Console.WriteLine("\nAll IDs are globally unique and sortable!");
            Console.WriteLine();
        }

        static void ClockDriftExample()
        {
            Console.WriteLine("=== Example 5: Clock Drift Detection ===");

            var config = new SnowflakeConfig(datacenterId: 1, workerId: 1);
            var generator = new SnowflakeIdGenerator(config);

            Console.WriteLine("Normal operation:");
            for (int i = 0; i < 3; i++)
            {
                var id = generator.NextId();
                Console.WriteLine($"  Generated ID: {id}");
            }

            Console.WriteLine("\nIf system clock moves backward, ClockDriftException is thrown");
            Console.WriteLine("(protecting against duplicate IDs)");
            Console.WriteLine("\nBest practice: Use NTP to keep clocks synchronized\n");
        }
    }
}
