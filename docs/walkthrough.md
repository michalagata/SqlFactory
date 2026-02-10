# SQLFactory Complete Walkthrough

**Version:** 1.0.0 (Production Ready)  
**Target Framework:** .NET 10.0 (with multi-targeting support)  
**Test Coverage:** 60.21% Core, 392 passing tests

---

## Table of Contents

1. [Introduction](#introduction)
2. [Quick Start](#quick-start)
3. [Core Features](#core-features)
4. [Advanced Features](#advanced-features)
5. [Performance & Optimization](#performance--optimization)
6. [Best Practices](#best-practices)
7. [Migration Guide](#migration-guide)

---

## Introduction

SQLFactory is a lightweight, high-performance micro-ORM for .NET that provides a clean, fluent API for database operations. It combines the simplicity of Dapper with advanced features inspired by Entity Framework Core, while maintaining minimal overhead and maximum control.

### Key Highlights

- ✅ **Zero Configuration**: Works out of the box with attribute-based mapping
- ✅ **High Performance**: Minimal overhead, optimized for speed
- ✅ **Advanced Features**: Include/ThenInclude, Global Filters, Soft Delete, Caching
- ✅ **Multi-Database**: SQL Server, PostgreSQL, MySQL, SQLite support
- ✅ **Async/Await**: Full async support throughout
- ✅ **Production Ready**: 392 passing tests, battle-tested

---

## Quick Start

### Installation

```bash
dotnet add package SQLFactory
```

### Basic Setup

```csharp
using AnubisWorks.SQLFactory;

// Define your entity
public class Product {
    [Column(IsPrimaryKey = true, IsDbGenerated = true)]
    public int ProductID { get; set; }
    
    public string ProductName { get; set; }
    public decimal? UnitPrice { get; set; }
    public bool Discontinued { get; set; }
}

// Create database connection
var db = new Database("Data Source=northwind.db", "Microsoft.Data.Sqlite");

// Query data
var products = db.From<Product>()
    .Where(p => p.UnitPrice > 10)
    .OrderBy(p => p.ProductName)
    .ToList();

// Insert
var newProduct = new Product { 
    ProductName = "Chai", 
    UnitPrice = 18.00m 
};
db.Table<Product>().Add(newProduct);

// Update
product.UnitPrice = 20.00m;
db.Table<Product>().Update(product);

// Delete
db.Table<Product>().Remove(product);
```

---

## Core Features

### 1. CRUD Operations

#### Insert
```csharp
var product = new Product { ProductName = "Chai", UnitPrice = 18.00m };

// Single insert
db.Table<Product>().Add(product);
// Product ID is automatically populated

// Bulk insert
var products = new[] { product1, product2, product3 };
db.Table<Product>().AddRange(products);
```

#### Query
```csharp
// All records
var all = db.From<Product>().ToList();

// Filtered
var expensive = db.From<Product>()
    .Where(p => p.UnitPrice > 50)
    .ToList();

// Single record
var product = db.From<Product>()
    .Where(p => p.ProductID == 1)
    .FirstOrDefault();

// Find by primary key
var product = db.Table<Product>().Find(1);

// Count
var count = db.From<Product>()
    .Where(p => p.Discontinued)
    .Count();

// Exists
var hasExpensive = db.From<Product>()
    .Where(p => p.UnitPrice > 100)
    .Any();
```

#### Update
```csharp
var product = db.Table<Product>().Find(1);
product.UnitPrice = 25.00m;
product.ProductName = "Updated Name";

db.Table<Product>().Update(product);
```

#### Delete
```csharp
var product = db.Table<Product>().Find(1);
db.Table<Product>().Remove(product);

// Bulk delete
var discontinued = db.From<Product>()
    .Where(p => p.Discontinued)
    .ToList();
db.Table<Product>().RemoveRange(discontinued);
```

### 2. Async Operations

All operations have async counterparts:

```csharp
// Query
var products = await db.From<Product>()
    .Where(p => p.UnitPrice > 10)
    .ToListAsync();

// Count
var count = await db.From<Product>()
    .Where(p => p.Discontinued)
    .CountAsync();

// Single record
var product = await db.From<Product>()
    .Where(p => p.ProductID == 1)
    .FirstOrDefaultAsync();

// Execute
await db.SqlExecuteAsync("UPDATE Products SET UnitPrice = UnitPrice * 1.1");
```

### 3. SQL Builder

For complex queries, use the fluent SQL builder:

```csharp
var query = SQL.SELECT("p.ProductName, c.CategoryName")
    .FROM("Products p")
    .JOIN("Categories c ON p.CategoryID = c.CategoryID")
    .WHERE("p.UnitPrice > {0}", 50)
    .WHERE("p.Discontinued = {0}", false)
    .ORDER_BY("p.ProductName");

var results = db.Map(query, r => new {
    ProductName = r.GetString("ProductName"),
    CategoryName = r.GetString("CategoryName")
}).ToList();
```

### 4. Transactions

```csharp
// Automatic transaction
using (var trans = db.EnsureInTransaction()) {
    db.Table<Product>().Add(product1);
    db.Table<Category>().Add(category);
    
    trans.Commit();
    // If Commit() is not called, transaction rolls back automatically
}

// Manual transaction management
db.Transaction = db.Connection.BeginTransaction();
try {
    db.Table<Product>().Add(product);
    db.Table<Order>().Add(order);
    
    db.Transaction.Commit();
} catch {
    db.Transaction.Rollback();
    throw;
}
```

---

## Advanced Features

### 1. Eager Loading (Include / ThenInclude)

Load related entities in a single query to avoid N+1 problems:

```csharp
// Single navigation property
var products = await db.From<Product>()
    .Include(p => p.Category)
    .ToListAsync();

// Collection navigation
var categories = await db.From<Category>()
    .Include(c => c.Products)
    .ToListAsync();

// Multi-level (ThenInclude)
var orders = await db.From<Order>()
    .Include(o => o.Customer)
        .ThenInclude(c => c.Orders)  // Customer's other orders
    .Include(o => o.OrderDetails)
        .ThenInclude(od => od.Product)
    .ToListAsync();
```

**Performance Note:** Uses split queries (separate SELECT statements) to avoid cartesian explosion.

### 2. Pagination

```csharp
// Manual pagination
var page = db.From<Product>()
    .OrderBy(p => p.ProductName)
    .Skip(20)
    .Take(10)
    .ToList();

// Helper method with metadata
var pagedResult = db.From<Product>()
    .Where(p => !p.Discontinued)
    .OrderBy(p => p.ProductName)
    .ToPagedList(pageNumber: 3, pageSize: 20);

Console.WriteLine($"Page {pagedResult.CurrentPage} of {pagedResult.TotalPages}");
Console.WriteLine($"Total: {pagedResult.TotalCount} items");

foreach (var product in pagedResult.Items) {
    Console.WriteLine(product.ProductName);
}
```

### 3. Global Query Filters

Automatically filter queries (soft delete, multi-tenancy):

```csharp
// Register a soft delete filter
GlobalFilterManager.Register(new SoftDeleteFilter<Product>());

// All queries automatically exclude deleted products
var products = db.From<Product>().ToList(); // WHERE IsDeleted = 0

// Admin view - include deleted
var allProducts = db.From<Product>()
    .IgnoreQueryFilters()
    .ToList();

// Custom filter example
public class SoftDeleteFilter<TEntity> : IGlobalFilter<TEntity>
    where TEntity : ISoftDeletable
{
    public string FilterName => "SoftDelete";
    public bool IsEnabled => true;
    
    public Expression<Func<TEntity, bool>> GetFilter() {
        return entity => !entity.IsDeleted;
    }
}
```

### 4. Soft Delete

```csharp
// Mark entity as deleted (IsDeleted = true)
db.Table<Product>().Extension("SoftDelete").SoftDelete(product);

// Permanent delete
db.Table<Product>().Extension("HardDelete").HardDelete(product);

// Query deleted records
var deleted = db.From<Product>()
    .Extension("OnlyDeleted")
    .OnlyDeleted()
    .ToList();

// Restore deleted record
db.Table<Product>().Extension("Restore").Restore(product);
```

### 5. Optimistic Concurrency

```csharp
public class Product {
    public int ProductID { get; set; }
    public string ProductName { get; set; }
    
    [RowVersion]
    public byte[] RowVersion { get; set; }
}

// Update with concurrency check
try {
    product.ProductName = "Updated";
    db.Table<Product>().Update(product);
} catch (DbConcurrencyException ex) {
    // Handle conflict
    Console.WriteLine("Record was modified by another user");
    
    // Refresh from database
    var current = db.Table<Product>().Find(product.ProductID);
    
    // Merge changes or ask user
}
```

### 6. Query Result Caching

```csharp
// Cache query results for 5 minutes
var products = db.From<Product>()
    .Where(p => p.CategoryID == 1)
    .Cacheable(TimeSpan.FromMinutes(5))
    .ToList();

// Subsequent calls return cached data
var sameProducts = db.From<Product>()
    .Where(p => p.CategoryID == 1)
    .Cacheable(TimeSpan.FromMinutes(5))
    .ToList(); // Retrieved from cache

// Clear cache for entity type
db.Cache.Clear<Product>();

// Clear entire cache
db.Cache.ClearAll();
```

### 7. JSON Column Support

```csharp
public class Product {
    public int ProductID { get; set; }
    public string ProductName { get; set; }
    
    [JsonColumn]
    public ProductMetadata Metadata { get; set; }  // Serialized as JSON
}

public class ProductMetadata {
    public string[] Tags { get; set; }
    public Dictionary<string, string> CustomFields { get; set; }
}

// Automatic serialization/deserialization
var product = new Product {
    ProductName = "Laptop",
    Metadata = new ProductMetadata {
        Tags = new[] { "electronics", "computers" },
        CustomFields = new Dictionary<string, string> {
            ["Brand"] = "Dell",
            ["Model"] = "XPS 15"
        }
    }
};
db.Table<Product>().Add(product);

// Query and use
var loaded = db.Table<Product>().Find(product.ProductID);
Console.WriteLine(loaded.Metadata.Tags[0]); // "electronics"
```

### 8. Upsert (Insert or Update)

```csharp
// Insert if new, update if exists
var product = new Product { 
    ProductID = 5,  // Existing ID
    ProductName = "Updated Product",
    UnitPrice = 30.00m
};

db.Table<Product>().InsertOrUpdate(product);
// Automatically detects if record exists and updates, otherwise inserts

// Bulk upsert
var products = new[] { product1, product2, product3 };
db.Table<Product>().InsertOrUpdateRange(products);
```

### 9. Fluent Configuration

Alternative to attributes:

```csharp
public class NorthwindDatabase : Database {
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.Entity<Product>(entity => {
            entity.ToTable("Products");
            entity.HasKey(p => p.ProductID);
            
            entity.Property(p => p.ProductID)
                .IsDbGenerated();
            
            entity.Property(p => p.ProductName)
                .IsRequired()
                .HasMaxLength(40);
            
            entity.Property(p => p.UnitPrice)
                .HasPrecision(19, 4);
        });
    }
}
```

### 10. CRUD Interceptors (AOP)

```csharp
// Define an interceptor
public class AuditInterceptor<TEntity> : ICrudInterceptor<TEntity>
    where TEntity : IAuditable
{
    public string InterceptorName => "Audit";
    public int Order => 0;
    
    public void OnInserting(TEntity entity, CrudContext context) {
        entity.CreatedAt = DateTime.UtcNow;
        entity.CreatedBy = context.CurrentUserId;
    }
    
    public void OnUpdating(TEntity entity, CrudContext context) {
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = context.CurrentUserId;
    }
    
    // Implement other methods...
}

// Register
InterceptorManager.Register(new AuditInterceptor<Product>());

// All inserts/updates automatically set audit fields
db.Table<Product>().Add(product); // CreatedAt/CreatedBy set automatically
```

### 11. Repository Pattern

```csharp
// Get repository
var productRepo = db.GetRepository<Product>();

// Use repository methods
productRepo.Insert(product);
var all = productRepo.GetAll();
var product = productRepo.Get(1);
productRepo.Update(product);
productRepo.Delete(product);

// Advanced queries via Table property
var filtered = productRepo.Table
    .Where(p => p.UnitPrice > 50)
    .OrderBy(p => p.ProductName)
    .ToList();
```

---

## Performance & Optimization

### 1. Batch Operations

```csharp
// Bulk insert (much faster than individual inserts)
var products = new List<Product>();
for (int i = 0; i < 10000; i++) {
    products.Add(new Product { ProductName = $"Product {i}" });
}

db.Table<Product>().AddRange(products);
// Uses optimized bulk insert (BULK INSERT for SQL Server, COPY for PostgreSQL)
```

### 2. Provider-Specific Optimizations

SQLFactory automatically uses provider-optimized bulk operations:

- **SQL Server**: `SqlBulkCopy` for bulk inserts
- **PostgreSQL**: `COPY` command for bulk inserts
- **SQLite**: Transaction batching for maximum speed
- **MySQL**: Optimized batch inserts

### 3. Query Optimization

```csharp
// Bad: N+1 query problem
var categories = db.From<Category>().ToList();
foreach (var cat in categories) {
    cat.Products = db.From<Product>()
        .Where(p => p.CategoryID == cat.CategoryID)
        .ToList(); // N queries!
}

// Good: Single query with Include
var categories = db.From<Category>()
    .Include(c => c.Products)
    .ToList(); // 2 queries total (split query)

// Best: For read-only scenarios, use JOIN + projection
var results = db.Map(
    SQL.SELECT("c.CategoryName, p.ProductName")
        .FROM("Categories c")
        .JOIN("Products p ON c.CategoryID = p.CategoryID"),
    r => new {
        Category = r.GetString("CategoryName"),
        Product = r.GetString("ProductName")
    }
).ToList(); // 1 query, minimal memory
```

### 4. Async for Scalability

```csharp
// Use async for I/O-bound operations
public async Task<List<Product>> GetProductsAsync(int categoryId) {
    return await db.From<Product>()
        .Where(p => p.CategoryID == categoryId)
        .ToListAsync();
}

// Parallel queries
var categoriesTask = db.From<Category>().ToListAsync();
var suppliersTask = db.From<Supplier>().ToListAsync();

await Task.WhenAll(categoriesTask, suppliersTask);

var categories = await categoriesTask;
var suppliers = await suppliersTask;
```

---

## Best Practices

### 1. Connection Management

```csharp
// ✅ Good: Use using statement
using (var db = new Database(connectionString)) {
    var products = db.From<Product>().ToList();
} // Connection closed automatically

// ✅ Good: Reuse Database instance (singleton)
public class DataAccess {
    private readonly Database _db;
    
    public DataAccess(string connectionString) {
        _db = new Database(connectionString);
    }
    
    public List<Product> GetProducts() {
        return _db.From<Product>().ToList();
    }
}

// ❌ Bad: Creating Database for every operation
for (int i = 0; i < 100; i++) {
    using (var db = new Database(connectionString)) {
        db.Table<Product>().Add(products[i]);
    } // Creates 100 connections!
}
```

### 2. Transaction Scope

```csharp
// ✅ Good: Explicit transactions for multiple operations
using (var db = new Database(connectionString))
using (var trans = db.EnsureInTransaction()) {
    db.Table<Product>().Add(product);
    db.Table<Category>().Add(category);
    db.Table<OrderDetail>().Add(orderDetail);
    
    trans.Commit();
}

// ❌ Bad: No transaction for related operations
db.Table<Product>().Add(product);
db.Table<Category>().Add(category); // If this fails, product is orphaned!
```

### 3. Projection for Large Datasets

```csharp
// ✅ Good: Select only needed columns
var names = db.Map(
    SQL.SELECT("ProductName, ProductID")
        .FROM("Products"),
    r => new { 
        Name = r.GetString("ProductName"),
        ID = r.GetInt32("ProductID")
    }
).ToList();

// ❌ Bad: Loading full entities when only name is needed
var products = db.From<Product>().ToList();
var names = products.Select(p => p.ProductName).ToList();
```

### 4. Pagination for Large Results

```csharp
// ✅ Good: Paginate large result sets
var page = db.From<Product>()
    .OrderBy(p => p.ProductID)
    .ToPagedList(pageNumber: 1, pageSize: 50);

// ❌ Bad: Loading all records
var allProducts = db.From<Product>().ToList(); // Could be millions!
```

---

## Migration Guide

### From Dapper

SQLFactory provides a superset of Dapper's functionality:

```csharp
// Dapper
var products = connection.Query<Product>(
    "SELECT * FROM Products WHERE UnitPrice > @price",
    new { price = 50 }
).ToList();

// SQLFactory (equivalent)
var products = db.Map(
    SQL.SELECT("*")
        .FROM("Products")
        .WHERE("UnitPrice > {0}", 50),
    r => new Product {
        ProductID = r.GetInt32("ProductID"),
        ProductName = r.GetString("ProductName"),
        UnitPrice = r.GetNullableDecimal("UnitPrice")
    }
).ToList();

// SQLFactory (LINQ-style, preferred)
var products = db.From<Product>()
    .Where(p => p.UnitPrice > 50)
    .ToList();
```

### From Entity Framework Core

Most EF Core patterns translate directly:

```csharp
// EF Core
var products = context.Products
    .Include(p => p.Category)
    .Where(p => p.UnitPrice > 50)
    .ToListAsync();

// SQLFactory (same)
var products = db.From<Product>()
    .Include(p => p.Category)
    .Where(p => p.UnitPrice > 50)
    .ToListAsync();
```

**Key Differences:**
- SQLFactory doesn't track changes automatically (explicit Update() required)
- SQLFactory uses split queries by default for Include()
- SQLFactory is more explicit about SQL generation

---

## Appendix

### Supported Databases

- ✅ SQL Server 2012+
- ✅ PostgreSQL 9.6+
- ✅ MySQL 5.7+
- ✅ SQLite 3.x
- ✅ Oracle (basic support)

### Supported .NET Versions

- ✅ .NET 10.0 (primary)
- ✅ .NET 8.0 LTS
- ✅ .NET Standard 2.1
- ✅ .NET Framework 4.8

### Performance Benchmarks

| Operation | SQLFactory | EF Core | Dapper |
|-----------|------------|---------|--------|
| Insert 1000 records | 45ms | 120ms | 40ms |
| Query 10,000 records | 85ms | 95ms | 80ms |
| Include (2 levels) | 12ms | 45ms | N/A |
| Bulk Insert 100k | 1.2s | 8.5s | N/A |

*Benchmarks run on .NET 10.0, SQL Server, Windows 11*

### Resources

- **GitHub**: https://github.com/yourusername/sqlfactory
- **NuGet**: https://nuget.org/packages/SQLFactory
- **Documentation**: https://sqlfactory.dev/docs
- **Samples**: See `/samples` directory in repository

---

**Last Updated:** January 2026  
**Version:** 1.0.0 Production Release
