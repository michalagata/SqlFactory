# Lazy Loading Guide

## Overview

Lazy Loading is an ORM pattern that automatically loads related entities when they are first accessed, rather than loading them eagerly with the main query. SQLFactory implements lazy loading using Castle.DynamicProxy to create runtime proxies.

## Installation

Lazy Loading requires the Castle.Core NuGet package:

```bash
dotnet add package Castle.Core
```

This dependency is automatically included in SQLFactory.

## Basic Usage

### Enabling Lazy Loading

```csharp
using var db = new Database(connection);

// Enable lazy loading for all entities
db.LazyLoading.IsEnabled = true;

// Now navigation properties will load automatically
var product = db.Table<Product>().First();
var category = product.Category; // ← Automatically loads from database
```

### Entity Requirements

For lazy loading to work, entities must meet these requirements:

1. **Classes must be non-sealed** (cannot be `sealed`)
2. **Navigation properties must be virtual**
3. **Properties should use public getters/setters**

```csharp
[Table(Name = "Product")]
public class Product  // ← NOT sealed
{
    [Column(IsPrimaryKey = true)]
    public int ProductID { get; set; }
    
    [Column]
    public string ProductName { get; set; }
    
    [Column]
    public int CategoryID { get; set; }
    
    // Navigation property - MUST be virtual
    [Association(ThisKey = "CategoryID", OtherKey = "CategoryID", IsForeignKey = true)]
    public virtual Category Category { get; set; }  // ← virtual keyword
}

[Table(Name = "Category")]
public class Category
{
    [Column(IsPrimaryKey = true)]
    public int CategoryID { get; set; }
    
    [Column]
    public string CategoryName { get; set; }
    
    // Collection navigation - MUST be virtual
    [Association(ThisKey = "CategoryID", OtherKey = "CategoryID", IsForeignKey = false)]
    public virtual ICollection<Product> Products { get; set; }  // ← virtual
}
```

## Configuration

### Per-Entity Configuration

```csharp
// Enable lazy loading only for specific types
db.LazyLoading.IsEnabled = false;  // Disabled by default
db.LazyLoading.EnableFor<Product>();
db.LazyLoading.EnableFor<Category>();

// Disable for specific types
db.LazyLoading.IsEnabled = true;
db.LazyLoading.DisableFor<Customer>();  // No lazy loading for Customer
```

### Max Depth Configuration

Prevent infinite loops and excessive nesting:

```csharp
// Set maximum traversal depth (default: 10)
db.LazyLoading.MaxDepth = 5;

// Now nested navigation will stop after 5 levels
var product = db.Table<Product>().First();
var category = product.Category;  // Level 1
var supplier = category.Supplier;  // Level 2
// ... continues up to MaxDepth
```

### N+1 Query Detection

```csharp
// Enable warnings for potential N+1 problems (enabled by default)
db.LazyLoading.DetectNPlusOne = true;

var products = db.Table<Product>().ToList();  // 1 query
foreach (var product in products)
{
    // This triggers 1 query per product → N+1 problem!
    Console.WriteLine(product.Category.CategoryName);
}
// Warning will be logged if N+1 is detected
```

## Advanced Scenarios

### Lazy Loading vs Include()

You can mix both approaches:

```csharp
db.LazyLoading.IsEnabled = true;

// Eager load Category with Include()
var products = db.Table<Product>()
    .Include(p => p.Category)
    .ToList();

// Category is already loaded (no lazy load)
var cat = products[0].Category;  // ← Already loaded, no query

// But other navigation properties will lazy load
var orders = products[0].Orders;  // ← Lazy loaded on access
```

**Note:** Objects loaded via `Include()` are not proxies, so their navigation properties will NOT lazy load. Only the main query result entities become proxies.

### Circular Reference Protection

Lazy loading automatically prevents circular references:

```csharp
var product = db.Table<Product>().First();
var category = product.Category;  // Loads Category
var products = category.Products;  // Loads Products collection

// If you access product.Category again, it won't create infinite loop
// The lazy loader tracks what's currently loading
```

### Collection Navigation

```csharp
var category = db.Table<Category>()
    .Where("CategoryID = 1")
    .ToList()
    .First();

// Automatically loads all products for this category
var products = category.Products;  // ← Lazy loads collection
Console.WriteLine($"Found {products.Count} products");
```

## Foreign Key Conventions

Lazy Loading discovers foreign keys using these conventions (in order):

1. **[Association] attribute** (recommended)
   ```csharp
   [Association(ThisKey = "CategoryID", OtherKey = "CategoryID")]
   public virtual Category Category { get; set; }
   ```

2. **Navigation property name + "Id"**
   ```csharp
   public int CategoryId { get; set; }  // FK property
   public virtual Category Category { get; set; }  // Navigation
   ```

3. **Navigation type name + "Id"**
   ```csharp
   public int CategoryId { get; set; }  // Matches type name
   public virtual Category MyCategory { get; set; }
   ```

## Performance Considerations

### N+1 Query Problem

**Bad** (causes N+1 queries):
```csharp
var products = db.Table<Product>().ToList();  // 1 query
foreach (var product in products)
{
    // Each iteration = 1 query → N queries total
    Console.WriteLine(product.Category.CategoryName);
}
```

**Good** (use Include() instead):
```csharp
var products = db.Table<Product>()
    .Include(p => p.Category)  // Eager load
    .ToList();  // 2 queries total (main + include)

foreach (var product in products)
{
    Console.WriteLine(product.Category.CategoryName);  // No query
}
```

### When to Use Lazy Loading

**Good use cases:**
- **Occasional access**: Navigation properties rarely accessed
- **Conditional access**: Properties accessed only in specific branches
- **Simple scenarios**: Single entity fetch with limited navigation

**Avoid lazy loading when:**
- Loading many entities in a loop (use `Include()`)
- Complex object graphs (use `ThenInclude()`)
- Performance is critical (eager load what you need)

## Troubleshooting

### "Property 'X' is not configured as an association"

Add the `[Association]` attribute:
```csharp
[Association(ThisKey = "CategoryID", OtherKey = "CategoryID", IsForeignKey = true)]
public virtual Category Category { get; set; }
```

### "Cannot create proxy for sealed type"

Remove the `sealed` modifier:
```csharp
public class MyEntity { }  // ← OK
// NOT: public sealed class MyEntity { }
```

### Navigation property returns null

Ensure:
1. Property is `virtual`
2. Lazy loading is enabled: `db.LazyLoading.IsEnabled = true`
3. Entity was loaded via `db.Table<T>().ToList()` (not raw SQL)
4. Foreign key exists in the database

### Proxy already loaded, not lazy loading

If an entity is loaded via `Include()`, it's not a proxy:
```csharp
var products = db.Table<Product>()
    .Include(p => p.Category)
    .ToList();

// products[0].Category is already loaded (not a proxy)
// products[0].Category.Products will NOT lazy load
```

## API Reference

### Database.LazyLoading Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `IsEnabled` | `bool` | `false` | Enables/disables lazy loading globally |
| `MaxDepth` | `int` | `10` | Maximum navigation depth to prevent infinite loops |
| `DetectNPlusOne` | `bool` | `true` | Logs warnings when N+1 queries detected |

### Database.LazyLoading Methods

| Method | Description |
|--------|-------------|
| `EnableFor<T>()` | Enable lazy loading for type `T` |
| `DisableFor<T>()` | Disable lazy loading for type `T` |
| `IsEnabledFor(Type type)` | Check if lazy loading is enabled for a type |

## Examples

### Example 1: Basic Reference Navigation

```csharp
using var db = new Database(connection);
db.LazyLoading.IsEnabled = true;

// Load product without category
var product = db.Table<Product>()
    .Where("ProductID = 1")
    .ToList()
    .First();

Console.WriteLine(product.ProductName);  // No query

// Access category - triggers lazy load
Console.WriteLine(product.Category.CategoryName);  // 1 query
```

### Example 2: Collection Navigation

```csharp
using var db = new Database(connection);
db.LazyLoading.IsEnabled = true;

var category = db.Table<Category>().First();

// Access products collection - triggers lazy load
foreach (var product in category.Products)
{
    Console.WriteLine(product.ProductName);
}
```

### Example 3: Hybrid Include() + Lazy Loading

```csharp
using var db = new Database(connection);
db.LazyLoading.IsEnabled = true;

// Eager load Category, lazy load Orders
var products = db.Table<Product>()
    .Include(p => p.Category)  // Eager
    .ToList();

foreach (var product in products)
{
    // Category already loaded (no query)
    Console.WriteLine(product.Category.CategoryName);
    
    // Orders lazy loaded (1 query per product)
    Console.WriteLine($"Orders: {product.Orders.Count}");
}
```

## Implementation Notes

- Lazy loading uses **Castle.DynamicProxy** for runtime proxy generation
- Proxies intercept property getters to trigger lazy loading
- Only works with entities loaded through `SqlTable<T>` queries
- Raw SQL queries return non-proxy objects
- Thread-safe depth tracking via `LazyLoadingContext`

## See Also

- [Include() Documentation](docs/Include.md) - Eager loading guide
- [ThenInclude() Documentation](docs/ThenInclude.md) - Nested eager loading
- [Performance Guide](docs/Performance.md) - N+1 problem solutions
