using AnubisWorks.SQLFactory;
using AnubisWorks.SQLFactory.ChangeTracking;
using AnubisWorks.SQLFactory.Caching;
using AnubisWorks.SQLFactory.SoftDelete;
using Microsoft.Data.Sqlite;
using System.Diagnostics;

namespace SQLFactory.Examples.FullStackApp;

/// <summary>
/// Example: Complete E-Commerce Application
/// Demonstrates: Integration of ALL SQLFactory features in production-ready scenario
/// Features: CRUD, SqlBuilder, Include/ThenInclude, Change Tracking, Soft Delete,
///           Global Filters, Caching, Bulk Operations, Transactions
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║       SQLFactory Full-Stack E-Commerce Application            ║");
        Console.WriteLine("║       Demonstrating ALL Features in Production Scenario       ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝\n");

        var app = new ECommerceApp();
        app.Run();
    }
}

public class ECommerceApp
{
    private readonly Database _db;
    private int _currentTenantId = 1;
    private string _currentUserId = "user123";

    public ECommerceApp()
    {
        _db = InitializeDatabase();
        ConfigureGlobalSettings();
    }

    public void Run()
    {
        Console.WriteLine("🚀 Starting E-Commerce Application...\n");

        // Demo scenarios
        ShowProductCatalog();
        CreateNewOrder();
        UpdateInventory();
        CustomerManagement();
        AdminOperations();
        PerformanceOptimizations();
        ReportingAndAnalytics();

        Console.WriteLine("\n✅ E-Commerce Application Demo Completed Successfully!");
        Console.WriteLine("\n═══════════════════════════════════════════════════════════════");
        Console.WriteLine("Features Demonstrated:");
        Console.WriteLine("  ✓ CRUD Operations");
        Console.WriteLine("  ✓ SqlBuilder (Fluent Queries)");
        Console.WriteLine("  ✓ Eager Loading (Include/ThenInclude)");
        Console.WriteLine("  ✓ Change Tracking & SaveChanges()");
        Console.WriteLine("  ✓ Soft Delete Pattern");
        Console.WriteLine("  ✓ Global Query Filters (Multi-tenancy)");
        Console.WriteLine("  ✓ Query Result Caching");
        Console.WriteLine("  ✓ Bulk Operations");
        Console.WriteLine("  ✓ Transaction Management");
        Console.WriteLine("  ✓ Complex Joins & Aggregations");
        Console.WriteLine("═══════════════════════════════════════════════════════════════\n");
    }

    private Database InitializeDatabase()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE Tenants (
                Id INTEGER PRIMARY KEY,
                Name TEXT NOT NULL
            );

            CREATE TABLE Categories (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                TenantId INTEGER NOT NULL,
                IsDeleted INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE Products (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Description TEXT,
                Price REAL NOT NULL,
                Stock INTEGER NOT NULL,
                CategoryId INTEGER NOT NULL,
                TenantId INTEGER NOT NULL,
                IsDeleted INTEGER NOT NULL DEFAULT 0,
                DeletedAt TEXT NULL,
                FOREIGN KEY (CategoryId) REFERENCES Categories(Id)
            );

            CREATE TABLE Customers (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Email TEXT NOT NULL,
                Name TEXT NOT NULL,
                TenantId INTEGER NOT NULL,
                IsDeleted INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE Orders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                OrderNumber TEXT NOT NULL,
                CustomerId INTEGER NOT NULL,
                OrderDate TEXT NOT NULL,
                TotalAmount REAL NOT NULL,
                Status TEXT NOT NULL,
                TenantId INTEGER NOT NULL,
                FOREIGN KEY (CustomerId) REFERENCES Customers(Id)
            );

            CREATE TABLE OrderItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                OrderId INTEGER NOT NULL,
                ProductId INTEGER NOT NULL,
                Quantity INTEGER NOT NULL,
                UnitPrice REAL NOT NULL,
                FOREIGN KEY (OrderId) REFERENCES Orders(Id),
                FOREIGN KEY (ProductId) REFERENCES Products(Id)
            );

            -- Seed data
            INSERT INTO Tenants VALUES (1, 'TechStore'), (2, 'FashionHub');
            
            INSERT INTO Categories (Name, TenantId) VALUES
                ('Electronics', 1), ('Accessories', 1),
                ('Clothing', 2), ('Shoes', 2);

            INSERT INTO Products (Name, Description, Price, Stock, CategoryId, TenantId) VALUES
                ('Laptop Pro', 'High-performance laptop', 1299.99, 50, 1, 1),
                ('Wireless Mouse', 'Ergonomic mouse', 29.99, 200, 2, 1),
                ('USB-C Cable', 'Fast charging cable', 12.99, 500, 2, 1),
                ('Designer Shirt', 'Premium cotton shirt', 79.99, 100, 3, 2),
                ('Running Shoes', 'Professional athletic shoes', 129.99, 75, 4, 2);

            INSERT INTO Customers (Email, Name, TenantId) VALUES
                ('john@example.com', 'John Doe', 1),
                ('jane@example.com', 'Jane Smith', 1),
                ('alice@example.com', 'Alice Johnson', 2);
        ";
        cmd.ExecuteNonQuery();

        Console.WriteLine("✓ Database initialized with sample data\n");
        return new Database(conn);
    }

    private void ConfigureGlobalSettings()
    {
        // Enable Change Tracking
        _db.EnableChangeTracking();

        // Global Filters: Multi-tenancy + Soft Delete
        _db.AddGlobalFilter<Product>(p => p.TenantId == _currentTenantId && !p.IsDeleted);
        _db.AddGlobalFilter<Category>(c => c.TenantId == _currentTenantId && !c.IsDeleted);
        _db.AddGlobalFilter<Customer>(c => c.TenantId == _currentTenantId && !c.IsDeleted);
        _db.AddGlobalFilter<Order>(o => o.TenantId == _currentTenantId);

        // Configure Caching
        _db.ConfigureCache(maxSize: 100);

        Console.WriteLine("✓ Global settings configured:");
        Console.WriteLine($"  • Tenant ID: {_currentTenantId}");
        Console.WriteLine($"  • Change Tracking: Enabled");
        Console.WriteLine($"  • Soft Delete Filter: Active");
        Console.WriteLine($"  • Cache Size: 100 entries\n");
    }

    private void ShowProductCatalog()
    {
        Console.WriteLine("📦 PRODUCT CATALOG (Eager Loading + Caching)");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");

        // Eager loading with Include + Caching
        var sw = Stopwatch.StartNew();
        var products = _db.Query<Product>()
                          .Include(p => p.Category)
                          .Cacheable(TimeSpan.FromMinutes(5))
                          .ToList();
        sw.Stop();

        Console.WriteLine($"Loaded {products.Count} products in {sw.ElapsedMilliseconds}ms (first query)\n");

        foreach (var product in products)
        {
            Console.WriteLine($"  • {product.Name}");
            Console.WriteLine($"    Category: {product.Category.Name}");
            Console.WriteLine($"    Price: ${product.Price:F2} | Stock: {product.Stock}");
            Console.WriteLine($"    {product.Description}\n");
        }

        // Second query - cache hit
        sw.Restart();
        var cachedProducts = _db.Query<Product>()
                                .Include(p => p.Category)
                                .Cacheable(TimeSpan.FromMinutes(5))
                                .ToList();
        sw.Stop();
        Console.WriteLine($"✓ Cache hit: {cachedProducts.Count} products in {sw.ElapsedMilliseconds}ms (instant!)\n");
    }

    private void CreateNewOrder()
    {
        Console.WriteLine("🛒 CREATE NEW ORDER (Transactions + Change Tracking)");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");

        _db.BeginTransaction();
        try
        {
            // Create order
            var order = new Order
            {
                OrderNumber = $"ORD-{DateTime.Now:yyyyMMddHHmmss}",
                CustomerId = 1,
                OrderDate = DateTime.UtcNow,
                Status = "Pending",
                TenantId = _currentTenantId,
                TotalAmount = 0
            };
            _db.Insert(order);
            Console.WriteLine($"✓ Order created: {order.OrderNumber}");

            // Add order items
            var items = new List<OrderItem>
            {
                new OrderItem { OrderId = order.Id, ProductId = 1, Quantity = 1, UnitPrice = 1299.99m },
                new OrderItem { OrderId = order.Id, ProductId = 2, Quantity = 2, UnitPrice = 29.99m }
            };

            foreach (var item in items)
            {
                _db.Insert(item);
                
                // Update inventory
                var product = _db.Single<Product>("SELECT * FROM Products WHERE Id = @0", item.ProductId);
                product.Stock -= item.Quantity;
                _db.Update(product);
                
                order.TotalAmount += item.Quantity * item.UnitPrice;
                Console.WriteLine($"  • Added: {product.Name} x{item.Quantity} = ${item.Quantity * item.UnitPrice:F2}");
            }

            // Update order total
            _db.Update(order);
            Console.WriteLine($"\n✓ Order Total: ${order.TotalAmount:F2}");

            _db.Commit();
            Console.WriteLine("✓ Transaction committed successfully\n");

            // Clear cache (inventory changed)
            _db.ClearCache<Product>();
        }
        catch (Exception ex)
        {
            _db.Rollback();
            Console.WriteLine($"✗ Transaction rolled back: {ex.Message}\n");
        }
    }

    private void UpdateInventory()
    {
        Console.WriteLine("📊 BULK INVENTORY UPDATE (Bulk Operations)");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");

        // Load low-stock products
        var lowStockProducts = _db.Query<Product>()
                                  .Where(p => p.Stock < 100)
                                  .ToList();
        Console.WriteLine($"Found {lowStockProducts.Count} products with low stock\n");

        // Bulk restock
        foreach (var product in lowStockProducts)
        {
            product.Stock += 50; // Add 50 units
            Console.WriteLine($"  • Restocking {product.Name}: +50 units");
        }

        var sw = Stopwatch.StartNew();
        _db.BulkUpdate(lowStockProducts);
        sw.Stop();

        Console.WriteLine($"\n✓ Bulk updated {lowStockProducts.Count} products in {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"✓ Throughput: {lowStockProducts.Count / (sw.ElapsedMilliseconds / 1000.0):F0} records/sec\n");
    }

    private void CustomerManagement()
    {
        Console.WriteLine("👥 CUSTOMER MANAGEMENT (Soft Delete + Global Filters)");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");

        // Active customers (filtered automatically)
        var customers = _db.Query<Customer>("SELECT * FROM Customers").ToList();
        Console.WriteLine($"Active customers (tenant {_currentTenantId}): {customers.Count}");

        foreach (var customer in customers)
        {
            Console.WriteLine($"  • {customer.Name} ({customer.Email})");
        }

        // Soft delete a customer
        var customer1 = customers.First();
        customer1.IsDeleted = true;
        customer1.DeletedAt = DateTime.UtcNow;
        _db.Update(customer1);
        Console.WriteLine($"\n✓ Soft deleted: {customer1.Name}");

        // Query again - automatically excluded
        var activeCustomers = _db.Query<Customer>("SELECT * FROM Customers").ToList();
        Console.WriteLine($"✓ Active customers after deletion: {activeCustomers.Count}\n");

        // Admin view - see all (ignore filters)
        var allCustomers = _db.Query<Customer>()
                              .IgnoreQueryFilters()
                              .ToList();
        Console.WriteLine($"Admin view (all customers): {allCustomers.Count}");
    }

    private void AdminOperations()
    {
        Console.WriteLine("\n🔐 ADMIN OPERATIONS (Complex Queries)");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");

        // SqlBuilder for complex reporting
        var query = new SqlBuilder()
            .Select("c.Name AS Category, COUNT(p.Id) AS ProductCount, AVG(p.Price) AS AvgPrice, SUM(p.Stock) AS TotalStock")
            .From("Products p")
            .InnerJoin("Categories c ON p.CategoryId = c.Id")
            .Where("p.IsDeleted = 0")
            .GroupBy("c.Name")
            .OrderBy("ProductCount DESC");

        var report = _db.Query<CategoryReport>(query.ToSql()).ToList();

        Console.WriteLine("Category Performance Report:\n");
        Console.WriteLine("  Category         | Products | Avg Price | Total Stock");
        Console.WriteLine("  ────────────────────────────────────────────────────────");
        
        foreach (var item in report)
        {
            Console.WriteLine($"  {item.Category,-16} | {item.ProductCount,8} | ${item.AvgPrice,8:F2} | {item.TotalStock,11}");
        }

        // Orders summary with JOIN
        var ordersSummary = _db.Sql<OrderSummary>(@"
            SELECT 
                o.OrderNumber,
                c.Name AS CustomerName,
                o.OrderDate,
                o.TotalAmount,
                COUNT(oi.Id) AS ItemCount
            FROM Orders o
            INNER JOIN Customers c ON o.CustomerId = c.Id
            LEFT JOIN OrderItems oi ON o.Id = oi.OrderId
            WHERE o.TenantId = @0
            GROUP BY o.Id, o.OrderNumber, c.Name, o.OrderDate, o.TotalAmount
            ORDER BY o.OrderDate DESC
        ", _currentTenantId).ToList();

        Console.WriteLine($"\n\nRecent Orders ({ordersSummary.Count}):\n");
        foreach (var order in ordersSummary)
        {
            Console.WriteLine($"  {order.OrderNumber} | {order.CustomerName}");
            Console.WriteLine($"    ${order.TotalAmount:F2} | {order.ItemCount} items | {order.OrderDate:yyyy-MM-dd}");
        }
    }

    private void PerformanceOptimizations()
    {
        Console.WriteLine("\n\n⚡ PERFORMANCE OPTIMIZATIONS");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");

        // Pagination
        const int pageSize = 2;
        var page1 = _db.Query<Product>()
                       .OrderBy(p => p.Name)
                       .Skip(0)
                       .Take(pageSize)
                       .ToList();
        Console.WriteLine($"✓ Pagination: Page 1 ({page1.Count} items)");

        // Aggregates without loading entities
        var totalProducts = _db.ExecuteScalar<int>("SELECT COUNT(*) FROM Products WHERE TenantId = @0", _currentTenantId);
        var avgPrice = _db.ExecuteScalar<decimal>("SELECT AVG(Price) FROM Products WHERE TenantId = @0", _currentTenantId);
        Console.WriteLine($"✓ Aggregates: {totalProducts} products, Avg price: ${avgPrice:F2}");

        // Exists check (efficient)
        var hasLowStock = _db.ExecuteScalar<int>("SELECT EXISTS(SELECT 1 FROM Products WHERE Stock < 10)") == 1;
        Console.WriteLine($"✓ Existence check: Low stock alert = {hasLowStock}");
    }

    private void ReportingAndAnalytics()
    {
        Console.WriteLine("\n\n📈 REPORTING & ANALYTICS");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");

        // Revenue by category
        var revenueReport = _db.Sql<RevenueReport>(@"
            SELECT 
                c.Name AS Category,
                SUM(oi.Quantity * oi.UnitPrice) AS Revenue,
                COUNT(DISTINCT o.Id) AS OrderCount,
                SUM(oi.Quantity) AS UnitsSold
            FROM OrderItems oi
            INNER JOIN Products p ON oi.ProductId = p.Id
            INNER JOIN Categories c ON p.CategoryId = c.Id
            INNER JOIN Orders o ON oi.OrderId = o.Id
            WHERE o.TenantId = @0
            GROUP BY c.Name
            ORDER BY Revenue DESC
        ", _currentTenantId).ToList();

        Console.WriteLine("Revenue by Category:\n");
        Console.WriteLine("  Category         | Revenue     | Orders | Units Sold");
        Console.WriteLine("  ─────────────────────────────────────────────────────");
        
        decimal totalRevenue = 0;
        foreach (var item in revenueReport)
        {
            Console.WriteLine($"  {item.Category,-16} | ${item.Revenue,10:F2} | {item.OrderCount,6} | {item.UnitsSold,10}");
            totalRevenue += item.Revenue;
        }
        Console.WriteLine($"\n  Total Revenue: ${totalRevenue:F2}");
    }
}

// Domain Entities
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public int CategoryId { get; set; }
    public int TenantId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public virtual Category Category { get; set; } = null!;
}

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TenantId { get; set; }
    public bool IsDeleted { get; set; }
}

public class Customer
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int TenantId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public class Order
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public int TenantId { get; set; }
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

// DTOs for Reports
public class CategoryReport
{
    public string Category { get; set; } = string.Empty;
    public int ProductCount { get; set; }
    public decimal AvgPrice { get; set; }
    public int TotalStock { get; set; }
}

public class OrderSummary
{
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
}

public class RevenueReport
{
    public string Category { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int OrderCount { get; set; }
    public int UnitsSold { get; set; }
}
