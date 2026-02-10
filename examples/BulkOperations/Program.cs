using AnubisWorks.SQLFactory;
using AnubisWorks.SQLFactory.BulkOperations;
using Microsoft.Data.Sqlite;
using System.Diagnostics;

namespace SQLFactory.Examples.BulkOperations;

/// <summary>
/// Example: Bulk Operations for high-performance data manipulation
/// Demonstrates: BulkInsert(), BulkUpdate(), BulkDelete(), performance benchmarks
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== SQLFactory Bulk Operations Example ===\n");

        var db = CreateDatabase();

        Console.WriteLine("1. BULK INSERT");
        BulkInsertExample(db);

        Console.WriteLine("\n2. BULK UPDATE");
        BulkUpdateExample(db);

        Console.WriteLine("\n3. BULK DELETE");
        BulkDeleteExample(db);

        Console.WriteLine("\n4. PERFORMANCE BENCHMARKS");
        PerformanceBenchmarksExample(db);

        Console.WriteLine("\n5. BEST PRACTICES");
        BestPracticesExample();

        Console.WriteLine("\n=== Example completed successfully! ===");
    }

    static Database CreateDatabase()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE Products (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Price REAL NOT NULL,
                Stock INTEGER NOT NULL,
                CategoryId INTEGER NOT NULL
            );
        ";
        cmd.ExecuteNonQuery();

        Console.WriteLine("   ✓ Database created\n");
        return new Database(conn);
    }

    static void BulkInsertExample(Database db)
    {
        Console.WriteLine("   Bulk insert operations:");

        // Generate test data
        var products = Enumerable.Range(1, 1000)
            .Select(i => new Product
            {
                Name = $"Product {i}",
                Price = 10 + (i % 100),
                Stock = 50 + (i % 20),
                CategoryId = (i % 5) + 1
            })
            .ToList();

        Console.WriteLine($"   Generated {products.Count} products");

        // Bulk insert
        var sw = Stopwatch.StartNew();
        db.BulkInsert(products);
        sw.Stop();

        Console.WriteLine($"   ✓ BulkInsert(): {products.Count} records in {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"   ✓ Throughput: {products.Count / (sw.ElapsedMilliseconds / 1000.0):F0} records/sec");

        // Verify
        var count = db.ExecuteScalar<int>("SELECT COUNT(*) FROM Products");
        Console.WriteLine($"   ✓ Verification: {count} records in database");
    }

    static void BulkUpdateExample(Database db)
    {
        Console.WriteLine("   Bulk update operations:");

        // Load products to update
        var products = db.Query<Product>("SELECT * FROM Products WHERE CategoryId = @0 LIMIT 500", 1)
                         .ToList();
        Console.WriteLine($"   Loaded {products.Count} products");

        // Modify in memory
        foreach (var product in products)
        {
            product.Price *= 1.15m; // 15% price increase
            product.Stock += 10;     // Restock
        }

        // Bulk update
        var sw = Stopwatch.StartNew();
        db.BulkUpdate(products);
        sw.Stop();

        Console.WriteLine($"   ✓ BulkUpdate(): {products.Count} records in {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"   ✓ Throughput: {products.Count / (sw.ElapsedMilliseconds / 1000.0):F0} records/sec");

        // Verify
        var updated = db.Single<Product>("SELECT * FROM Products WHERE Id = @0", products[0].Id);
        Console.WriteLine($"   ✓ Verification: {updated.Name} (${updated.Price}, Stock: {updated.Stock})");
    }

    static void BulkDeleteExample(Database db)
    {
        Console.WriteLine("   Bulk delete operations:");

        // Select products to delete
        var toDelete = db.Query<Product>("SELECT * FROM Products WHERE CategoryId = @0", 3)
                         .ToList();
        Console.WriteLine($"   Selected {toDelete.Count} products for deletion");

        // Bulk delete
        var sw = Stopwatch.StartNew();
        db.BulkDelete(toDelete);
        sw.Stop();

        Console.WriteLine($"   ✓ BulkDelete(): {toDelete.Count} records in {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"   ✓ Throughput: {toDelete.Count / (sw.ElapsedMilliseconds / 1000.0):F0} records/sec");

        // Verify
        var remaining = db.ExecuteScalar<int>("SELECT COUNT(*) FROM Products");
        Console.WriteLine($"   ✓ Verification: {remaining} records remaining");
    }

    static void PerformanceBenchmarksExample(Database db)
    {
        Console.WriteLine("   Performance comparison: Single vs Bulk operations");

        const int recordCount = 1000;
        
        // Reset database
        db.Execute("DELETE FROM Products");

        // SINGLE INSERT
        var singleInsertData = Enumerable.Range(1, recordCount)
            .Select(i => new Product
            {
                Name = $"Product {i}",
                Price = 10 + i,
                Stock = 50,
                CategoryId = 1
            })
            .ToList();

        var sw = Stopwatch.StartNew();
        foreach (var product in singleInsertData)
        {
            db.Insert(product);
        }
        sw.Stop();
        var singleInsertMs = sw.ElapsedMilliseconds;
        Console.WriteLine($"\n   Single INSERT: {recordCount} records in {singleInsertMs}ms");

        // Reset
        db.Execute("DELETE FROM Products");

        // BULK INSERT
        var bulkInsertData = Enumerable.Range(1, recordCount)
            .Select(i => new Product
            {
                Name = $"Product {i}",
                Price = 10 + i,
                Stock = 50,
                CategoryId = 1
            })
            .ToList();

        sw.Restart();
        db.BulkInsert(bulkInsertData);
        sw.Stop();
        var bulkInsertMs = sw.ElapsedMilliseconds;
        Console.WriteLine($"   Bulk INSERT: {recordCount} records in {bulkInsertMs}ms");

        // COMPARISON
        var speedup = singleInsertMs / (double)bulkInsertMs;
        Console.WriteLine($"\n   ✓ Bulk is {speedup:F1}x faster");
        Console.WriteLine($"   ✓ Time saved: {singleInsertMs - bulkInsertMs}ms");
        Console.WriteLine($"   ✓ Recommendation: Use bulk for {recordCount}+ records");
    }

    static void BestPracticesExample()
    {
        Console.WriteLine("   Best practices:");

        Console.WriteLine("\n   1. BATCH SIZE");
        Console.WriteLine("     • Optimal: 1,000 - 10,000 records per batch");
        Console.WriteLine("     • Too small: Overhead dominates");
        Console.WriteLine("     • Too large: Memory pressure, transaction locks");

        Console.WriteLine("\n   2. TRANSACTION MANAGEMENT");
        Console.WriteLine("     • Bulk operations use single transaction");
        Console.WriteLine("     • Rollback on error maintains consistency");
        Console.WriteLine("     • Consider chunking for very large datasets");

        Console.WriteLine("\n   3. PERFORMANCE TUNING");
        Console.WriteLine("     • Disable indexes before bulk insert (rebuild after)");
        Console.WriteLine("     • Use BulkInsert() for initial data load");
        Console.WriteLine("     • Use BulkUpdate() for batch processing");
        Console.WriteLine("     • Monitor database locks and timeouts");

        Console.WriteLine("\n   4. WHEN TO USE");
        Console.WriteLine("     • Data imports/exports");
        Console.WriteLine("     • ETL pipelines");
        Console.WriteLine("     • Batch processing jobs");
        Console.WriteLine("     • Database migrations");
        Console.WriteLine("     • Report generation with staging tables");

        Console.WriteLine("\n   5. EXAMPLE: Chunked Processing");
        Console.WriteLine(@"
            const int chunkSize = 5000;
            for (int i = 0; i < largeDataset.Count; i += chunkSize)
            {
                var chunk = largeDataset.Skip(i).Take(chunkSize).ToList();
                db.BulkInsert(chunk);
                Console.WriteLine($""Processed {i + chunk.Count} records"");
            }
        ");
    }
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public int CategoryId { get; set; }
}
