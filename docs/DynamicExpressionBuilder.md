# Dynamic Expression Builder Guide

## Overview

The Dynamic Expression Builder (`Expressionable<T>`) is a fluent API for constructing complex LINQ `Where` conditions at runtime. It allows you to combine multiple predicates using `AND`/`OR` logic without repeating boilerplate expression manipulation code.

## Quick Start

### Basic Usage

```csharp
using AnubisWorks.SQLFactory.Expressions;
using var db = new Database(connection);

// Build dynamic filter
var filter = new Expressionable<Product>();

if (!string.IsNullOrEmpty(searchTerm))
    filter.And(p => p.Name.Contains(searchTerm));

if (minPrice.HasValue)
    filter.And(p => p.Price >= minPrice.Value);

if (maxPrice.HasValue)
    filter.And(p => p.Price <= maxPrice.Value);

// Execute query
var products = db.Table<Product>()
    .Where(filter.ToExpression())
    .ToList();
```

## API Reference

### Expressionable<T> Class

Dynamic predicate builder for type `T`.

#### And()

Combines current predicate with a new condition using **AND** logic.

```csharp
public Expressionable<T> And(Expression<Func<T, bool>> expr)
```

**Example:**
```csharp
var filter = new Expressionable<Product>()
    .And(p => p.Price > 10)
    .And(p => p.Stock > 0);

// Equivalent to: WHERE Price > 10 AND Stock > 0
```

#### Or()

Combines current predicate with a new condition using **OR** logic.

```csharp
public Expressionable<T> Or(Expression<Func<T, bool>> expr)
```

**Example:**
```csharp
var filter = new Expressionable<Product>()
    .Or(p => p.Category == "Electronics")
    .Or(p => p.Category == "Computers");

// Equivalent to: WHERE Category = 'Electronics' OR Category = 'Computers'
```

#### ToExpression()

Returns the combined expression as `Expression<Func<T, bool>>`.

```csharp
public Expression<Func<T, bool>> ToExpression()
```

**Returns:** Combined predicate expression, or `_ => true` if no conditions were added.

**Example:**
```csharp
var filter = new Expressionable<Product>()
    .And(p => p.Price > 10);

Expression<Func<Product, bool>> expr = filter.ToExpression();

// Use with SqlSet
var products = db.Table<Product>().Where(expr).ToList();
```

## Use Cases

### 1. Dynamic Search Filters

Build search forms where users can toggle multiple filters:

```csharp
public List<Product> SearchProducts(
    string name = null,
    decimal? minPrice = null,
    decimal? maxPrice = null,
    string category = null,
    bool? inStock = null)
{
    var filter = new Expressionable<Product>();
    
    if (!string.IsNullOrEmpty(name))
        filter.And(p => p.Name.Contains(name));
    
    if (minPrice.HasValue)
        filter.And(p => p.Price >= minPrice.Value);
    
    if (maxPrice.HasValue)
        filter.And(p => p.Price <= maxPrice.Value);
    
    if (!string.IsNullOrEmpty(category))
        filter.And(p => p.Category == category);
    
    if (inStock.HasValue && inStock.Value)
        filter.And(p => p.Stock > 0);
    
    return db.Table<Product>()
        .Where(filter.ToExpression())
        .ToList();
}
```

### 2. Multi-Criteria Queries

Combine multiple search criteria with OR logic:

```csharp
public List<User> FindUsers(string searchTerm)
{
    var filter = new Expressionable<User>()
        .Or(u => u.FirstName.Contains(searchTerm))
        .Or(u => u.LastName.Contains(searchTerm))
        .Or(u => u.Email.Contains(searchTerm))
        .Or(u => u.PhoneNumber.Contains(searchTerm));
    
    return db.Table<User>()
        .Where(filter.ToExpression())
        .ToList();
}
```

### 3. Role-Based Filtering

Apply different filters based on user permissions:

```csharp
public List<Order> GetOrders(User currentUser)
{
    var filter = new Expressionable<Order>();
    
    if (currentUser.Role == "Admin")
    {
        // Admin sees all orders
        filter.And(o => true);
    }
    else if (currentUser.Role == "Manager")
    {
        // Manager sees orders from their department
        filter.And(o => o.Department == currentUser.Department);
    }
    else
    {
        // Regular user sees only their own orders
        filter.And(o => o.UserId == currentUser.Id);
    }
    
    return db.Table<Order>()
        .Where(filter.ToExpression())
        .ToList();
}
```

### 4. Date Range Filters

Handle optional date ranges elegantly:

```csharp
public List<Invoice> GetInvoices(DateTime? startDate, DateTime? endDate)
{
    var filter = new Expressionable<Invoice>();
    
    if (startDate.HasValue)
        filter.And(i => i.InvoiceDate >= startDate.Value);
    
    if (endDate.HasValue)
        filter.And(i => i.InvoiceDate <= endDate.Value);
    
    return db.Table<Invoice>()
        .Where(filter.ToExpression())
        .OrderByDescending(i => i.InvoiceDate)
        .ToList();
}
```

### 5. Complex Boolean Logic

Build queries with mixed AND/OR conditions:

```csharp
public List<Product> FindDiscountedProducts()
{
    // (Price < 20 OR OnSale = true) AND Stock > 0
    
    var priceOrSale = new Expressionable<Product>()
        .Or(p => p.Price < 20)
        .Or(p => p.OnSale);
    
    var filter = new Expressionable<Product>()
        .And(priceOrSale.ToExpression())
        .And(p => p.Stock > 0);
    
    return db.Table<Product>()
        .Where(filter.ToExpression())
        .ToList();
}
```

## Advanced Patterns

### Reusable Filter Components

Create modular filter functions:

```csharp
public class ProductFilters
{
    public static Expression<Func<Product, bool>> InStock()
        => p => p.Stock > 0;
    
    public static Expression<Func<Product, bool>> Affordable(decimal maxPrice)
        => p => p.Price <= maxPrice;
    
    public static Expression<Func<Product, bool>> InCategory(string category)
        => p => p.Category == category;
}

// Compose filters
var filter = new Expressionable<Product>()
    .And(ProductFilters.InStock())
    .And(ProductFilters.Affordable(50.00m))
    .And(ProductFilters.InCategory("Electronics"));

var products = db.Table<Product>()
    .Where(filter.ToExpression())
    .ToList();
```

### Conditional AND vs OR

Choose combining logic based on user preference:

```csharp
public List<Product> Search(string[] tags, bool matchAll)
{
    var filter = new Expressionable<Product>();
    
    foreach (var tag in tags)
    {
        if (matchAll)
            filter.And(p => p.Tags.Contains(tag));  // Match ALL tags
        else
            filter.Or(p => p.Tags.Contains(tag));   // Match ANY tag
    }
    
    return db.Table<Product>()
        .Where(filter.ToExpression())
        .ToList();
}
```

### Builder with Fluent Initialization

```csharp
var products = db.Table<Product>()
    .Where(new Expressionable<Product>()
        .And(p => p.Price > 10)
        .And(p => p.Stock > 0)
        .Or(p => p.OnSale)
        .ToExpression())
    .ToList();
```

### Empty Filter Handling

`ToExpression()` returns `_ => true` when no conditions are added:

```csharp
var filter = new Expressionable<Product>();
// No conditions added

var products = db.Table<Product>()
    .Where(filter.ToExpression())  // Returns all products (_ => true)
    .ToList();
```

## How It Works

### Expression Parameter Substitution

`Expressionable<T>` combines expressions by:

1. **Creating common parameter:** `var parameter = Expression.Parameter(typeof(T))`
2. **Rewriting expressions:** Replace each lambda's parameter with the common one
3. **Combining bodies:** Use `Expression.AndAlso` or `Expression.OrElse`

**Example:**
```csharp
// Original expressions
Expression<Func<Product, bool>> expr1 = p => p.Price > 10;
Expression<Func<Product, bool>> expr2 = p => p.Stock > 0;

// Combined (internally)
// p => (p.Price > 10) && (p.Stock > 0)
```

### ReplaceExpressionVisitor

Internal visitor class that rewrites parameter references:

```csharp
private class ReplaceExpressionVisitor : ExpressionVisitor
{
    private readonly Expression _oldValue;
    private readonly Expression _newValue;
    
    protected override Expression VisitParameter(ParameterExpression node)
    {
        return node == _oldValue ? _newValue : base.VisitParameter(node);
    }
}
```

This ensures all sub-expressions refer to the same parameter instance.

## Best Practices

### 1. Prefer Strongly-Typed Expressions

```csharp
// ✅ Good - compile-time safety
filter.And(p => p.Price > 10);

// ❌ Avoid - string-based queries
filter.And(p => p.GetType().GetProperty("Price").GetValue(p) > 10);
```

### 2. Avoid Complex Nested Logic

```csharp
// ⚠️ Hard to read
var filter = new Expressionable<Product>()
    .And(p => (p.Price > 10 && p.Stock > 0) || (p.OnSale && p.Price > 5));

// ✅ Better - break into sub-filters
var normalProducts = new Expressionable<Product>()
    .And(p => p.Price > 10)
    .And(p => p.Stock > 0);

var saleProducts = new Expressionable<Product>()
    .And(p => p.OnSale)
    .And(p => p.Price > 5);

var filter = new Expressionable<Product>()
    .Or(normalProducts.ToExpression())
    .Or(saleProducts.ToExpression());
```

### 3. Test Dynamic Queries

```csharp
[Test]
public void SearchProducts_WithAllFilters_ReturnsFilteredResults()
{
    // Arrange
    var filter = new Expressionable<Product>()
        .And(p => p.Price > 10)
        .And(p => p.Stock > 0);
    
    // Act
    var products = db.Table<Product>()
        .Where(filter.ToExpression())
        .ToList();
    
    // Assert
    Assert.That(products, Is.All.Matches<Product>(p => p.Price > 10 && p.Stock > 0));
}
```

### 4. Handle Null/Empty Gracefully

```csharp
public List<Product> Search(string name)
{
    var filter = new Expressionable<Product>();
    
    // ✅ Safe - only adds filter if value provided
    if (!string.IsNullOrEmpty(name))
        filter.And(p => p.Name.Contains(name));
    
    return db.Table<Product>()
        .Where(filter.ToExpression())
        .ToList();
}
```

### 5. Use Extension Methods for Clarity

```csharp
public static class ProductFilterExtensions
{
    public static Expressionable<Product> InStock(this Expressionable<Product> filter)
        => filter.And(p => p.Stock > 0);
    
    public static Expressionable<Product> AffordableUnder(
        this Expressionable<Product> filter, 
        decimal maxPrice)
        => filter.And(p => p.Price <= maxPrice);
}

// Usage
var products = db.Table<Product>()
    .Where(new Expressionable<Product>()
        .InStock()
        .AffordableUnder(50.00m)
        .ToExpression())
    .ToList();
```

## Comparison with Alternatives

### Expressionable vs Manual IQueryable

```csharp
// ❌ Manual (verbose)
IQueryable<Product> query = db.Table<Product>();

if (!string.IsNullOrEmpty(name))
    query = query.Where(p => p.Name.Contains(name));

if (minPrice.HasValue)
    query = query.Where(p => p.Price >= minPrice.Value);

var products = query.ToList();

// ✅ Expressionable (concise)
var filter = new Expressionable<Product>();

if (!string.IsNullOrEmpty(name))
    filter.And(p => p.Name.Contains(name));

if (minPrice.HasValue)
    filter.And(p => p.Price >= minPrice.Value);

var products = db.Table<Product>()
    .Where(filter.ToExpression())
    .ToList();
```

### Expressionable vs PredicateBuilder

```csharp
// LinqKit PredicateBuilder
var predicate = PredicateBuilder.New<Product>();
predicate = predicate.And(p => p.Price > 10);
predicate = predicate.Or(p => p.OnSale);

// SQLFactory Expressionable (similar API)
var filter = new Expressionable<Product>()
    .And(p => p.Price > 10)
    .Or(p => p.OnSale);
```

**Trade-offs:**
- **Expressionable:** Built-in, zero dependencies, fluent API
- **PredicateBuilder:** Mature library, more features (Not, Start, etc.)

## Troubleshooting

### Issue: "Parameter not in scope" exception

**Cause:** Manually combining expressions with different parameter instances.

**Solution:** Use `Expressionable<T>` which handles parameter substitution automatically.

### Issue: Empty filter returns no results

**Cause:** Expecting `null` expression instead of `_ => true`.

**Solution:** `ToExpression()` always returns a valid expression. Empty filter = return all.

### Issue: Complex AND/OR logic not working

**Cause:** Mixing `And()` and `Or()` in unexpected order.

**Solution:** Use sub-filters for grouped conditions:
```csharp
var group1 = new Expressionable<Product>()
    .And(p => p.Price > 10)
    .And(p => p.Stock > 0);

var group2 = new Expressionable<Product>()
    .And(p => p.OnSale);

var final = new Expressionable<Product>()
    .Or(group1.ToExpression())
    .Or(group2.ToExpression());
```

## Performance Notes

- **Expression compilation:** Minimal overhead; expressions are compiled once by EF/Dapper
- **Runtime construction:** Fast; only combines expression trees
- **SQL generation:** Identical to hand-written LINQ expressions

## See Also

- [LINQ Query Expressions](https://docs.microsoft.com/en-us/dotnet/csharp/linq/)
- [Expression Trees](https://docs.microsoft.com/en-us/dotnet/csharp/expression-trees)
- [Global Query Filters](docs/GlobalQueryFilters.md) - Automatic filter application
- [Soft Delete](docs/SoftDelete.md) - Built-in filtering for soft-deleted records
