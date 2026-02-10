# Upsert (InsertOrUpdate) Guide

## Overview

Upsert is a database operation that combines INSERT and UPDATE logic: **insert** if the record doesn't exist, **update** if it does. SQLFactory provides intelligent upsert operations based on primary key detection.

## Quick Start

### Basic Upsert

```csharp
using var db = new Database(connection);

// Create or update a product
var product = new Product
{
    Id = 1,  // If Id=1 exists → UPDATE, else → INSERT
    Name = "New Product",
    Price = 99.99m
};

db.Table<Product>().InsertOrUpdate(product);
```

### How It Works

1. **Check existence:** `Database.Find()` queries by primary key
2. **Insert:** If not found, calls `SqlTable<T>.Add(entity)`
3. **Update:** If found, calls `SqlTable<T>.Update(entity)`

## API Reference

### InsertOrUpdate() - Single Entity

```csharp
public static void InsertOrUpdate<TEntity>(
    this SqlTable<TEntity> table, 
    TEntity entity)
```

**Parameters:**
- `entity` - Entity to insert or update

**Behavior:**
- Determines operation based on primary key value
- Uses `Database.Find()` for existence check
- Automatically chooses INSERT or UPDATE

**Example:**
```csharp
var category = new Category { Id = 5, Name = "Electronics" };
db.Table<Category>().InsertOrUpdate(category);
```

### InsertOrUpdate() - Batch

```csharp
public static void InsertOrUpdate<TEntity>(
    this SqlTable<TEntity> table, 
    IEnumerable<TEntity> entities)
```

**Parameters:**
- `entities` - Collection of entities to upsert

**Behavior:**
- Processes each entity individually
- Each entity is checked and inserted/updated
- Executed within implicit or explicit transaction

**Example:**
```csharp
var products = new[]
{
    new Product { Id = 1, Name = "Product A", Price = 10.00m },
    new Product { Id = 2, Name = "Product B", Price = 20.00m },
    new Product { Id = 0, Name = "Product C", Price = 30.00m }  // Id=0 → INSERT
};

db.Table<Product>().InsertOrUpdate(products);
```

## Use Cases

### 1. Import/Sync Operations

Synchronize external data with your database:

```csharp
// Import from external API
var externalProducts = await FetchProductsFromApi();

foreach (var apiProduct in externalProducts)
{
    var product = new Product
    {
        Id = apiProduct.ExternalId,
        Name = apiProduct.Name,
        Price = apiProduct.Price,
        UpdatedAt = DateTime.UtcNow
    };
    
    db.Table<Product>().InsertOrUpdate(product);
}
```

### 2. Configuration/Settings Management

Store or update application settings:

```csharp
public void SaveSetting(string key, string value)
{
    var setting = db.Table<AppSetting>()
        .Where(s => s.Key == key)
        .FirstOrDefault();
    
    if (setting == null)
        setting = new AppSetting { Key = key };
    
    setting.Value = value;
    setting.UpdatedAt = DateTime.UtcNow;
    
    db.Table<AppSetting>().InsertOrUpdate(setting);
}
```

### 3. Cache Refresh

Update cache tables with latest data:

```csharp
public void RefreshProductCache(int productId)
{
    var product = db.Table<Product>().Find(productId);
    
    var cacheEntry = new ProductCache
    {
        ProductId = product.Id,
        Name = product.Name,
        Price = product.Price,
        CachedAt = DateTime.UtcNow
    };
    
    db.Table<ProductCache>().InsertOrUpdate(cacheEntry);
}
```

### 4. User Profile Updates

Handle user registration and profile updates uniformly:

```csharp
public void SaveUserProfile(UserProfile profile)
{
    // Works for both new users and existing users
    db.Table<UserProfile>().InsertOrUpdate(profile);
}
```

### 5. Inventory Management

Maintain inventory levels:

```csharp
public void UpdateInventory(int productId, int quantity)
{
    var inventory = new Inventory
    {
        ProductId = productId,
        Quantity = quantity,
        LastUpdated = DateTime.UtcNow
    };
    
    db.Table<Inventory>().InsertOrUpdate(inventory);
}
```

## Batch Operations

### Sequential Batch Upsert

```csharp
var categories = new[]
{
    new Category { Id = 1, Name = "Updated Category" },
    new Category { Id = 0, Name = "New Category" },
    new Category { Id = 3, Name = "Another Update" }
};

// Processes each entity in sequence
db.Table<Category>().InsertOrUpdate(categories);
```

### Transaction-Safe Batch

```csharp
using var transaction = db.BeginTransaction();

try
{
    db.Table<Product>().InsertOrUpdate(products);
    db.Table<Category>().InsertOrUpdate(categories);
    
    transaction.Commit();
}
catch
{
    transaction.Rollback();
    throw;
}
```

### Parallel Batch with Manual Control

For large datasets, consider manual parallelization with separate connections:

```csharp
// NOT recommended for small datasets (overhead > benefit)
var batches = products.Chunk(100);

Parallel.ForEach(batches, batch =>
{
    using var db = new Database(connectionString);
    db.Table<Product>().InsertOrUpdate(batch);
});
```

## Primary Key Requirements

### Auto-Generated Keys (Identity/AutoIncrement)

```csharp
[Table(Name = "Product")]
public class Product
{
    [Column(IsPrimaryKey = true, IsDbGenerated = true)]
    public int Id { get; set; }  // 0 or negative = INSERT
    
    [Column]
    public string Name { get; set; }
}

// INSERT when Id = 0
var newProduct = new Product { Id = 0, Name = "New" };
db.Table<Product>().InsertOrUpdate(newProduct);

// UPDATE when Id > 0
var existingProduct = new Product { Id = 5, Name = "Updated" };
db.Table<Product>().InsertOrUpdate(existingProduct);
```

### Manual/GUID Keys

```csharp
[Table(Name = "Document")]
public class Document
{
    [Column(IsPrimaryKey = true)]
    public Guid Id { get; set; }
    
    [Column]
    public string Content { get; set; }
}

// For manual keys, always set the key explicitly
var doc = new Document 
{ 
    Id = Guid.Parse("..."),  // Must exist in DB for UPDATE
    Content = "Updated content" 
};

db.Table<Document>().InsertOrUpdate(doc);
```

### Composite Keys

```csharp
[Table(Name = "OrderItem")]
public class OrderItem
{
    [Column(IsPrimaryKey = true)]
    public int OrderId { get; set; }
    
    [Column(IsPrimaryKey = true)]
    public int ProductId { get; set; }
    
    [Column]
    public int Quantity { get; set; }
}

// Upsert checks BOTH key columns
var item = new OrderItem 
{ 
    OrderId = 100, 
    ProductId = 5, 
    Quantity = 10 
};

db.Table<OrderItem>().InsertOrUpdate(item);
```

## Performance Considerations

### Existence Check Overhead

Each upsert performs a `Find()` query:

```csharp
// This generates N+1 queries (1 Find + 1 INSERT/UPDATE per entity)
foreach (var product in products)
{
    db.Table<Product>().InsertOrUpdate(product);
}
```

**Optimization:** Use explicit checks when you know the state:

```csharp
// Better for known new records
var newProducts = products.Where(p => p.Id == 0);
db.Table<Product>().AddRange(newProducts);

// Better for known existing records
var existingProducts = products.Where(p => p.Id > 0);
foreach (var p in existingProducts)
    db.Table<Product>().Update(p);
```

### Bulk Upsert Optimization

For large datasets, consider provider-specific bulk operations:

```sql
-- SQL Server MERGE (native upsert)
MERGE INTO Product AS target
USING @ProductTable AS source
ON target.Id = source.Id
WHEN MATCHED THEN UPDATE SET Name = source.Name, Price = source.Price
WHEN NOT MATCHED THEN INSERT (Name, Price) VALUES (source.Name, source.Price);
```

```csharp
// Custom bulk upsert for SQL Server
public void BulkUpsert(List<Product> products)
{
    var sql = @"
        MERGE INTO Product AS target
        USING (VALUES (@Id, @Name, @Price)) AS source (Id, Name, Price)
        ON target.Id = source.Id
        WHEN MATCHED THEN UPDATE SET Name = source.Name, Price = source.Price
        WHEN NOT MATCHED THEN INSERT (Id, Name, Price) VALUES (source.Id, source.Name, source.Price);
    ";
    
    foreach (var product in products)
    {
        db.Execute(sql, new { product.Id, product.Name, product.Price });
    }
}
```

## Best Practices

### 1. Use Upsert for Idempotent Operations

When the same operation can be safely repeated:

```csharp
// Safe to call multiple times
public void EnsureDefaultCategories()
{
    var defaults = new[]
    {
        new Category { Id = 1, Name = "General" },
        new Category { Id = 2, Name = "Special" }
    };
    
    db.Table<Category>().InsertOrUpdate(defaults);
}
```

### 2. Avoid Upsert for Concurrent Writes

Upsert doesn't handle concurrency:

```csharp
// ❌ RACE CONDITION: Two threads may both INSERT
var product = new Product { Id = 0, Name = "Concurrent" };
db.Table<Product>().InsertOrUpdate(product);

// ✅ Better: Use Optimistic Concurrency
[RowVersion]
public long Version { get; set; }
```

### 3. Explicit State When Known

```csharp
// ❌ Wasteful: Checks existence when you know it's new
var newProduct = CreateNewProduct();
db.Table<Product>().InsertOrUpdate(newProduct);  // Extra Find() call

// ✅ Better: Direct insert
db.Table<Product>().Add(newProduct);
```

### 4. Transaction for Related Entities

```csharp
using var tx = db.BeginTransaction();

try
{
    db.Table<Order>().InsertOrUpdate(order);
    db.Table<OrderItem>().InsertOrUpdate(order.Items);
    
    tx.Commit();
}
catch
{
    tx.Rollback();
    throw;
}
```

### 5. Timestamp/Audit Fields

Update metadata on upsert:

```csharp
public void SaveProduct(Product product)
{
    product.UpdatedAt = DateTime.UtcNow;
    product.UpdatedBy = CurrentUser.Id;
    
    if (product.Id == 0)
        product.CreatedAt = DateTime.UtcNow;
    
    db.Table<Product>().InsertOrUpdate(product);
}
```

## Comparison with Alternatives

### Upsert vs Manual Check

```csharp
// ❌ Manual (verbose)
var existing = db.Table<Product>().Find(product.Id);
if (existing == null)
    db.Table<Product>().Add(product);
else
    db.Table<Product>().Update(product);

// ✅ Upsert (concise)
db.Table<Product>().InsertOrUpdate(product);
```

### Upsert vs Native MERGE

```csharp
// Upsert: 2 queries (Find + INSERT/UPDATE)
db.Table<Product>().InsertOrUpdate(product);

// Native MERGE: 1 query (provider-specific)
db.Execute(@"
    INSERT INTO Product (Id, Name, Price) VALUES (@Id, @Name, @Price)
    ON CONFLICT(Id) DO UPDATE SET Name = @Name, Price = @Price
", product);
```

**Trade-off:**
- **Upsert:** Cross-provider, simple, readable (2 queries)
- **MERGE:** Provider-specific, optimal performance (1 query)

## Troubleshooting

### Issue: Upsert always inserts

**Cause:** Primary key not configured correctly.

**Solution:**
```csharp
[Column(IsPrimaryKey = true)]  // ← Must be set
public int Id { get; set; }
```

### Issue: Duplicate key violation

**Cause:** Concurrent upserts on same key.

**Solution:** Use explicit transactions or optimistic concurrency:
```csharp
using var tx = db.BeginTransaction();
db.Table<Product>().InsertOrUpdate(product);
tx.Commit();
```

### Issue: Upsert doesn't update

**Cause:** Primary key mismatch between entity and database.

**Solution:** Verify key values match:
```csharp
var found = db.Table<Product>().Find(product.Id);
Console.WriteLine($"Database Id: {found?.Id}, Entity Id: {product.Id}");
```

### Issue: Poor performance on large batches

**Cause:** N+1 queries (1 Find per entity).

**Solution:** Use provider-specific bulk operations or batch into transactions:
```csharp
using var tx = db.BeginTransaction();

foreach (var batch in products.Chunk(100))
{
    db.Table<Product>().InsertOrUpdate(batch);
}

tx.Commit();
```

## See Also

- [Bulk Operations](docs/BulkOperations.md) - High-performance batch inserts
- [Optimistic Concurrency](docs/OptimisticConcurrency.md) - Handle concurrent updates
- [Transactions](docs/Transactions.md) - ACID guarantees
- [Change Tracking](docs/ChangeTracking.md) - Automatic state management
