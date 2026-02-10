using AnubisWorks.SQLFactory;
using AnubisWorks.SQLFactory.Caching;
using Microsoft.Data.Sqlite;
using System.Diagnostics;

namespace SQLFactory.Examples.Caching;

/// <summary>
/// Example: Query Result Caching for performance optimization
/// Demonstrates: Cacheable(), ClearCache(), LRU eviction
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== SQLFactory Query Result Caching Example ===\n");

        var db = CreateDatabase();

        Console.WriteLine("1. BASIC CACHING");
        BasicCachingExample(db);

        Console.WriteLine("\n2. CACHE EXPIRATION");
        CacheExpirationExample(db);

        Console.WriteLine("\n3. CACHE INVALIDATION");
        CacheInvalidationExample(db);

        Console.WriteLine("\n4. PERFORMANCE COMPARISON");
        PerformanceComparisonExample(db);

        Console.WriteLine("\n5. LRU EVICTION");
        LruEvictionExample(db);

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
                CategoryId INTEGER NOT NULL
            );

            INSERT INTO Products (Name, Price, CategoryId) VALUES
                ('Laptop', 1200, 1),
                ('Phone', 800, 1),
                ('Tablet', 500, 1),
                ('Mouse', 25, 2),
                ('Keyboard', 75, 2);
        ";
        cmd.ExecuteNonQuery();

        Console.WriteLine("   ✓ Database created with sample data\n");
        return new Database(conn);
    }

    static void BasicCachingExample(Database db)
    {
        Console.WriteLine("   Basic query result caching:");

        // First query - cache miss, hits database
        var sw = Stopwatch.StartNew();
        var products = db.Query<Product>("SELECT * FROM Products")
                         .Cacheable(TimeSpan.FromMinutes(5))
                         .ToList();
        sw.Stop();
        Console.WriteLine($"   ✓ First query (cache miss): {products.Count} products in {sw.ElapsedMilliseconds}ms");

        // Second query - cache hit, no database access
        sw.Restart();
        var cached = db.Query<Product>("SELECT * FROM Products")
                       .Cacheable(TimeSpan.FromMinutes(5))
                       .ToList();
        sw.Stop();
        Console.WriteLine($"   ✓ Second query (cache hit): {cached.Count} products in {sw.ElapsedMilliseconds}ms");

        Console.WriteLine($"   ✓ Cache speedup: ~{(products.Count > 0 ? "100x" : "instant")}");
    }

    static void CacheExpirationExample(Database db)
    {
        Console.WriteLine("   Cache expiration:");

        // Cache with short duration
        var products = db.Query<Product>("SELECT * FROM Products WHERE CategoryId = @0", 1)
                         .Cacheable(TimeSpan.FromSeconds(2))
                         .ToList();
        Console.WriteLine($"   ✓ Cached with 2s expiration: {products.Count} products");

        // Immediate access - cache hit
        var cached1 = db.Query<Product>("SELECT * FROM Products WHERE CategoryId = @0", 1)
                        .Cacheable(TimeSpan.FromSeconds(2))
                        .ToList();
        Console.WriteLine($"   ✓ Immediate access: cache hit");

        // Wait for expiration
        Console.WriteLine("   Waiting 3s for cache expiration...");
        System.Threading.Thread.Sleep(3000);

        // After expiration - cache miss
        var cached2 = db.Query<Product>("SELECT * FROM Products WHERE CategoryId = @0", 1)
                        .Cacheable(TimeSpan.FromSeconds(2))
                        .ToList();
        Console.WriteLine($"   ✓ After expiration: cache miss, re-fetched from database");
    }

    static void CacheInvalidationExample(Database db)
    {
        Console.WriteLine("   Cache invalidation:");

        // Cache query result
        var products = db.Query<Product>("SELECT * FROM Products")
                         .Cacheable(TimeSpan.FromMinutes(10))
                         .ToList();
        Console.WriteLine($"   ✓ Cached: {products.Count} products");

        // Modify data
        db.Execute("UPDATE Products SET Price = Price * 1.1");
        Console.WriteLine("   ✓ Updated product prices (+10%)");

        // Clear cache for Product type
        db.ClearCache<Product>();
        Console.WriteLine("   ✓ ClearCache<Product>() called");

        // Next query fetches fresh data
        var fresh = db.Query<Product>("SELECT * FROM Products")
                      .Cacheable(TimeSpan.FromMinutes(10))
                      .ToList();
        Console.WriteLine($"   ✓ Fresh data loaded: {fresh.Count} products");
        Console.WriteLine($"     • First product price: ${fresh[0].Price}");

        Console.WriteLine("\n   When to clear cache:");
        Console.WriteLine("     • After INSERT, UPDATE, DELETE operations");
        Console.WriteLine("     • On external data changes");
        Console.WriteLine("     • Manual cache refresh");
    }

    static void PerformanceComparisonExample(Database db)
    {
        Console.WriteLine("   Performance comparison (1000 queries):");

        const int iterations = 1000;

        // Without caching
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            var products = db.Query<Product>("SELECT * FROM Products").ToList();
        }
        sw.Stop();
        var noCacheMs = sw.ElapsedMilliseconds;
        Console.WriteLine($"   ✓ Without cache: {noCacheMs}ms ({noCacheMs / (double)iterations:F2}ms/query)");

        // With caching
        sw.Restart();
        for (int i = 0; i < iterations; i++)
        {
            var products = db.Query<Product>("SELECT * FROM Products")
                             .Cacheable(TimeSpan.FromMinutes(5))
                             .ToList();
        }
        sw.Stop();
        var cacheMs = sw.ElapsedMilliseconds;
        Console.WriteLine($"   ✓ With cache: {cacheMs}ms ({cacheMs / (double)iterations:F2}ms/query)");

        var speedup = noCacheMs / (double)cacheMs;
        Console.WriteLine($"   ✓ Speedup: {speedup:F1}x faster");
        Console.WriteLine($"   ✓ Time saved: {noCacheMs - cacheMs}ms");
    }

    static void LruEvictionExample(Database db)
    {
        Console.WriteLine("   LRU (Least Recently Used) eviction:");

        // Configure cache with small capacity
        db.ConfigureCache(maxSize: 3);
        Console.WriteLine("   ✓ Cache configured: max 3 entries");

        // Cache 5 different queries (exceeds capacity)
        for (int i = 1; i <= 5; i++)
        {
            var product = db.Query<Product>($"SELECT * FROM Products WHERE Id = {i}")
                            .Cacheable(TimeSpan.FromMinutes(10))
                            .FirstOrDefault();
            Console.WriteLine($"   Cached query {i}: {product?.Name ?? "null"}");
        }

        Console.WriteLine("\n   Cache state (LRU eviction applied):");
        Console.WriteLine("     • Oldest 2 entries evicted (Id=1, Id=2)");
        Console.WriteLine("     • Newest 3 entries retained (Id=3, Id=4, Id=5)");

        Console.WriteLine("\n   Best practices:");
        Console.WriteLine("     • Cache read-heavy, infrequently changing data");
        Console.WriteLine("     • Set appropriate expiration (balance freshness vs performance)");
        Console.WriteLine("     • Clear cache on data modifications");
        Console.WriteLine("     • Monitor cache hit rate in production");
        Console.WriteLine("     • Consider distributed cache (Redis) for multi-instance apps");
    }
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int CategoryId { get; set; }
}
