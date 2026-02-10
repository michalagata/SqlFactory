# Optimistic Concurrency Guide

## Overview

Optimistic Concurrency is a database pattern that prevents lost updates by detecting when a record has been modified by another transaction between read and write operations. SQLFactory implements optimistic concurrency using row versioning.

## Quick Start

### 1. Add RowVersion Property

```csharp
using AnubisWorks.SQLFactory;

[Table(Name = "Product")]
public class Product
{
    [Column(IsPrimaryKey = true, IsDbGenerated = true)]
    public int Id { get; set; }
    
    [Column]
    public string Name { get; set; }
    
    [Column]
    public decimal Price { get; set; }
    
    // Row version for optimistic concurrency
    [Column]
    [RowVersion]  // or [Timestamp]
    public long Version { get; set; }
}
```

### 2. Database Schema

```sql
CREATE TABLE Product (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Price DECIMAL(18,2) NOT NULL,
    Version INTEGER DEFAULT 1  -- or BIGINT
);
```

### 3. Handle Concurrency Conflicts

```csharp
using var db = new Database(connection);

try
{
    var product = db.Table<Product>().Find(1);
    product.Price = 999.99m;
    
    db.Table<Product>().Update(product);
}
catch (DbConcurrencyException ex)
{
    // Another user modified this record
    Console.WriteLine("Concurrency conflict detected!");
    // Handle conflict (retry, merge, or abort)
}
```

## How It Works

### Automatic Version Checking

When you update an entity with a `[RowVersion]` property:

1. **Read:** Load entity with current version
   ```sql
   SELECT Id, Name, Price, Version FROM Product WHERE Id = 1
   -- Returns: Version = 5
   ```

2. **Modify:** Change entity in memory
   ```csharp
   product.Price = 999.99m;  // Version still = 5
   ```

3. **Update:** SQLFactory generates SQL with version check
   ```sql
   UPDATE Product 
   SET Price = 999.99, Version = Version + 1
   WHERE Id = 1 AND Version = 5
   ```

4. **Verify:** If 0 rows affected → `DbConcurrencyException`

### Version Increment

SQLFactory automatically:
- Increments version on every UPDATE
- Checks version matches in WHERE clause
- Works with `int`, `long`, `short` types

## API Reference

### Attributes

#### [RowVersion]

Marks a property as a row version column for optimistic concurrency.

```csharp
[RowVersion]
public long Version { get; set; }
```

**Supported Types:**
- `int`
- `long` (recommended)
- `short`

#### [Timestamp]

Alias for `[RowVersion]`. Provides SQL Server-style naming.

```csharp
[Timestamp]
public long RowVersion { get; set; }
```

### DbConcurrencyException

Exception thrown when a concurrency conflict is detected.

```csharp
public class DbConcurrencyException : Exception
{
    public object Entity { get; }
    public object ExpectedVersion { get; }
    public object ActualVersion { get; }
}
```

**Properties:**
- `Entity` - The entity that failed to update
- `ExpectedVersion` - Version from your in-memory entity
- `ActualVersion` - Current version in database (if available)
- `Message` - Detailed error description

## Conflict Resolution Strategies

### Strategy 1: Client Wins (Force Update)

Overwrite database with your changes, ignoring concurrent modifications.

```csharp
void SaveWithClientWins(Product product)
{
    bool saved = false;
    int retries = 3;
    
    while (!saved && retries > 0)
    {
        try
        {
            db.Table<Product>().Update(product);
            saved = true;
        }
        catch (DbConcurrencyException)
        {
            // Reload to get current version
            var current = db.Table<Product>().Find(product.Id);
            product.Version = current.Version;  // Use current version
            retries--;
        }
    }
}
```

### Strategy 2: Database Wins (Discard Changes)

Reload from database and discard your changes.

```csharp
void SaveWithDatabaseWins(Product product)
{
    try
    {
        db.Table<Product>().Update(product);
    }
    catch (DbConcurrencyException)
    {
        // Reload fresh data from database
        var fresh = db.Table<Product>().Find(product.Id);
        // Notify user that their changes were discarded
        Console.WriteLine("Your changes were discarded. Database version loaded.");
        return;
    }
}
```

### Strategy 3: Merge Changes

Apply both sets of changes intelligently.

```csharp
void SaveWithMerge(Product product, decimal originalPrice)
{
    try
    {
        db.Table<Product>().Update(product);
    }
    catch (DbConcurrencyException)
    {
        var current = db.Table<Product>().Find(product.Id);
        
        // If database value changed, keep it
        if (current.Price != originalPrice)
        {
            Console.WriteLine($"Price was changed by another user to {current.Price}");
            product.Price = current.Price;
        }
        
        // Update other fields and retry
        product.Version = current.Version;
        db.Table<Product>().Update(product);
    }
}
```

### Strategy 4: User Decision

Prompt user to choose which version to keep.

```csharp
void SaveWithUserChoice(Product product)
{
    try
    {
        db.Table<Product>().Update(product);
    }
    catch (DbConcurrencyException ex)
    {
        var current = db.Table<Product>().Find(product.Id);
        
        Console.WriteLine("Conflict detected!");
        Console.WriteLine($"Your version: {product.Price}");
        Console.WriteLine($"Database version: {current.Price}");
        Console.WriteLine("1. Keep your changes");
        Console.WriteLine("2. Keep database version");
        
        var choice = Console.ReadLine();
        
        if (choice == "1")
        {
            product.Version = current.Version;
            db.Table<Product>().Update(product);
        }
        else
        {
            // User chose database version, discard changes
        }
    }
}
```

## Provider-Specific Support

### SQLite (Manual Version Column)

```sql
CREATE TABLE Product (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Price REAL NOT NULL,
    Version INTEGER DEFAULT 1
);
```

```csharp
[RowVersion]
public long Version { get; set; }
```

### SQL Server (ROWVERSION)

SQL Server provides a built-in `ROWVERSION` type (formerly `TIMESTAMP`):

```sql
CREATE TABLE Product (
    Id INT PRIMARY KEY IDENTITY,
    Name NVARCHAR(100) NOT NULL,
    Price DECIMAL(18,2) NOT NULL,
    RowVersion ROWVERSION NOT NULL
);
```

```csharp
[RowVersion]
[Column(UpdateCheck = UpdateCheck.Always)]
public byte[] RowVersion { get; set; }
```

**Note:** SQL Server's `ROWVERSION` is a `byte[]` (8 bytes), automatically updated by SQL Server.

### PostgreSQL (xmin)

PostgreSQL provides a system column `xmin` for concurrency:

```csharp
[Column(Name = "xmin")]
[RowVersion]
public long TransactionId { get; set; }
```

Query with xmin:
```sql
SELECT *, xmin FROM Product WHERE Id = 1;
```

Update with xmin check:
```sql
UPDATE Product 
SET Name = 'Updated', Price = 999.99
WHERE Id = 1 AND xmin = 12345;
```

### MySQL (Timestamp)

```sql
CREATE TABLE Product (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    Name VARCHAR(100) NOT NULL,
    Price DECIMAL(18,2) NOT NULL,
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    Version BIGINT DEFAULT 1
);
```

```csharp
[RowVersion]
public long Version { get; set; }

// Or use timestamp
[Column]
public DateTime UpdatedAt { get; set; }
```

## Best Practices

### 1. Always Use Long for Version

```csharp
// ✅ Good - won't overflow
[RowVersion]
public long Version { get; set; }

// ⚠️ Risky - may overflow on high-traffic tables
[RowVersion]
public int Version { get; set; }
```

### 2. Handle Exceptions Gracefully

```csharp
try
{
    db.Table<Product>().Update(product);
}
catch (DbConcurrencyException ex)
{
    // Log the conflict
    logger.LogWarning(ex, "Concurrency conflict for Product {Id}", product.Id);
    
    // Inform the user
    throw new ApplicationException(
        "This record was modified by another user. Please refresh and try again.", ex);
}
```

### 3. Keep Version Read-Only in UI

Don't let users manually edit the version field:

```csharp
[RowVersion]
[Editable(false)]  // Make it read-only in forms
public long Version { get; set; }
```

### 4. Initialize Version on Insert

```sql
-- SQLite
CREATE TABLE Product (
    ...,
    Version INTEGER DEFAULT 1
);

-- SQL Server
CREATE TABLE Product (
    ...,
    Version BIGINT DEFAULT 1
);
```

### 5. Consider Retry Limits

Avoid infinite retry loops:

```csharp
int maxRetries = 3;
int attempt = 0;

while (attempt < maxRetries)
{
    try
    {
        db.Table<Product>().Update(product);
        break;  // Success
    }
    catch (DbConcurrencyException)
    {
        attempt++;
        if (attempt >= maxRetries)
            throw;  // Give up after max retries
        
        Thread.Sleep(100 * attempt);  // Exponential backoff
    }
}
```

## Common Scenarios

### Scenario 1: E-commerce Inventory

Prevent overselling by detecting concurrent purchases:

```csharp
public void PurchaseProduct(int productId, int quantity)
{
    var product = db.Table<Product>().Find(productId);
    
    if (product.Stock < quantity)
        throw new InvalidOperationException("Insufficient stock");
    
    product.Stock -= quantity;
    
    try
    {
        db.Table<Product>().Update(product);
    }
    catch (DbConcurrencyException)
    {
        // Another customer bought simultaneously
        throw new ApplicationException("This product was just purchased by another customer. Please try again.");
    }
}
```

### Scenario 2: Multi-User Document Editing

```csharp
public void SaveDocument(Document document)
{
    try
    {
        db.Table<Document>().Update(document);
    }
    catch (DbConcurrencyException)
    {
        var current = db.Table<Document>().Find(document.Id);
        
        Console.WriteLine($"Document was modified by {current.LastModifiedBy} at {current.LastModifiedAt}");
        Console.WriteLine("Your version may be outdated. Do you want to overwrite?");
        
        // Prompt user for action
    }
}
```

### Scenario 3: Bank Account Transactions

```csharp
public void Transfer(int fromAccountId, int toAccountId, decimal amount)
{
    using var tx = db.BeginTransaction();
    
    try
    {
        var fromAccount = db.Table<Account>().Find(fromAccountId);
        var toAccount = db.Table<Account>().Find(toAccountId);
        
        fromAccount.Balance -= amount;
        toAccount.Balance += amount;
        
        db.Table<Account>().Update(fromAccount);
        db.Table<Account>().Update(toAccount);
        
        tx.Commit();
    }
    catch (DbConcurrencyException)
    {
        tx.Rollback();
        throw new ApplicationException("Account was modified during transfer. Transaction rolled back.");
    }
}
```

## Troubleshooting

### Issue: Version not incrementing

**Cause:** Column not configured correctly in database.

**Solution:** Ensure default value or auto-increment:
```sql
Version INTEGER DEFAULT 1
```

### Issue: DbConcurrencyException on first update

**Cause:** Version is 0 or NULL in entity.

**Solution:** Ensure Insert sets initial version:
```csharp
// After insert
Assert.That(product.Version, Is.GreaterThan(0));
```

### Issue: Version overflow

**Cause:** Using `int` instead of `long` on high-traffic table.

**Solution:** Use `long`:
```csharp
[RowVersion]
public long Version { get; set; }
```

### Issue: Concurrency check not working

**Cause:** Database.Configuration.UseVersionMember is false.

**Solution:** Enable version checking (enabled by default):
```csharp
db.Configuration.UseVersionMember = true;
```

## Performance Considerations

- **Minimal overhead:** Version check adds one comparison to WHERE clause
- **Index version column:** For performance on large tables
  ```sql
  CREATE INDEX IX_Product_Version ON Product(Version);
  ```
- **Batch updates:** Each update increments version independently

## See Also

- [Change Tracking](docs/ChangeTracking.md) - Entity state management
- [Transactions](docs/Transactions.md) - ACID guarantees
- [Soft Delete](docs/SoftDelete.md) - Logical record deletion
