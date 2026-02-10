using AnubisWorks.SQLFactory;
using Microsoft.Data.Sqlite;

namespace SQLFactory.Examples.BasicCRUD;

/// <summary>
/// Example: Basic CRUD operations with SQLFactory
/// Demonstrates: Create, Read, Update, Delete, Queries, Transactions
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== SQLFactory Basic CRUD Example ===\n");

        // Create in-memory SQLite database
        var connectionString = "Data Source=:memory:";
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        // Initialize database
        InitializeDatabase(connection);

        // Create Database instance
        var db = new Database(connection);

        // Run examples
        Console.WriteLine("1. CREATE - Insert new records");
        CreateExamples(db);

        Console.WriteLine("\n2. READ - Query records");
        ReadExamples(db);

        Console.WriteLine("\n3. UPDATE - Modify existing records");
        UpdateExamples(db);

        Console.WriteLine("\n4. DELETE - Remove records");
        DeleteExamples(db);

        Console.WriteLine("\n5. TRANSACTIONS - Atomic operations");
        TransactionExamples(db);

        Console.WriteLine("\n=== Example completed successfully! ===");
    }

    static void InitializeDatabase(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE Products (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Price REAL NOT NULL,
                Stock INTEGER NOT NULL,
                CategoryId INTEGER NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE Categories (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Description TEXT
            );

            -- Insert sample categories
            INSERT INTO Categories (Name, Description) VALUES
                ('Electronics', 'Electronic devices and accessories'),
                ('Books', 'Physical and digital books'),
                ('Clothing', 'Apparel and fashion');
        ";
        cmd.ExecuteNonQuery();
    }

    static void CreateExamples(Database db)
    {
        // Example 1: Insert single record
        var product = new Product
        {
            Name = "Laptop Dell XPS 13",
            Price = 1299.99m,
            Stock = 10,
            CategoryId = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        db.Insert(product);
        Console.WriteLine($"   ✓ Inserted product: {product.Name} (Id: {product.Id})");

        // Example 2: Insert multiple records
        var products = new[]
        {
            new Product
            {
                Name = "The Pragmatic Programmer",
                Price = 39.99m,
                Stock = 50,
                CategoryId = 2,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new Product
            {
                Name = "Clean Code",
                Price = 44.95m,
                Stock = 30,
                CategoryId = 2,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new Product
            {
                Name = "T-Shirt Nike",
                Price = 29.99m,
                Stock = 100,
                CategoryId = 3,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        foreach (var p in products)
        {
            db.Insert(p);
            Console.WriteLine($"   ✓ Inserted: {p.Name} (Id: {p.Id})");
        }
    }

    static void ReadExamples(Database db)
    {
        // Example 1: Get all records
        var allProducts = db.Sql<Product>("SELECT * FROM Products").ToList();
        Console.WriteLine($"   ✓ Total products: {allProducts.Count}");

        // Example 2: Get by ID
        var product = db.Single<Product>("SELECT * FROM Products WHERE Id = @0", 1);
        Console.WriteLine($"   ✓ Found product by Id: {product.Name}");

        // Example 3: Query with WHERE clause
        var books = db.Sql<Product>("SELECT * FROM Products WHERE CategoryId = @0", 2).ToList();
        Console.WriteLine($"   ✓ Books found: {books.Count}");
        foreach (var book in books)
        {
            Console.WriteLine($"     - {book.Name}: ${book.Price}");
        }

        // Example 4: Query with ordering
        var expensive = db.Sql<Product>("SELECT * FROM Products ORDER BY Price DESC LIMIT 3").ToList();
        Console.WriteLine($"   ✓ Top 3 most expensive:");
        foreach (var item in expensive)
        {
            Console.WriteLine($"     - {item.Name}: ${item.Price}");
        }

        // Example 5: Query with multiple conditions
        var activeExpensive = db.Sql<Product>(
            "SELECT * FROM Products WHERE IsActive = @0 AND Price > @1 ORDER BY Price DESC",
            true, 40m
        ).ToList();
        Console.WriteLine($"   ✓ Active products over $40: {activeExpensive.Count}");

        // Example 6: FirstOrDefault
        var cheapest = db.Sql<Product>("SELECT * FROM Products ORDER BY Price ASC LIMIT 1").FirstOrDefault();
        if (cheapest != null)
        {
            Console.WriteLine($"   ✓ Cheapest product: {cheapest.Name} (${cheapest.Price})");
        }

        // Example 7: Count
        var count = db.Sql<int>("SELECT COUNT(*) FROM Products WHERE IsActive = @0", true).Single();
        Console.WriteLine($"   ✓ Active products count: {count}");

        // Example 8: Aggregate functions
        var avgPrice = db.Sql<decimal>("SELECT AVG(Price) FROM Products").Single();
        Console.WriteLine($"   ✓ Average price: ${avgPrice:F2}");

        var totalStock = db.Sql<int>("SELECT SUM(Stock) FROM Products").Single();
        Console.WriteLine($"   ✓ Total stock: {totalStock} units");
    }

    static void UpdateExamples(Database db)
    {
        // Example 1: Update single record
        var product = db.Single<Product>("SELECT * FROM Products WHERE Id = @0", 1);
        product.Price = 1199.99m;
        product.Stock = 5;
        
        db.Update(product);
        Console.WriteLine($"   ✓ Updated product: {product.Name} - New price: ${product.Price}");

        // Example 2: Update with SQL
        var rowsAffected = db.Execute(
            "UPDATE Products SET Stock = Stock + @0 WHERE CategoryId = @1",
            10, 2
        );
        Console.WriteLine($"   ✓ Increased stock for {rowsAffected} books");

        // Example 3: Update multiple fields
        db.Execute(
            "UPDATE Products SET IsActive = @0, Price = Price * 0.9 WHERE Stock > @1",
            true, 50
        );
        Console.WriteLine("   ✓ Applied 10% discount to high-stock items");

        // Example 4: Update with verification
        var productToUpdate = db.Single<Product>("SELECT * FROM Products WHERE Id = @0", 2);
        var originalPrice = productToUpdate.Price;
        productToUpdate.Price = originalPrice * 1.1m;
        db.Update(productToUpdate);
        
        var updated = db.Single<Product>("SELECT * FROM Products WHERE Id = @0", 2);
        Console.WriteLine($"   ✓ Price updated: ${originalPrice:F2} → ${updated.Price:F2}");
    }

    static void DeleteExamples(Database db)
    {
        // Example 1: Delete single record
        var product = db.Single<Product>("SELECT * FROM Products WHERE Id = @0", 4);
        db.Delete(product);
        Console.WriteLine($"   ✓ Deleted product: {product.Name}");

        // Example 2: Delete with SQL
        var deletedCount = db.Execute(
            "DELETE FROM Products WHERE Stock = @0 AND IsActive = @1",
            0, false
        );
        Console.WriteLine($"   ✓ Deleted {deletedCount} inactive products with zero stock");

        // Example 3: Verify deletion
        var remaining = db.Sql<int>("SELECT COUNT(*) FROM Products").Single();
        Console.WriteLine($"   ✓ Remaining products: {remaining}");
    }

    static void TransactionExamples(Database db)
    {
        // Example 1: Successful transaction
        Console.WriteLine("   Testing successful transaction...");
        db.BeginTransaction();
        try
        {
            var product = new Product
            {
                Name = "iPhone 15",
                Price = 999.99m,
                Stock = 20,
                CategoryId = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Insert(product);
            
            db.Execute("UPDATE Categories SET Description = @0 WHERE Id = @1",
                "Updated electronics description", 1);

            db.Commit();
            Console.WriteLine("   ✓ Transaction committed successfully");
        }
        catch
        {
            db.Rollback();
            Console.WriteLine("   ✗ Transaction rolled back");
            throw;
        }

        // Example 2: Failed transaction (rollback)
        Console.WriteLine("   Testing transaction rollback...");
        var beforeCount = db.Sql<int>("SELECT COUNT(*) FROM Products").Single();
        
        db.BeginTransaction();
        try
        {
            var product = new Product
            {
                Name = "Test Product",
                Price = 99.99m,
                Stock = 1,
                CategoryId = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Insert(product);

            // Simulate error
            throw new Exception("Simulated error");
        }
        catch
        {
            db.Rollback();
            var afterCount = db.Sql<int>("SELECT COUNT(*) FROM Products").Single();
            Console.WriteLine($"   ✓ Transaction rolled back (count: {beforeCount} → {afterCount})");
        }
    }
}

/// <summary>
/// Product entity - POCO (Plain Old CLR Object)
/// No attributes required, SQLFactory maps by convention
/// </summary>
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public int CategoryId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Category entity
/// </summary>
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
