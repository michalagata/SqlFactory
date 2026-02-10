using AnubisWorks.SQLFactory;
using Microsoft.Data.Sqlite;

namespace SQLFactory.Examples.EagerLoading;

/// <summary>
/// Example: Eager Loading for efficient data fetching
/// Demonstrates: Include(), ThenInclude(), split queries, N+1 problem prevention
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== SQLFactory Eager Loading Example ===\n");

        var db = CreateDatabase();

        Console.WriteLine("1. N+1 PROBLEM DEMONSTRATION");
        N1ProblemExample(db);

        Console.WriteLine("\n2. SINGLE LEVEL INCLUDE");
        SingleLevelIncludeExample(db);

        Console.WriteLine("\n3. MULTI-LEVEL INCLUDE WITH THENINCLUDE");
        MultiLevelIncludeExample(db);

        Console.WriteLine("\n4. COLLECTION INCLUDE");
        CollectionIncludeExample(db);

        Console.WriteLine("\n5. SPLIT QUERY STRATEGY");
        SplitQueryExample(db);

        Console.WriteLine("\n=== Example completed successfully! ===");
    }

    static Database CreateDatabase()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE Categories (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL
            );

            CREATE TABLE Products (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Price REAL NOT NULL,
                CategoryId INTEGER NOT NULL,
                FOREIGN KEY (CategoryId) REFERENCES Categories(Id)
            );

            CREATE TABLE OrderItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProductId INTEGER NOT NULL,
                Quantity INTEGER NOT NULL,
                UnitPrice REAL NOT NULL,
                FOREIGN KEY (ProductId) REFERENCES Products(Id)
            );

            INSERT INTO Categories (Name) VALUES ('Electronics'), ('Books'), ('Clothing');
            
            INSERT INTO Products (Name, Price, CategoryId) VALUES
                ('Laptop', 1200, 1),
                ('Phone', 800, 1),
                ('Novel', 15, 2),
                ('T-Shirt', 25, 3);

            INSERT INTO OrderItems (ProductId, Quantity, UnitPrice) VALUES
                (1, 2, 1200),
                (2, 1, 800),
                (3, 5, 15);
        ";
        cmd.ExecuteNonQuery();

        Console.WriteLine("   ✓ Database created with sample data\n");
        return new Database(conn);
    }

    static void N1ProblemExample(Database db)
    {
        Console.WriteLine("   WITHOUT Include() - N+1 queries:");
        
        var products = db.Query<Product>("SELECT * FROM Products").ToList();
        Console.WriteLine($"   Query 1: Loaded {products.Count} products");

        // Each product access triggers separate query - N+1 problem!
        int queryCount = 1;
        foreach (var product in products)
        {
            var category = db.Single<Category>("SELECT * FROM Categories WHERE Id = @0", product.CategoryId);
            queryCount++;
            Console.WriteLine($"   Query {queryCount}: Category for {product.Name}");
        }
        
        Console.WriteLine($"   ⚠ Total: {queryCount} database queries (1 + {products.Count} N+1 problem)");
    }

    static void SingleLevelIncludeExample(Database db)
    {
        Console.WriteLine("   WITH Include() - single query:");

        // Include() fetches related data in one query
        var products = db.Query<Product>()
                         .Include(p => p.Category)
                         .ToList();

        Console.WriteLine($"   ✓ Single query: Loaded {products.Count} products with categories");

        foreach (var product in products)
        {
            // No additional queries - category already loaded!
            Console.WriteLine($"     • {product.Name} ({product.Category.Name}) - ${product.Price}");
        }

        Console.WriteLine("   ✓ Total: 1 database query (efficient!)");
    }

    static void MultiLevelIncludeExample(Database db)
    {
        Console.WriteLine("   Multi-level relationships with ThenInclude():");

        // OrderItem → Product → Category (2 levels deep)
        var orderItems = db.Query<OrderItem>()
                           .Include(oi => oi.Product)
                           .ThenInclude(p => p.Category)
                           .ToList();

        Console.WriteLine($"   ✓ Loaded {orderItems.Count} order items with products and categories");

        foreach (var item in orderItems)
        {
            Console.WriteLine($"     • {item.Product.Name} ({item.Product.Category.Name})");
            Console.WriteLine($"       Qty: {item.Quantity} × ${item.UnitPrice} = ${item.Quantity * item.UnitPrice}");
        }

        Console.WriteLine("   ✓ Single query with JOINs for nested data");
    }

    static void CollectionIncludeExample(Database db)
    {
        Console.WriteLine("   Include() with collections (1-to-many):");

        // Category has many Products
        var categories = db.Query<Category>()
                           .Include(c => c.Products)
                           .ToList();

        Console.WriteLine($"   ✓ Loaded {categories.Count} categories with products");

        foreach (var category in categories)
        {
            Console.WriteLine($"     • {category.Name}: {category.Products.Count} products");
            foreach (var product in category.Products)
            {
                Console.WriteLine($"       - {product.Name} (${product.Price})");
            }
        }

        Console.WriteLine("   ✓ Collections loaded eagerly");
    }

    static void SplitQueryExample(Database db)
    {
        Console.WriteLine("   Split Query strategy for large collections:");

        // AsSplitQuery() prevents cartesian explosion
        var categories = db.Query<Category>()
                           .Include(c => c.Products)
                           .AsSplitQuery() // Multiple queries instead of single JOIN
                           .ToList();

        Console.WriteLine($"   ✓ Categories: 1 query");
        Console.WriteLine($"   ✓ Products: 1 query per collection");
        Console.WriteLine($"   Total: {1 + categories.Count} queries");

        Console.WriteLine("\n   When to use AsSplitQuery():");
        Console.WriteLine("     • Multiple large collections (avoids cartesian explosion)");
        Console.WriteLine("     • Better performance with many-to-many relationships");
        Console.WriteLine("     • Trade-off: More queries, but simpler and faster");
    }
}

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int CategoryId { get; set; }
    public virtual Category Category { get; set; } = null!;
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}

public class OrderItem
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public virtual Product Product { get; set; } = null!;
}
