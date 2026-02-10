using AnubisWorks.SQLFactory;
using AnubisWorks.SQLFactory.SoftDelete;
using Microsoft.Data.Sqlite;

namespace SQLFactory.Examples.SoftDelete;

/// <summary>
/// Example: Soft Delete pattern for data retention
/// Demonstrates: ISoftDeletable, SoftDelete(), Restore(), filtering
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== SQLFactory Soft Delete Example ===\n");

        var db = CreateDatabase();

        Console.WriteLine("1. SOFT DELETE OPERATIONS");
        SoftDeleteOperationsExample(db);

        Console.WriteLine("\n2. RESTORE DELETED RECORDS");
        RestoreDeletedExample(db);

        Console.WriteLine("\n3. QUERY DELETED RECORDS");
        QueryDeletedExample(db);

        Console.WriteLine("\n4. HARD DELETE");
        HardDeleteExample(db);

        Console.WriteLine("\n5. INTEGRATION WITH GLOBAL FILTERS");
        GlobalFilterIntegrationExample(db);

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
                IsDeleted INTEGER NOT NULL DEFAULT 0,
                DeletedAt TEXT NULL,
                DeletedBy TEXT NULL
            );

            INSERT INTO Products (Name, Price, IsDeleted, DeletedAt, DeletedBy) VALUES
                ('Laptop', 1200, 0, NULL, NULL),
                ('Phone', 800, 0, NULL, NULL),
                ('Tablet', 500, 1, datetime('now'), 'admin');  -- Already soft deleted
        ";
        cmd.ExecuteNonQuery();

        Console.WriteLine("   ✓ Database created with sample data\n");
        return new Database(conn);
    }

    static void SoftDeleteOperationsExample(Database db)
    {
        Console.WriteLine("   Soft delete operations:");

        var product = db.Single<Product>("SELECT * FROM Products WHERE Id = @0", 1);
        Console.WriteLine($"   ✓ Loaded: {product.Name} (IsDeleted: {product.IsDeleted})");

        // Soft delete
        product.SoftDelete("user123");
        db.Update(product);
        
        Console.WriteLine($"   ✓ Soft deleted: {product.Name}");
        Console.WriteLine($"     • IsDeleted: {product.IsDeleted}");
        Console.WriteLine($"     • DeletedAt: {product.DeletedAt}");
        Console.WriteLine($"     • DeletedBy: {product.DeletedBy}");

        // Verify soft delete
        var active = db.Query<Product>("SELECT * FROM Products WHERE IsDeleted = 0").ToList();
        Console.WriteLine($"   ✓ Active products remaining: {active.Count}");
    }

    static void RestoreDeletedExample(Database db)
    {
        Console.WriteLine("   Restoring soft deleted records:");

        var deleted = db.Single<Product>("SELECT * FROM Products WHERE Id = @0", 3);
        Console.WriteLine($"   ✓ Found deleted: {deleted.Name} (Deleted: {deleted.DeletedAt})");

        // Restore
        deleted.Restore();
        db.Update(deleted);
        
        Console.WriteLine($"   ✓ Restored: {deleted.Name}");
        Console.WriteLine($"     • IsDeleted: {deleted.IsDeleted}");
        Console.WriteLine($"     • DeletedAt: {deleted.DeletedAt}");
        Console.WriteLine($"     • DeletedBy: {deleted.DeletedBy}");

        // Verify restoration
        var active = db.Query<Product>("SELECT * FROM Products WHERE IsDeleted = 0").ToList();
        Console.WriteLine($"   ✓ Active products: {active.Count}");
    }

    static void QueryDeletedExample(Database db)
    {
        Console.WriteLine("   Querying deleted records:");

        // Only deleted records
        var deleted = db.Query<Product>()
                        .OnlyDeleted()
                        .ToList();
        Console.WriteLine($"   ✓ OnlyDeleted(): {deleted.Count} records");

        foreach (var product in deleted)
        {
            Console.WriteLine($"     • {product.Name} (Deleted: {product.DeletedAt}, By: {product.DeletedBy})");
        }

        // Include deleted in results
        var all = db.Query<Product>()
                    .IncludeDeleted()
                    .ToList();
        Console.WriteLine($"   ✓ IncludeDeleted(): {all.Count} total records");

        // Active only (default behavior)
        var active = db.Query<Product>().ToList();
        Console.WriteLine($"   ✓ Default query: {active.Count} active records");
    }

    static void HardDeleteExample(Database db)
    {
        Console.WriteLine("   Hard delete (permanent removal):");

        var product = db.Single<Product>("SELECT * FROM Products WHERE Id = @0", 2);
        Console.WriteLine($"   ✓ Product to hard delete: {product.Name}");

        // Hard delete permanently removes from database
        db.HardDelete(product);
        Console.WriteLine($"   ✓ Hard deleted: {product.Name} (permanently removed)");

        // Verify hard delete
        var exists = db.Query<Product>("SELECT * FROM Products WHERE Id = @0", 2).FirstOrDefault();
        Console.WriteLine($"   ✓ Verification: Product exists = {exists != null}");

        Console.WriteLine("\n   When to use:");
        Console.WriteLine("     • Soft Delete: Default (audit trail, recovery)");
        Console.WriteLine("     • Hard Delete: GDPR compliance, test data cleanup");
    }

    static void GlobalFilterIntegrationExample(Database db)
    {
        Console.WriteLine("   Integration with Global Query Filters:");

        // Register global filter to exclude soft deleted
        db.AddGlobalFilter<Product>(p => !p.IsDeleted);
        Console.WriteLine("   ✓ Global filter registered: !IsDeleted");

        // Normal queries automatically exclude deleted
        var products = db.Query<Product>("SELECT * FROM Products").ToList();
        Console.WriteLine($"   ✓ Query result: {products.Count} active products");

        foreach (var product in products)
        {
            Console.WriteLine($"     • {product.Name} (${product.Price})");
        }

        // Admin view - override filter
        var allProducts = db.Query<Product>()
                            .IgnoreQueryFilters()
                            .ToList();
        Console.WriteLine($"   ✓ Admin view: {allProducts.Count} total (including deleted)");

        Console.WriteLine("\n   Best practices:");
        Console.WriteLine("     • Use ISoftDeletable interface");
        Console.WriteLine("     • Track DeletedAt and DeletedBy for audit");
        Console.WriteLine("     • Combine with Global Filters for automatic filtering");
        Console.WriteLine("     • Provide admin interface for restore operations");
        Console.WriteLine("     • Consider retention policies for old deleted data");
    }
}

public class Product : ISoftDeletable
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    public void SoftDelete(string deletedBy)
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        DeletedBy = deletedBy;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
    }
}

public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    string? DeletedBy { get; set; }
    void SoftDelete(string deletedBy);
    void Restore();
}
