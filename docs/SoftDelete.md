# Soft Delete Guide

## Overview

Soft Delete is a data management pattern where records are marked as "deleted" rather than being physically removed from the database. This allows for data recovery, audit trails, and maintaining referential integrity.

SQLFactory provides built-in soft delete support through the `ISoftDeletable` interface and query filters.

## Quick Start

### 1. Implement ISoftDeletable

```csharp
using AnubisWorks.SQLFactory.SoftDelete;

[Table(Name = "Product")]
public class Product : ISoftDeletable
{
    [Column(IsPrimaryKey = true, IsDbGenerated = true)]
    public int Id { get; set; }
    
    [Column]
    public string Name { get; set; }
    
    [Column]
    public decimal Price { get; set; }
    
    // Required by ISoftDeletable
    [Column]
    public bool IsDeleted { get; set; }
}
```

### 2. Configure Auto-Filtering

```csharp
using var db = new Database(connection);

// Enable automatic filtering of soft-deleted records
db.ConfigureSoftDelete<Product>();

// Now all queries automatically exclude deleted records
var products = db.Table<Product>().ToList();  // Only active products
```

### 3. Soft Delete a Record

```csharp
var product = db.Table<Product>().First();

// Soft delete - marks IsDeleted = true
db.Table<Product>().SoftDelete(product);

// Verify: product won't appear in normal queries
var exists = db.Table<Product>()
    .Where("Id = {0}", product.Id)
    .ToList()
    .Any();  // Returns false
```

## Core Features

### Soft Delete vs Hard Delete

```csharp
var product = db.Table<Product>().IncludeDeleted().First();

// Soft delete - marks as deleted, keeps in database
db.Table<Product>().SoftDelete(product);

// Hard delete - permanently removes from database
db.Table<Product>().HardDelete(product);
```

### Restore Deleted Records

```csharp
// Get a deleted record
var deleted = db.Table<Product>()
    .OnlyDeleted()  // Query only deleted records
    .First();

// Restore it
db.Table<Product>().Restore(deleted);

// Now it appears in normal queries again
var restored = db.Table<Product>()
    .Where("Id = {0}", deleted.Id)
    .First();  // Works!
```

### Query Deleted Records

```csharp
// Include deleted records in query
var allProducts = db.Table<Product>()
    .IncludeDeleted()
    .ToList();  // Returns both active and deleted

// Query ONLY deleted records (recycle bin scenario)
var deletedProducts = db.Table<Product>()
    .OnlyDeleted()
    .ToList();  // Returns only deleted records
```

## API Reference

### ISoftDeletable Interface

```csharp
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
}
```

Implement this interface on any entity you want to soft delete.

### Extension Methods

#### ConfigureSoftDelete<T>()

Registers a global query filter to automatically exclude deleted records.

```csharp
db.ConfigureSoftDelete<Product>();
```

**Important:** Call this once during database initialization for each soft-deletable entity type.

#### SoftDelete<T>()

Marks an entity as deleted without removing it from the database.

```csharp
db.Table<Product>().SoftDelete(product);
```

- Sets `IsDeleted = true`
- Updates the database record
- Entity remains in the database

#### HardDelete<T>()

Permanently deletes an entity from the database.

```csharp
db.Table<Product>().HardDelete(product);
```

- Removes the record from the database
- Cannot be restored
- Use with caution

#### Restore<T>()

Restores a soft-deleted entity.

```csharp
db.Table<Product>().Restore(product);
```

- Sets `IsDeleted = false`
- Entity becomes visible in normal queries again

#### IncludeDeleted<T>()

Includes soft-deleted records in the query.

```csharp
var allProducts = db.Table<Product>()
    .IncludeDeleted()
    .ToList();
```

- Temporarily disables the soft delete filter
- Returns both active and deleted records

#### OnlyDeleted<T>()

Returns only soft-deleted records.

```csharp
var deletedProducts = db.Table<Product>()
    .OnlyDeleted()
    .ToList();
```

- Disables the soft delete filter
- Adds `IsDeleted = 1` filter
- Useful for "recycle bin" UIs

## Patterns & Best Practices

### Pattern 1: Application Startup Configuration

```csharp
public class ApplicationDbContext : Database
{
    public ApplicationDbContext(IDbConnection connection) : base(connection)
    {
        // Configure soft delete for all relevant entities
        this.ConfigureSoftDelete<Product>();
        this.ConfigureSoftDelete<Order>();
        this.ConfigureSoftDelete<Customer>();
    }
}
```

### Pattern 2: Soft Delete with Cascade

When you soft delete a parent entity, consider soft deleting child entities:

```csharp
public void DeleteOrder(Order order)
{
    using (var db = new Database(connection))
    {
        db.ConfigureSoftDelete<Order>();
        db.ConfigureSoftDelete<OrderDetail>();
        
        // Soft delete order
        db.Table<Order>().SoftDelete(order);
        
        // Soft delete order details
        var details = db.Table<OrderDetail>()
            .Where("OrderId = {0}", order.Id)
            .ToList();
        
        foreach (var detail in details)
        {
            db.Table<OrderDetail>().SoftDelete(detail);
        }
    }
}
```

### Pattern 3: Audit Trail

Keep track of when records were deleted:

```csharp
[Table(Name = "Product")]
public class Product : ISoftDeletable
{
    [Column(IsPrimaryKey = true)]
    public int Id { get; set; }
    
    [Column]
    public string Name { get; set; }
    
    // ISoftDeletable
    [Column]
    public bool IsDeleted { get; set; }
    
    // Audit fields
    [Column]
    public DateTime? DeletedAt { get; set; }
    
    [Column]
    public int? DeletedBy { get; set; }
}

// Custom soft delete with audit
public void SoftDeleteWithAudit(Product product, int userId)
{
    product.IsDeleted = true;
    product.DeletedAt = DateTime.UtcNow;
    product.DeletedBy = userId;
    
    db.Table<Product>().Update(product);
}
```

### Pattern 4: Recycle Bin UI

```csharp
public class RecycleBinService
{
    private readonly Database _db;
    
    public IEnumerable<Product> GetDeletedProducts()
    {
        return _db.Table<Product>()
            .OnlyDeleted()
            .ToList();
    }
    
    public void RestoreProduct(int productId)
    {
        var product = _db.Table<Product>()
            .OnlyDeleted()
            .Where("Id = {0}", productId)
            .First();
        
        _db.Table<Product>().Restore(product);
    }
    
    public void PermanentlyDelete(int productId)
    {
        var product = _db.Table<Product>()
            .OnlyDeleted()
            .Where("Id = {0}", productId)
            .First();
        
        _db.Table<Product>().HardDelete(product);
    }
}
```

## Integration with Query Filters

Soft delete uses SQLFactory's Query Filter system under the hood:

```csharp
// These are equivalent:
db.ConfigureSoftDelete<Product>();

// Is the same as:
db.QueryFilters.Add<Product>(p => p.IsDeleted == false);
```

You can combine soft delete filters with other filters:

```csharp
// Configure soft delete
db.ConfigureSoftDelete<Product>();

// Add multi-tenancy filter
db.QueryFilters.Add<Product>(p => p.TenantId == currentTenantId);

// Both filters are applied automatically
var products = db.Table<Product>().ToList();
// WHERE IsDeleted = 0 AND TenantId = @tenantId
```

## Database Schema

### SQLite

```sql
CREATE TABLE Product (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Price REAL NOT NULL,
    IsDeleted INTEGER NOT NULL DEFAULT 0
);
```

### SQL Server

```sql
CREATE TABLE Product (
    Id INT PRIMARY KEY IDENTITY,
    Name NVARCHAR(100) NOT NULL,
    Price DECIMAL(18,2) NOT NULL,
    IsDeleted BIT NOT NULL DEFAULT 0
);
```

### PostgreSQL

```sql
CREATE TABLE Product (
    Id SERIAL PRIMARY KEY,
    Name VARCHAR(100) NOT NULL,
    Price DECIMAL(18,2) NOT NULL,
    IsDeleted BOOLEAN NOT NULL DEFAULT FALSE
);
```

## Troubleshooting

### Issue: Deleted records still appear in queries

**Solution:** Make sure you called `ConfigureSoftDelete<T>()`:

```csharp
db.ConfigureSoftDelete<Product>();
```

### Issue: Can't query deleted records with IncludeDeleted()

**Solution:** Ensure the `IsDeleted` column exists in the database:

```sql
ALTER TABLE Product ADD COLUMN IsDeleted INTEGER DEFAULT 0;
```

### Issue: Filter applied twice

If you manually add `Where("IsDeleted = 0")` AND configure soft delete, the filter will be applied twice (though harmless):

```csharp
db.ConfigureSoftDelete<Product>();

// Don't do this - IsDeleted filter already applied automatically
var products = db.Table<Product>()
    .Where("IsDeleted = 0")  // ← Redundant
    .ToList();
```

### Issue: Performance with large tables

Add an index on the `IsDeleted` column:

```sql
CREATE INDEX IX_Product_IsDeleted ON Product(IsDeleted);
```

## Performance Considerations

- **Index IsDeleted:** Always index the `IsDeleted` column for better query performance
- **Periodic cleanup:** Consider permanently deleting old soft-deleted records to prevent table bloat
- **Cascade deletes:** Be mindful of performance when soft deleting entities with many children

## See Also

- [Query Filters Documentation](docs/QueryFilters.md) - Global filter system
- [Repository Pattern](docs/Repository.md) - Encapsulating data access
- [Change Tracking](docs/ChangeTracking.md) - Tracking entity state changes
