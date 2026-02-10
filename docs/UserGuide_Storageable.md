# SQLFactory Storageable - User Guide

## Table of Contents
- [Overview](#overview)
- [Key Concepts](#key-concepts)
- [Compare Algorithm](#compare-algorithm)
- [Key Selectors](#key-selectors)
- [Equality Comparers](#equality-comparers)
- [SyncWith Integration](#syncwith-integration)
- [Real-World Examples](#real-world-examples)
- [Best Practices](#best-practices)

## Overview

Storageable provides **key-based data comparison and synchronization** between in-memory collections and database tables. It identifies new, modified, and deleted records by comparing keys and values, then generates appropriate INSERT, UPDATE, DELETE operations.

### When to Use Storageable

- **API Synchronization**: Sync external API data with local database
- **Data Migration**: Compare and merge data from different sources
- **ETL Pipelines**: Extract, transform, load with change detection
- **Master-Detail Sync**: Synchronize related record sets
- **Reconciliation**: Detect and resolve data discrepancies

### When NOT to Use Storageable

- **Simple Inserts**: Use `Table<T>().Add()` for single records
- **Full Replacements**: Use `DELETE + INSERT` if rewriting entire table
- **Stream Processing**: Real-time event streams with individual operations
- **No Key Available**: Records lack unique identifiers

## Key Concepts

### StorageableComparer<T>

Core class that compares two collections and produces a `StorageableResult`.

```csharp
public class StorageableComparer<T>
{
    public StorageableResult<T> Compare(
        IEnumerable<T> source,      // Current data (e.g., from API)
        IEnumerable<T> target,      // Existing data (from database)
        Func<T, object> keySelector // How to identify records
    );
}
```

### StorageableResult<T>

Contains categorized records based on comparison:

```csharp
public class StorageableResult<T>
{
    public List<T> ToInsert { get; set; }  // New records (in source, not in target)
    public List<T> ToUpdate { get; set; }  // Modified records (different values)
    public List<T> ToDelete { get; set; }  // Removed records (in target, not in source)
    public List<T> Unchanged { get; set; } // Identical records (no action needed)
}
```

### Comparison Workflow

```
┌─────────────────┐     ┌─────────────────┐
│  Source Data    │     │  Target Data    │
│  (API, File)    │     │  (Database)     │
└────────┬────────┘     └────────┬────────┘
         │                       │
         └───────────┬───────────┘
                     │
              ┌──────▼──────┐
              │  Compare    │
              │  by Keys    │
              └──────┬──────┘
                     │
         ┌───────────┼───────────┐
         │           │           │
    ┌────▼────┐ ┌───▼────┐ ┌───▼────┐
    │ Insert  │ │ Update │ │ Delete │
    │ New     │ │ Changed│ │ Missing│
    └─────────┘ └────────┘ └────────┘
```

## Compare Algorithm

### How It Works

1. **Key Extraction**: Extract keys from both collections using `keySelector`
2. **Set Operations**: Identify keys in source only, target only, or both
3. **Value Comparison**: For matching keys, compare full objects using `IEqualityComparer<T>`
4. **Categorization**: Assign records to Insert, Update, Delete, or Unchanged

### Algorithm Pseudocode

```
sourceKeys = source.Select(keySelector)
targetKeys = target.Select(keySelector)

toInsertKeys = sourceKeys - targetKeys       // Keys only in source
toDeleteKeys = targetKeys - sourceKeys       // Keys only in target
commonKeys = sourceKeys ∩ targetKeys         // Keys in both

For each key in commonKeys:
    sourceRecord = source[key]
    targetRecord = target[key]
    
    if equalityComparer.Equals(sourceRecord, targetRecord):
        → Unchanged
    else:
        → ToUpdate
```

### Example

```csharp
// Source data (from API)
var apiProducts = new[]
{
    new Product { Id = 1, Name = "Widget", Price = 10.00m },     // Exists, unchanged
    new Product { Id = 2, Name = "Gadget", Price = 25.00m },     // Exists, price changed
    new Product { Id = 4, Name = "Thingamajig", Price = 5.00m }  // New product
};

// Target data (from database)
var dbProducts = new[]
{
    new Product { Id = 1, Name = "Widget", Price = 10.00m },     // Unchanged
    new Product { Id = 2, Name = "Gadget", Price = 20.00m },     // Old price
    new Product { Id = 3, Name = "Doohickey", Price = 15.00m }   // Deleted from API
};

var comparer = new StorageableComparer<Product>();
var result = comparer.Compare(
    source: apiProducts,
    target: dbProducts,
    keySelector: p => p.Id
);

// Result:
// ToInsert: [Product { Id = 4, ... }]           // New
// ToUpdate: [Product { Id = 2, Price = 25 }]    // Changed
// ToDelete: [Product { Id = 3, ... }]           // Removed
// Unchanged: [Product { Id = 1, ... }]          // Same
```

## Key Selectors

### Simple Key

Single property as identifier:

```csharp
var result = comparer.Compare(
    source: apiProducts,
    target: dbProducts,
    keySelector: p => p.Id
);
```

### Composite Key

Multiple properties combined:

```csharp
// Using anonymous object
var result = comparer.Compare(
    source: apiOrders,
    target: dbOrders,
    keySelector: o => new { o.OrderId, o.LineNumber }
);

// Using tuple
var result = comparer.Compare(
    source: apiOrders,
    target: dbOrders,
    keySelector: o => (o.OrderId, o.LineNumber)
);
```

### String Key

Concatenated string identifier:

```csharp
var result = comparer.Compare(
    source: apiRecords,
    target: dbRecords,
    keySelector: r => $"{r.Year}-{r.Month}-{r.Code}"
);
```

### Custom Key Class

```csharp
public class OrderLineKey : IEquatable<OrderLineKey>
{
    public int OrderId { get; set; }
    public int LineNumber { get; set; }

    public bool Equals(OrderLineKey? other) =>
        other != null && OrderId == other.OrderId && LineNumber == other.LineNumber;

    public override int GetHashCode() =>
        HashCode.Combine(OrderId, LineNumber);
}

var result = comparer.Compare(
    source: apiLines,
    target: dbLines,
    keySelector: line => new OrderLineKey 
    { 
        OrderId = line.OrderId, 
        LineNumber = line.LineNumber 
    }
);
```

## Equality Comparers

### Default Comparer (Reference Equality)

By default, uses `EqualityComparer<T>.Default`:

```csharp
var result = comparer.Compare(source, target, keySelector);
// Uses reference equality - will mark all as "ToUpdate" unless overridden
```

### Custom Value Comparer

Compare specific properties:

```csharp
public class ProductComparer : IEqualityComparer<Product>
{
    public bool Equals(Product? x, Product? y)
    {
        if (x == null || y == null) return false;
        return x.Name == y.Name && x.Price == y.Price;
    }

    public int GetHashCode(Product obj) =>
        HashCode.Combine(obj.Name, obj.Price);
}

var result = comparer.Compare(
    source: apiProducts,
    target: dbProducts,
    keySelector: p => p.Id,
    comparer: new ProductComparer()
);
```

### Tolerance-Based Comparer

Allow small differences:

```csharp
public class ProductToleranceComparer : IEqualityComparer<Product>
{
    private readonly decimal _priceTolerance;

    public ProductToleranceComparer(decimal tolerance = 0.01m)
    {
        _priceTolerance = tolerance;
    }

    public bool Equals(Product? x, Product? y)
    {
        if (x == null || y == null) return false;
        return x.Name == y.Name && 
               Math.Abs(x.Price - y.Price) <= _priceTolerance;
    }

    public int GetHashCode(Product obj) =>
        HashCode.Combine(obj.Name, (int)(obj.Price * 100));
}

var result = comparer.Compare(
    source, target, 
    keySelector: p => p.Id,
    comparer: new ProductToleranceComparer(tolerance: 0.05m)
);
```

### Field-Specific Comparers

Ignore certain fields:

```csharp
public class ProductIgnoreTimestampComparer : IEqualityComparer<Product>
{
    public bool Equals(Product? x, Product? y)
    {
        if (x == null || y == null) return false;
        // Ignore LastModified timestamp
        return x.Id == y.Id && 
               x.Name == y.Name && 
               x.Price == y.Price;
    }

    public int GetHashCode(Product obj) =>
        HashCode.Combine(obj.Id, obj.Name, obj.Price);
}
```

## SyncWith Integration

### Database.SyncWith()

Automatically executes INSERT, UPDATE, DELETE operations:

```csharp
public void SyncWith<T>(
    IEnumerable<T> source,
    Func<T, object> keySelector,
    IEqualityComparer<T>? comparer = null
);
```

### Basic Usage

```csharp
// Fetch current database state
var dbProducts = database.Table<Product>().ToList();

// Get new data from API
var apiProducts = await FetchProductsFromApiAsync();

// Sync database with API data
database.SyncWith(
    source: apiProducts,
    keySelector: p => p.Id
);

// Database now matches API exactly
```

### With Custom Comparer

```csharp
database.SyncWith(
    source: apiProducts,
    keySelector: p => p.Id,
    comparer: new ProductComparer()
);
```

### Transaction Behavior

All operations execute in a single transaction:

```csharp
try
{
    database.SyncWith(apiProducts, p => p.Id);
    // If any operation fails, entire sync rolls back
}
catch (Exception ex)
{
    Console.WriteLine($"Sync failed: {ex.Message}");
    // Database remains in original state
}
```

## Real-World Examples

### Example 1: API Synchronization

```csharp
public class ProductSyncService
{
    private readonly Database _database;
    private readonly HttpClient _httpClient;

    public async Task SyncProductsAsync()
    {
        // Fetch from external API
        var apiProducts = await _httpClient
            .GetFromJsonAsync<List<Product>>("https://api.example.com/products");

        // Sync with database (Insert/Update/Delete as needed)
        _database.SyncWith(
            source: apiProducts!,
            keySelector: p => p.Id,
            comparer: new ProductComparer()
        );

        Console.WriteLine("Products synchronized successfully");
    }
}

// Usage
var service = new ProductSyncService(database, httpClient);
await service.SyncProductsAsync();
```

### Example 2: CSV Import with Change Tracking

```csharp
public class CsvImporter
{
    public void ImportProducts(string csvFilePath)
    {
        // Read CSV file
        var csvProducts = ReadCsvFile<Product>(csvFilePath);

        // Get current database state
        var dbProducts = _database.Table<Product>().ToList();

        // Compare
        var comparer = new StorageableComparer<Product>();
        var result = comparer.Compare(
            source: csvProducts,
            target: dbProducts,
            keySelector: p => p.Sku // Use SKU as key
        );

        // Report changes
        Console.WriteLine($"New products: {result.ToInsert.Count}");
        Console.WriteLine($"Updated products: {result.ToUpdate.Count}");
        Console.WriteLine($"Discontinued products: {result.ToDelete.Count}");

        // Execute sync
        _database.SyncWith(csvProducts, p => p.Sku);
    }
}
```

### Example 3: Master-Detail Synchronization

```csharp
public class OrderSyncService
{
    public void SyncOrder(Order apiOrder)
    {
        // Sync order header
        var dbOrder = _database.Table<Order>()
            .Where(o => o.OrderId == apiOrder.OrderId)
            .FirstOrDefault();

        if (dbOrder == null)
        {
            _database.Table<Order>().Add(apiOrder);
        }
        else
        {
            _database.Table<Order>().Update(apiOrder);
        }

        // Sync order lines (handle additions, updates, deletions)
        var dbLines = _database.Table<OrderLine>()
            .Where(line => line.OrderId == apiOrder.OrderId)
            .ToList();

        _database.SyncWith(
            source: apiOrder.OrderLines,
            keySelector: line => new { line.OrderId, line.LineNumber }
        );
    }
}
```

### Example 4: Incremental ETL Pipeline

```csharp
public class EtlPipeline
{
    public async Task ExecuteAsync()
    {
        // Extract: Get data from source system
        var sourceRecords = await ExtractFromSourceAsync();

        // Transform: Clean and standardize data
        var transformedRecords = sourceRecords
            .Select(r => new TargetRecord
            {
                Id = r.SourceId,
                Name = CleanName(r.RawName),
                Value = NormalizeValue(r.RawValue)
            })
            .ToList();

        // Load: Compare and sync with target database
        var comparer = new StorageableComparer<TargetRecord>();
        var dbRecords = _database.Table<TargetRecord>().ToList();

        var result = comparer.Compare(
            source: transformedRecords,
            target: dbRecords,
            keySelector: r => r.Id
        );

        // Log changes
        _logger.LogInformation(
            "ETL: {Insert} inserts, {Update} updates, {Delete} deletes",
            result.ToInsert.Count,
            result.ToUpdate.Count,
            result.ToDelete.Count
        );

        // Apply changes
        _database.SyncWith(transformedRecords, r => r.Id);
    }
}
```

### Example 5: Data Reconciliation with Audit

```csharp
public class ReconciliationService
{
    public ReconciliationReport Reconcile(List<Product> masterData)
    {
        var currentData = _database.Table<Product>().ToList();

        var comparer = new StorageableComparer<Product>();
        var result = comparer.Compare(
            source: masterData,
            target: currentData,
            keySelector: p => p.Sku
        );

        // Audit trail
        foreach (var inserted in result.ToInsert)
        {
            _auditLog.LogChange("INSERT", "Product", inserted.Sku, null, inserted);
        }

        foreach (var updated in result.ToUpdate)
        {
            var old = currentData.First(p => p.Sku == updated.Sku);
            _auditLog.LogChange("UPDATE", "Product", updated.Sku, old, updated);
        }

        foreach (var deleted in result.ToDelete)
        {
            _auditLog.LogChange("DELETE", "Product", deleted.Sku, deleted, null);
        }

        // Apply changes
        _database.SyncWith(masterData, p => p.Sku);

        return new ReconciliationReport
        {
            Inserted = result.ToInsert.Count,
            Updated = result.ToUpdate.Count,
            Deleted = result.ToDelete.Count,
            Timestamp = DateTime.UtcNow
        };
    }
}
```

## Best Practices

### 1. Choose the Right Key

```csharp
// ✅ GOOD: Stable, immutable identifier
keySelector: p => p.Sku

// ✅ GOOD: Composite natural key
keySelector: o => new { o.Year, o.Month, o.AccountCode }

// ❌ BAD: Auto-increment ID from external source (may change)
keySelector: p => p.Id

// ❌ BAD: Mutable value
keySelector: p => p.Name
```

### 2. Use Custom Comparers for Value Equality

```csharp
// ❌ BAD: Default comparer uses reference equality
var result = comparer.Compare(source, target, p => p.Id);
// Everything marked as "ToUpdate" even if identical

// ✅ GOOD: Custom comparer checks actual values
var result = comparer.Compare(
    source, target, 
    p => p.Id,
    comparer: new ProductValueComparer()
);
```

### 3. Handle Large Datasets Efficiently

```csharp
// For millions of records, use batch processing
public void SyncInBatches(List<Product> source, int batchSize = 1000)
{
    var target = _database.Table<Product>().ToList();
    var comparer = new StorageableComparer<Product>();

    for (int i = 0; i < source.Count; i += batchSize)
    {
        var batch = source.Skip(i).Take(batchSize).ToList();
        var relevantTarget = target
            .Where(t => batch.Any(b => b.Id == t.Id))
            .ToList();

        var result = comparer.Compare(batch, relevantTarget, p => p.Id);

        // Execute batch operations
        if (result.ToInsert.Any())
            _database.Table<Product>().AddRange(result.ToInsert);
        
        if (result.ToUpdate.Any())
            _database.Table<Product>().UpdateRange(result.ToUpdate);
        
        if (result.ToDelete.Any())
            _database.Table<Product>().DeleteRange(result.ToDelete);
    }
}
```

### 4. Validate Before Sync

```csharp
public void SafeSync(List<Product> source)
{
    var target = _database.Table<Product>().ToList();
    var comparer = new StorageableComparer<Product>();
    var result = comparer.Compare(source, target, p => p.Id);

    // Sanity checks
    if (result.ToDelete.Count > target.Count * 0.5)
    {
        throw new InvalidOperationException(
            "Refusing to delete more than 50% of records. Manual review required.");
    }

    if (result.ToInsert.Count > 10000)
    {
        _logger.LogWarning("Large insertion detected: {Count} records", 
            result.ToInsert.Count);
    }

    // Proceed if valid
    _database.SyncWith(source, p => p.Id);
}
```

### 5. Log Changes for Traceability

```csharp
public void SyncWithLogging(List<Product> source)
{
    var target = _database.Table<Product>().ToList();
    var comparer = new StorageableComparer<Product>();
    var result = comparer.Compare(source, target, p => p.Id);

    _logger.LogInformation(
        "Sync starting: {Insert} inserts, {Update} updates, {Delete} deletes",
        result.ToInsert.Count,
        result.ToUpdate.Count,
        result.ToDelete.Count
    );

    try
    {
        _database.SyncWith(source, p => p.Id);
        _logger.LogInformation("Sync completed successfully");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Sync failed");
        throw;
    }
}
```

## Performance Considerations

### Time Complexity

- **Compare operation**: O(n + m) where n = source size, m = target size
- **Key extraction**: O(n + m)
- **Set operations**: O(n + m) using hash sets
- **Value comparison**: O(k) where k = number of matching keys

### Memory Usage

- Stores all source, target, and result collections in memory
- For very large datasets (millions of records), consider:
  - Batch processing
  - Streaming comparisons
  - Database-side comparisons using MERGE or UPSERT

### Database Operations

- SyncWith executes INSERT, UPDATE, DELETE in separate batches
- All operations wrapped in transaction (atomic)
- Use bulk operations for better performance on large changesets

## Limitations

1. **In-Memory Operations**: Both collections loaded into memory
2. **No Partial Updates**: Updates entire record, not individual fields
3. **Single Table**: Does not cascade to related tables automatically
4. **No Conflict Resolution**: Last-write-wins, no merge strategies
5. **Key Immutability**: Assumes keys don't change between source and target

## Related Documentation

- [Bulk Operations](BulkOperations.md)
- [Change Tracking](ChangeTracking.md)
- [Database API](Database-API.md)
- [Transaction Management](Transactions.md)
