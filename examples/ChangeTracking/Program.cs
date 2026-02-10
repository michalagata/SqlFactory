using AnubisWorks.SQLFactory;
using AnubisWorks.SQLFactory.ChangeTracking;
using Microsoft.Data.Sqlite;

namespace SQLFactory.Examples.ChangeTracking;

/// <summary>
/// Example: Change Tracking and Unit of Work
/// Demonstrates: DetectChanges(), SaveChanges(), state management
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== SQLFactory Change Tracking Example ===\n");

        var db = CreateDatabase();

        Console.WriteLine("1. BASIC CHANGE TRACKING");
        BasicChangeTrackingExample(db);

        Console.WriteLine("\n2. DETECT CHANGES");
        DetectChangesExample(db);

        Console.WriteLine("\n3. ENTITY STATES");
        EntityStatesExample(db);

        Console.WriteLine("\n4. BATCH SAVE CHANGES");
        BatchSaveChangesExample(db);

        Console.WriteLine("\n5. RELATIONSHIP FIXUP");
        RelationshipFixupExample(db);

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
                Stock INTEGER NOT NULL
            );

            INSERT INTO Products (Name, Price, Stock) VALUES
                ('Laptop', 1200, 10),
                ('Phone', 800, 25);
        ";
        cmd.ExecuteNonQuery();

        Console.WriteLine("   ✓ Database created with sample data\n");
        return new Database(conn);
    }

    static void BasicChangeTrackingExample(Database db)
    {
        Console.WriteLine("   Basic change tracking:");

        // Enable change tracking
        db.EnableChangeTracking();
        Console.WriteLine("   ✓ Change tracking enabled");

        // Load entity
        var product = db.Single<Product>("SELECT * FROM Products WHERE Id = @0", 1);
        Console.WriteLine($"   ✓ Loaded: {product.Name} (Price: ${product.Price})");

        // Modify entity
        product.Price = 1299.99m;
        product.Stock = 8;
        Console.WriteLine($"   Modified: Price → ${product.Price}, Stock → {product.Stock}");

        // Save changes
        db.SaveChanges();
        Console.WriteLine("   ✓ SaveChanges() executed");

        // Verify
        var updated = db.Single<Product>("SELECT * FROM Products WHERE Id = @0", 1);
        Console.WriteLine($"   ✓ Verified: {updated.Name} (${updated.Price}, Stock: {updated.Stock})");
    }

    static void DetectChangesExample(Database db)
    {
        Console.WriteLine("   Detecting changes:");

        db.EnableChangeTracking();
        
        var product = db.Single<Product>("SELECT * FROM Products WHERE Id = @0", 1);
        var originalPrice = product.Price;
        
        // Modify
        product.Price = 1499.99m;
        
        // Detect what changed
        var changes = db.DetectChanges(product);
        Console.WriteLine($"   ✓ Detected {changes.Count} changes:");
        
        foreach (var change in changes)
        {
            Console.WriteLine($"     • {change.Property}: {change.OriginalValue} → {change.CurrentValue}");
        }

        // Revert changes
        product.Price = originalPrice;
        var noChanges = db.DetectChanges(product);
        Console.WriteLine($"   ✓ After revert: {noChanges.Count} changes");
    }

    static void EntityStatesExample(Database db)
    {
        Console.WriteLine("   Entity state management:");

        db.EnableChangeTracking();

        // Added state
        var newProduct = new Product
        {
            Name = "Tablet",
            Price = 500,
            Stock = 15
        };
        db.Add(newProduct);
        Console.WriteLine($"   ✓ State: Added - {newProduct.Name}");

        // Modified state
        var existing = db.Single<Product>("SELECT * FROM Products WHERE Id = @0", 1);
        existing.Price = 1399.99m;
        db.MarkModified(existing);
        Console.WriteLine($"   ✓ State: Modified - {existing.Name}");

        // Deleted state
        var toDelete = db.Single<Product>("SELECT * FROM Products WHERE Id = @0", 2);
        db.MarkDeleted(toDelete);
        Console.WriteLine($"   ✓ State: Deleted - {toDelete.Name}");

        // Unchanged state
        var unchanged = db.Single<Product>("SELECT * FROM Products WHERE Id = @0", 1);
        Console.WriteLine($"   ✓ State: Unchanged - {unchanged.Name}");

        // Apply all changes in one transaction
        db.SaveChanges();
        Console.WriteLine("   ✓ All state changes persisted");
    }

    static void BatchSaveChangesExample(Database db)
    {
        Console.WriteLine("   Batch save changes:");

        db.EnableChangeTracking();

        // Multiple changes
        var products = db.Query<Product>("SELECT * FROM Products").ToList();
        
        foreach (var product in products)
        {
            product.Stock += 10; // Restock
            product.Price *= 1.05m; // 5% price increase
            Console.WriteLine($"   Modified: {product.Name}");
        }

        // Single SaveChanges() for all
        int affected = db.SaveChanges();
        Console.WriteLine($"   ✓ SaveChanges() affected {affected} entities");

        // Verify batch update
        var updated = db.Query<Product>("SELECT * FROM Products").ToList();
        Console.WriteLine($"   ✓ Verified: All {updated.Count} products updated");
    }

    static void RelationshipFixupExample(Database db)
    {
        Console.WriteLine("   Relationship fixup:");

        db.EnableChangeTracking();

        // Add product with category relationship
        var category = new Category { Name = "Electronics" };
        var product = new Product
        {
            Name = "Monitor",
            Price = 300,
            Stock = 20,
            Category = category
        };

        db.Add(category);
        db.Add(product);
        
        // Change tracking fixes relationships automatically
        Console.WriteLine($"   ✓ Product: {product.Name}");
        Console.WriteLine($"   ✓ Category: {category.Name}");
        Console.WriteLine($"   ✓ Relationship: CategoryId will be set after SaveChanges()");

        db.SaveChanges();
        Console.WriteLine($"   ✓ SaveChanges() - relationship fixup complete");
        Console.WriteLine($"   ✓ Product.CategoryId = {product.CategoryId}");

        Console.WriteLine("\n   Benefits:");
        Console.WriteLine("     • Automatic batch operations");
        Console.WriteLine("     • Transaction management");
        Console.WriteLine("     • Relationship integrity");
        Console.WriteLine("     • Unit of Work pattern");
    }
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public int? CategoryId { get; set; }
    public virtual Category? Category { get; set; }
}

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
