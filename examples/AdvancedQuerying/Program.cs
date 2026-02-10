using AnubisWorks.SQLFactory;
using Microsoft.Data.Sqlite;

namespace SQLFactory.Examples.AdvancedQuerying;

/// <summary>
/// Example: Advanced querying techniques with SQLFactory
/// Demonstrates: SqlBuilder, Complex queries, Joins, Grouping, Pagination, Dynamic SQL
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== SQLFactory Advanced Querying Example ===\n");

        var connectionString = "Data Source=:memory:";
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        InitializeDatabase(connection);
        var db = new Database(connection);

        Console.WriteLine("1. SQL BUILDER - Fluent query construction");
        SqlBuilderExamples(db);

        Console.WriteLine("\n2. JOINS - Combining tables");
        JoinExamples(db);

        Console.WriteLine("\n3. GROUPING & AGGREGATION");
        GroupingExamples(db);

        Console.WriteLine("\n4. PAGINATION");
        PaginationExamples(db);

        Console.WriteLine("\n5. DYNAMIC QUERIES");
        DynamicQueryExamples(db);

        Console.WriteLine("\n6. SUBQUERIES");
        SubqueryExamples(db);

        Console.WriteLine("\n=== Example completed successfully! ===");
    }

    static void InitializeDatabase(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE Categories (
                Id INTEGER PRIMARY KEY,
                Name TEXT NOT NULL,
                ParentId INTEGER
            );

            CREATE TABLE Products (
                Id INTEGER PRIMARY KEY,
                Name TEXT NOT NULL,
                Price REAL NOT NULL,
                Stock INTEGER NOT NULL,
                CategoryId INTEGER NOT NULL,
                SupplierId INTEGER NOT NULL,
                Rating REAL,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE Suppliers (
                Id INTEGER PRIMARY KEY,
                Name TEXT NOT NULL,
                Country TEXT NOT NULL,
                Email TEXT
            );

            CREATE TABLE Orders (
                Id INTEGER PRIMARY KEY,
                ProductId INTEGER NOT NULL,
                Quantity INTEGER NOT NULL,
                OrderDate TEXT NOT NULL,
                Status TEXT NOT NULL
            );

            -- Sample data
            INSERT INTO Categories VALUES
                (1, 'Electronics', NULL),
                (2, 'Computers', 1),
                (3, 'Phones', 1),
                (4, 'Books', NULL),
                (5, 'Programming', 4);

            INSERT INTO Suppliers VALUES
                (1, 'TechCorp', 'USA', 'contact@techcorp.com'),
                (2, 'BookWorld', 'UK', 'info@bookworld.co.uk'),
                (3, 'GlobalSupply', 'China', 'sales@globalsupply.cn');

            INSERT INTO Products VALUES
                (1, 'Laptop Dell', 1200, 15, 2, 1, 4.5, '2024-01-15'),
                (2, 'iPhone 15', 999, 50, 3, 1, 4.8, '2024-02-20'),
                (3, 'MacBook Pro', 2500, 8, 2, 1, 4.9, '2024-01-10'),
                (4, 'Clean Code', 45, 100, 5, 2, 4.7, '2024-03-01'),
                (5, 'Design Patterns', 55, 75, 5, 2, 4.6, '2024-03-05'),
                (6, 'Samsung Galaxy', 850, 30, 3, 3, 4.4, '2024-02-15');

            INSERT INTO Orders VALUES
                (1, 1, 2, '2024-03-01', 'Completed'),
                (2, 2, 1, '2024-03-02', 'Pending'),
                (3, 4, 3, '2024-03-02', 'Completed'),
                (4, 1, 1, '2024-03-03', 'Shipped'),
                (5, 5, 2, '2024-03-03', 'Completed');
        ";
        cmd.ExecuteNonQuery();
    }

    static void SqlBuilderExamples(Database db)
    {
        // Example 1: Basic SqlBuilder
        var builder = new SqlBuilder();
        builder.Select("Id, Name, Price")
               .From("Products")
               .Where("Price > @0", 100)
               .OrderBy("Price DESC");

        var products = db.Sql<Product>(builder.SQL, builder.Arguments).ToList();
        Console.WriteLine($"   ✓ Products over $100: {products.Count}");

        // Example 2: Multiple WHERE conditions
        builder = new SqlBuilder();
        builder.Select("*")
               .From("Products")
               .Where("Price > @0", 500)
               .Where("Stock < @0", 20)
               .Where("Rating >= @0", 4.5);

        products = db.Sql<Product>(builder.SQL, builder.Arguments).ToList();
        Console.WriteLine($"   ✓ Premium products with low stock: {products.Count}");
        foreach (var p in products)
        {
            Console.WriteLine($"     - {p.Name}: ${p.Price}, Stock: {p.Stock}, Rating: {p.Rating}");
        }

        // Example 3: OR conditions
        builder = new SqlBuilder();
        builder.Select("Name, Price, Stock")
               .From("Products")
               .Where("(CategoryId = @0 OR CategoryId = @1) AND Stock > @2", 2, 3, 10);

        products = db.Sql<Product>(builder.SQL, builder.Arguments).ToList();
        Console.WriteLine($"   ✓ Computers or Phones with stock > 10: {products.Count}");

        // Example 4: IN clause
        builder = new SqlBuilder();
        builder.Select("*")
               .From("Products")
               .Where("CategoryId IN (@0, @1, @2)", 2, 3, 5);

        products = db.Sql<Product>(builder.SQL, builder.Arguments).ToList();
        Console.WriteLine($"   ✓ Products in specific categories: {products.Count}");

        // Example 5: LIKE pattern
        builder = new SqlBuilder();
        builder.Select("Name, Price")
               .From("Products")
               .Where("Name LIKE @0", "%Mac%");

        products = db.Sql<Product>(builder.SQL, builder.Arguments).ToList();
        Console.WriteLine($"   ✓ Products matching 'Mac': {products.Count}");
    }

    static void JoinExamples(Database db)
    {
        // Example 1: INNER JOIN with SqlBuilder
        var builder = new SqlBuilder();
        builder.Select("p.Name AS ProductName, p.Price, c.Name AS CategoryName")
               .From("Products p")
               .InnerJoin("Categories c ON p.CategoryId = c.Id")
               .Where("p.Price > @0", 100)
               .OrderBy("p.Price DESC");

        var results = db.Sql<ProductWithCategory>(builder.SQL, builder.Arguments).ToList();
        Console.WriteLine($"   ✓ Products with categories (INNER JOIN): {results.Count}");
        foreach (var r in results)
        {
            Console.WriteLine($"     - {r.ProductName} ({r.CategoryName}): ${r.Price}");
        }

        // Example 2: Multiple JOINs
        builder = new SqlBuilder();
        builder.Select("p.Name, p.Price, c.Name AS CategoryName, s.Name AS SupplierName, s.Country")
               .From("Products p")
               .InnerJoin("Categories c ON p.CategoryId = c.Id")
               .InnerJoin("Suppliers s ON p.SupplierId = s.Id")
               .Where("s.Country = @0", "USA");

        var detailedResults = db.Sql<ProductDetail>(builder.SQL, builder.Arguments).ToList();
        Console.WriteLine($"   ✓ USA products with full details: {detailedResults.Count}");
        foreach (var r in detailedResults)
        {
            Console.WriteLine($"     - {r.Name} ({r.CategoryName}) from {r.SupplierName} ({r.Country})");
        }

        // Example 3: LEFT JOIN
        builder = new SqlBuilder();
        builder.Select("c.Name AS CategoryName, COUNT(p.Id) AS ProductCount")
               .From("Categories c")
               .LeftJoin("Products p ON c.Id = p.CategoryId")
               .GroupBy("c.Name")
               .Having("COUNT(p.Id) > @0", 0);

        var categoryStats = db.Sql<CategoryStats>(builder.SQL, builder.Arguments).ToList();
        Console.WriteLine($"   ✓ Categories with product counts: {categoryStats.Count}");
        foreach (var stat in categoryStats)
        {
            Console.WriteLine($"     - {stat.CategoryName}: {stat.ProductCount} products");
        }
    }

    static void GroupingExamples(Database db)
    {
        // Example 1: Simple GROUP BY
        var builder = new SqlBuilder();
        builder.Select("CategoryId, COUNT(*) AS Count, AVG(Price) AS AvgPrice, SUM(Stock) AS TotalStock")
               .From("Products")
               .GroupBy("CategoryId")
               .OrderBy("AvgPrice DESC");

        var stats = db.Sql<CategoryProductStats>(builder.SQL, builder.Arguments).ToList();
        Console.WriteLine($"   ✓ Category statistics:");
        foreach (var stat in stats)
        {
            Console.WriteLine($"     - Category {stat.CategoryId}: {stat.Count} products, Avg ${stat.AvgPrice:F2}, Stock: {stat.TotalStock}");
        }

        // Example 2: HAVING clause
        builder = new SqlBuilder();
        builder.Select("CategoryId, AVG(Price) AS AvgPrice")
               .From("Products")
               .GroupBy("CategoryId")
               .Having("AVG(Price) > @0", 100);

        var expensiveCategories = db.Sql<CategoryAverage>(builder.SQL, builder.Arguments).ToList();
        Console.WriteLine($"   ✓ Categories with avg price > $100: {expensiveCategories.Count}");

        // Example 3: Multiple aggregates
        builder = new SqlBuilder();
        builder.Select("CategoryId, MIN(Price) AS MinPrice, MAX(Price) AS MaxPrice, AVG(Price) AS AvgPrice")
               .From("Products")
               .GroupBy("CategoryId");

        var priceRanges = db.Sql<PriceRange>(builder.SQL, builder.Arguments).ToList();
        Console.WriteLine($"   ✓ Price ranges by category:");
        foreach (var range in priceRanges)
        {
            Console.WriteLine($"     - Category {range.CategoryId}: ${range.MinPrice:F2} - ${range.MaxPrice:F2} (avg ${range.AvgPrice:F2})");
        }
    }

    static void PaginationExamples(Database db)
    {
        const int pageSize = 2;

        // Example 1: Page 1
        var builder = new SqlBuilder();
        builder.Select("*")
               .From("Products")
               .OrderBy("Name")
               .Limit(pageSize)
               .Offset(0);

        var page1 = db.Sql<Product>(builder.SQL, builder.Arguments).ToList();
        Console.WriteLine($"   ✓ Page 1 (items {1}-{page1.Count}):");
        foreach (var p in page1)
        {
            Console.WriteLine($"     - {p.Name}");
        }

        // Example 2: Page 2
        builder = new SqlBuilder();
        builder.Select("*")
               .From("Products")
               .OrderBy("Name")
               .Limit(pageSize)
               .Offset(pageSize);

        var page2 = db.Sql<Product>(builder.SQL, builder.Arguments).ToList();
        Console.WriteLine($"   ✓ Page 2 (items {pageSize + 1}-{pageSize + page2.Count}):");
        foreach (var p in page2)
        {
            Console.WriteLine($"     - {p.Name}");
        }

        // Example 3: Get total count for pagination
        var totalCount = db.Sql<int>("SELECT COUNT(*) FROM Products").Single();
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        Console.WriteLine($"   ✓ Total: {totalCount} products, {totalPages} pages");
    }

    static void DynamicQueryExamples(Database db)
    {
        // Example 1: Dynamic WHERE conditions
        var builder = new SqlBuilder();
        builder.Select("*").From("Products");

        string? nameFilter = "iPhone";
        decimal? minPrice = 500m;
        int? categoryId = null;

        if (!string.IsNullOrEmpty(nameFilter))
        {
            builder.Where("Name LIKE @0", $"%{nameFilter}%");
        }
        if (minPrice.HasValue)
        {
            builder.Where("Price >= @0", minPrice.Value);
        }
        if (categoryId.HasValue)
        {
            builder.Where("CategoryId = @0", categoryId.Value);
        }

        var products = db.Sql<Product>(builder.SQL, builder.Arguments).ToList();
        Console.WriteLine($"   ✓ Dynamic filter results: {products.Count} products");

        // Example 2: Dynamic ORDER BY
        string sortBy = "Price";
        string sortOrder = "DESC";

        builder = new SqlBuilder();
        builder.Select("Name, Price, Stock")
               .From("Products")
               .OrderBy($"{sortBy} {sortOrder}");

        products = db.Sql<Product>(builder.SQL, builder.Arguments).ToList();
        Console.WriteLine($"   ✓ Sorted by {sortBy} {sortOrder}:");
        foreach (var p in products.Take(3))
        {
            Console.WriteLine($"     - {p.Name}: ${p.Price}");
        }

        // Example 3: Dynamic column selection
        var columns = new[] { "Name", "Price", "Rating" };
        builder = new SqlBuilder();
        builder.Select(string.Join(", ", columns))
               .From("Products")
               .Where("Rating IS NOT NULL");

        products = db.Sql<Product>(builder.SQL, builder.Arguments).ToList();
        Console.WriteLine($"   ✓ Selected columns dynamically: {products.Count} results");
    }

    static void SubqueryExamples(Database db)
    {
        // Example 1: Subquery in WHERE
        var builder = new SqlBuilder();
        builder.Select("*")
               .From("Products")
               .Where("Price > (SELECT AVG(Price) FROM Products)");

        var aboveAverage = db.Sql<Product>(builder.SQL, builder.Arguments).ToList();
        Console.WriteLine($"   ✓ Products above average price: {aboveAverage.Count}");

        // Example 2: Subquery with IN
        builder = new SqlBuilder();
        builder.Select("*")
               .From("Products")
               .Where("CategoryId IN (SELECT Id FROM Categories WHERE ParentId IS NOT NULL)");

        var subCategoryProducts = db.Sql<Product>(builder.SQL, builder.Arguments).ToList();
        Console.WriteLine($"   ✓ Products in subcategories: {subCategoryProducts.Count}");

        // Example 3: Correlated subquery
        var sql = @"
            SELECT p.Name, p.Price,
                   (SELECT COUNT(*) FROM Orders o WHERE o.ProductId = p.Id) AS OrderCount
            FROM Products p
            ORDER BY OrderCount DESC
        ";
        
        var productsWithOrders = db.Sql<ProductOrderStats>(sql).ToList();
        Console.WriteLine($"   ✓ Products with order counts:");
        foreach (var p in productsWithOrders.Take(5))
        {
            Console.WriteLine($"     - {p.Name}: {p.OrderCount} orders");
        }
    }
}

// DTOs for query results
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public int CategoryId { get; set; }
    public int SupplierId { get; set; }
    public double? Rating { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ProductWithCategory
{
    public string ProductName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string CategoryName { get; set; } = string.Empty;
}

public class ProductDetail
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class CategoryStats
{
    public string CategoryName { get; set; } = string.Empty;
    public int ProductCount { get; set; }
}

public class CategoryProductStats
{
    public int CategoryId { get; set; }
    public int Count { get; set; }
    public decimal AvgPrice { get; set; }
    public int TotalStock { get; set; }
}

public class CategoryAverage
{
    public int CategoryId { get; set; }
    public decimal AvgPrice { get; set; }
}

public class PriceRange
{
    public int CategoryId { get; set; }
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
    public decimal AvgPrice { get; set; }
}

public class ProductOrderStats
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int OrderCount { get; set; }
}
