using AnubisWorks.SQLFactory;
using AnubisWorks.SQLFactory.Filters;
using Microsoft.Data.Sqlite;

namespace SQLFactory.Examples.GlobalFilters;

/// <summary>
/// Example: Global Query Filters for cross-cutting concerns
/// Demonstrates: Soft delete, multi-tenancy, IgnoreQueryFilters()
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== SQLFactory Global Query Filters Example ===\n");

        var db = CreateDatabase();

        Console.WriteLine("1. SOFT DELETE FILTER");
        SoftDeleteFilterExample(db);

        Console.WriteLine("\n2. MULTI-TENANCY FILTER");
        MultiTenancyFilterExample(db);

        Console.WriteLine("\n3. IGNORE QUERY FILTERS");
        IgnoreFiltersExample(db);

        Console.WriteLine("\n4. CUSTOM FILTERS");
        CustomFiltersExample(db);

        Console.WriteLine("\n5. CONDITIONAL FILTERS");
        ConditionalFiltersExample(db);

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
                TenantId INTEGER NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1
            );

            INSERT INTO Products (Name, Price, IsDeleted, TenantId, IsActive) VALUES
                ('Laptop', 1200, 0, 1, 1),
                ('Phone', 800, 1, 1, 1),      -- Soft deleted
                ('Tablet', 500, 0, 2, 1),
                ('Monitor', 300, 0, 1, 0);    -- Inactive
        ";
        cmd.ExecuteNonQuery();

        Console.WriteLine("   ✓ Database created with sample data\n");
        return new Database(conn);
    }

    static void SoftDeleteFilterExample(Database db)
    {
        Console.WriteLine("   Soft delete filtering:");

        // Register global filter
        db.AddGlobalFilter<Product>(p => !p.IsDeleted);
        Console.WriteLine("   ✓ Registered filter: !IsDeleted");

        // Query automatically excludes soft-deleted records
        var products = db.Query<Product>("SELECT * FROM Products").ToList();
        Console.WriteLine($"   ✓ Active products: {products.Count}");
        
        foreach (var product in products)
        {
            Console.WriteLine($"     • {product.Name} (${product.Price}) [Deleted: {product.IsDeleted}]");
        }

        Console.WriteLine("   ✓ Soft deleted products automatically excluded");
    }

    static void MultiTenancyFilterExample(Database db)
    {
        Console.WriteLine("   Multi-tenancy filtering:");

        // Simulate current tenant context
        int currentTenantId = 1;
        
        // Register tenant filter
        db.AddGlobalFilter<Product>(p => p.TenantId == currentTenantId);
        Console.WriteLine($"   ✓ Registered filter: TenantId == {currentTenantId}");

        var products = db.Query<Product>("SELECT * FROM Products").ToList();
        Console.WriteLine($"   ✓ Tenant {currentTenantId} products: {products.Count}");

        foreach (var product in products)
        {
            Console.WriteLine($"     • {product.Name} (Tenant: {product.TenantId})");
        }

        // Change tenant context
        currentTenantId = 2;
        db.ClearGlobalFilters<Product>();
        db.AddGlobalFilter<Product>(p => p.TenantId == currentTenantId);
        
        var tenant2Products = db.Query<Product>("SELECT * FROM Products").ToList();
        Console.WriteLine($"   ✓ Tenant {currentTenantId} products: {tenant2Products.Count}");
    }

    static void IgnoreFiltersExample(Database db)
    {
        Console.WriteLine("   Ignoring filters for admin operations:");

        // With filters
        db.AddGlobalFilter<Product>(p => !p.IsDeleted);
        var filtered = db.Query<Product>("SELECT * FROM Products").ToList();
        Console.WriteLine($"   ✓ With filter: {filtered.Count} products");

        // Without filters (admin view)
        var all = db.Query<Product>()
                    .IgnoreQueryFilters()
                    .ToList();
        Console.WriteLine($"   ✓ IgnoreQueryFilters(): {all.Count} products (including deleted)");

        foreach (var product in all)
        {
            var status = product.IsDeleted ? "DELETED" : "Active";
            Console.WriteLine($"     • {product.Name} [{status}]");
        }

        Console.WriteLine("   ✓ Use case: Admin dashboard showing all records");
    }

    static void CustomFiltersExample(Database db)
    {
        Console.WriteLine("   Custom business logic filters:");

        // Filter 1: Active products only
        db.AddGlobalFilter<Product>(p => p.IsActive);
        Console.WriteLine("   ✓ Filter 1: IsActive");

        // Filter 2: Price range
        db.AddGlobalFilter<Product>(p => p.Price >= 100 && p.Price <= 1000);
        Console.WriteLine("   ✓ Filter 2: Price between 100 and 1000");

        // Filters are combined with AND
        var products = db.Query<Product>("SELECT * FROM Products").ToList();
        Console.WriteLine($"   ✓ Products matching ALL filters: {products.Count}");

        foreach (var product in products)
        {
            Console.WriteLine($"     • {product.Name} (${product.Price}) [Active: {product.IsActive}]");
        }
    }

    static void ConditionalFiltersExample(Database db)
    {
        Console.WriteLine("   Conditional filter application:");

        bool isAdmin = false;
        
        // Apply filter only for non-admin users
        if (!isAdmin)
        {
            db.AddGlobalFilter<Product>(p => !p.IsDeleted);
            db.AddGlobalFilter<Product>(p => p.IsActive);
            Console.WriteLine("   ✓ User filters: !IsDeleted && IsActive");
        }
        else
        {
            Console.WriteLine("   ✓ Admin: No filters applied");
        }

        var products = db.Query<Product>("SELECT * FROM Products").ToList();
        Console.WriteLine($"   ✓ Visible products: {products.Count}");

        // Switch to admin mode
        isAdmin = true;
        db.ClearGlobalFilters<Product>();
        
        var adminProducts = db.Query<Product>().ToList();
        Console.WriteLine($"   ✓ Admin view: {adminProducts.Count} products (no filters)");

        Console.WriteLine("\n   Best practices:");
        Console.WriteLine("     • Set filters early in application lifecycle");
        Console.WriteLine("     • Use DI/middleware for tenant/user context");
        Console.WriteLine("     • Clear filters when switching contexts");
        Console.WriteLine("     • Document filter behavior in API docs");
    }
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsDeleted { get; set; }
    public int TenantId { get; set; }
    public bool IsActive { get; set; }
}
