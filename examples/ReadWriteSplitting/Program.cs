using AnubisWorks.SQLFactory;
using AnubisWorks.SQLFactory.ReadWriteSplitting;
using Microsoft.Data.Sqlite;

namespace SQLFactory.Examples.ReadWriteSplitting;

/// <summary>
/// Example: Read/Write Splitting for horizontal database scaling
/// Demonstrates: Master-replica configuration, automatic query routing, load balancing
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== SQLFactory Read/Write Splitting Example ===\n");

        // Simulate primary (master) and replica databases
        var primaryConn = CreateDatabase("primary");
        var replica1Conn = CreateDatabase("replica1");
        var replica2Conn = CreateDatabase("replica2");

        // Configure Read/Write Splitting
        var config = new ReadWriteConfiguration
        {
            PrimaryConnectionString = "Data Source=:memory:",
            ReplicaConnectionStrings = new List<string>
            {
                "Data Source=:memory:",
                "Data Source=:memory:"
            },
            LoadBalancingStrategy = LoadBalancingStrategy.RoundRobin,
            EnableStickySessions = true,
            StickySessionWindow = TimeSpan.FromSeconds(30),
            MaxConnectionsPerPool = 10
        };

        var db = new Database(primaryConn);
        db.WithReadWriteSplitting(config);

        Console.WriteLine("1. AUTOMATIC QUERY ROUTING");
        AutomaticRoutingExamples(db);

        Console.WriteLine("\n2. EXPLICIT ROUTING HINTS");
        ExplicitRoutingExamples(db);

        Console.WriteLine("\n3. LOAD BALANCING STRATEGIES");
        LoadBalancingExamples();

        Console.WriteLine("\n4. STICKY SESSIONS");
        StickySessionExamples(db);

        Console.WriteLine("\n5. CONNECTION POOLING");
        ConnectionPoolingExample();

        Console.WriteLine("\n=== Example completed successfully! ===");
    }

    static SqliteConnection CreateDatabase(string name)
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
                UpdatedAt TEXT NOT NULL
            );

            INSERT INTO Products (Name, Price, Stock, UpdatedAt) VALUES
                ('Laptop', 1200, 10, datetime('now')),
                ('Phone', 800, 25, datetime('now')),
                ('Tablet', 500, 15, datetime('now'));
        ";
        cmd.ExecuteNonQuery();

        Console.WriteLine($"   ✓ Created {name} database");
        return conn;
    }

    static void AutomaticRoutingExamples(Database db)
    {
        Console.WriteLine("   Automatic query routing based on SQL analysis:");

        // SELECT queries → replicas
        var products = db.Sql<Product>("SELECT * FROM Products").ToList();
        Console.WriteLine($"   ✓ SELECT (→ replica): {products.Count} products");

        var product = db.Single<Product>("SELECT * FROM Products WHERE Id = @0", 1);
        Console.WriteLine($"   ✓ SELECT single (→ replica): {product.Name}");

        // INSERT, UPDATE, DELETE → primary
        var newProduct = new Product
        {
            Name = "Keyboard",
            Price = 99.99m,
            Stock = 50,
            UpdatedAt = DateTime.UtcNow
        };
        db.Insert(newProduct);
        Console.WriteLine($"   ✓ INSERT (→ primary): {newProduct.Name} (Id: {newProduct.Id})");

        product.Price = 1299.99m;
        db.Update(product);
        Console.WriteLine($"   ✓ UPDATE (→ primary): {product.Name} price updated");

        // Transaction → always primary
        db.BeginTransaction();
        try
        {
            db.Execute("UPDATE Products SET Stock = Stock - 1 WHERE Id = @0", 1);
            db.Commit();
            Console.WriteLine("   ✓ TRANSACTION (→ primary): Stock updated");
        }
        catch
        {
            db.Rollback();
            throw;
        }
    }

    static void ExplicitRoutingExamples(Database db)
    {
        Console.WriteLine("   Explicit routing hints override automatic routing:");

        // Force read from primary (for read-after-write consistency)
        var product = db.UsePrimary()
                        .Single<Product>("SELECT * FROM Products WHERE Id = @0", 1);
        Console.WriteLine($"   ✓ UsePrimary(): Read from primary - {product.Name}");

        // Force read from replica
        var products = db.UseReplica()
                         .Sql<Product>("SELECT * FROM Products WHERE Price > @0", 100)
                         .ToList();
        Console.WriteLine($"   ✓ UseReplica(): Read from replica - {products.Count} products");

        // Restore automatic routing
        products = db.UseAutoRouting()
                     .Sql<Product>("SELECT * FROM Products")
                     .ToList();
        Console.WriteLine($"   ✓ UseAutoRouting(): Auto route - {products.Count} products");

        // Chain methods
        var expensive = db.UsePrimary()
                          .Sql<Product>("SELECT * FROM Products WHERE Price > @0 ORDER BY Price DESC", 500)
                          .ToList();
        Console.WriteLine($"   ✓ Chained with query: {expensive.Count} expensive items");
    }

    static void LoadBalancingExamples()
    {
        Console.WriteLine("   Load balancing strategies:");

        // 1. Round Robin
        var rrConfig = new ReadWriteConfiguration
        {
            PrimaryConnectionString = "Data Source=:memory:",
            ReplicaConnectionStrings = new[] { "replica1", "replica2", "replica3" },
            LoadBalancingStrategy = LoadBalancingStrategy.RoundRobin
        };
        Console.WriteLine("   ✓ RoundRobin: Distributes evenly (replica1 → replica2 → replica3 → ...)");

        // 2. Random
        var randomConfig = new ReadWriteConfiguration
        {
            PrimaryConnectionString = "Data Source=:memory:",
            ReplicaConnectionStrings = new[] { "replica1", "replica2" },
            LoadBalancingStrategy = LoadBalancingStrategy.Random
        };
        Console.WriteLine("   ✓ Random: Random replica selection");

        // 3. Primary Replica (all reads to first replica)
        var primaryReplicaConfig = new ReadWriteConfiguration
        {
            PrimaryConnectionString = "Data Source=:memory:",
            ReplicaConnectionStrings = new[] { "replica1", "replica2" },
            LoadBalancingStrategy = LoadBalancingStrategy.PrimaryReplica
        };
        Console.WriteLine("   ✓ PrimaryReplica: All reads to first replica (replica1)");

        // Best practices
        Console.WriteLine("\n   Best practices:");
        Console.WriteLine("     • RoundRobin: Even load distribution (default)");
        Console.WriteLine("     • Random: Simple, good for many replicas");
        Console.WriteLine("     • PrimaryReplica: When one replica is more powerful");
    }

    static void StickySessionExamples(Database db)
    {
        Console.WriteLine("   Sticky sessions for read-after-write consistency:");

        // Enable sticky sessions
        var config = new ReadWriteConfiguration
        {
            PrimaryConnectionString = "Data Source=:memory:",
            ReplicaConnectionStrings = new[] { "replica1", "replica2" },
            EnableStickySessions = true,
            StickySessionWindow = TimeSpan.FromSeconds(30)
        };

        Console.WriteLine("   ✓ Sticky sessions enabled (30s window)");

        // Write operation
        var product = new Product
        {
            Name = "Mouse",
            Price = 29.99m,
            Stock = 100,
            UpdatedAt = DateTime.UtcNow
        };
        db.Insert(product);
        Console.WriteLine($"   ✓ INSERT: {product.Name} written to primary");

        // Immediate read → routed to primary (within sticky window)
        var readBack = db.Single<Product>("SELECT * FROM Products WHERE Id = @0", product.Id);
        Console.WriteLine($"   ✓ Immediate SELECT: Routed to primary (sticky) - {readBack.Name}");

        // After sticky window expires, reads go to replicas
        Console.WriteLine("   • After 30s: Reads route to replicas");

        // Manual sticky session control
        db.UsePrimary(); // Force primary for critical reads
        Console.WriteLine("   ✓ Manual override: UsePrimary() for critical operations");
    }

    static void ConnectionPoolingExample()
    {
        Console.WriteLine("   Connection pooling configuration:");

        var config = new ReadWriteConfiguration
        {
            PrimaryConnectionString = "Data Source=:memory:",
            ReplicaConnectionStrings = new[] { "replica1", "replica2" },
            MaxConnectionsPerPool = 100, // Max connections per pool
            LoadBalancingStrategy = LoadBalancingStrategy.RoundRobin
        };

        Console.WriteLine($"   ✓ Max connections per pool: {config.MaxConnectionsPerPool}");
        Console.WriteLine($"   ✓ Primary pool: 1 × {config.MaxConnectionsPerPool} = {config.MaxConnectionsPerPool} connections");
        Console.WriteLine($"   ✓ Replica pools: 2 × {config.MaxConnectionsPerPool} = {config.MaxConnectionsPerPool * 2} connections");
        Console.WriteLine($"   ✓ Total capacity: {config.MaxConnectionsPerPool * 3} concurrent connections");

        Console.WriteLine("\n   Benefits:");
        Console.WriteLine("     • Thread-safe connection management");
        Console.WriteLine("     • Automatic connection reuse");
        Console.WriteLine("     • Prevents connection exhaustion");
        Console.WriteLine("     • Scales with replica count");
    }
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public DateTime UpdatedAt { get; set; }
}
