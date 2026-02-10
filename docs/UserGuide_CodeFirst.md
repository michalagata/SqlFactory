# SQLFactory CodeFirst - User Guide

## Table of Contents
- [Overview](#overview)
- [Key Features](#key-features)
- [Getting Started](#getting-started)
- [InitTable Options](#inittable-options)
- [SyncStructure](#syncstructure)
- [BackupTable](#backuptable)
- [Best Practices](#best-practices)
- [Examples](#examples)
- [Safety Considerations](#safety-considerations)

## Overview

SQLFactory CodeFirst provides database schema management directly from your C# entity classes. It automatically creates and synchronizes database tables based on your model definitions, similar to Entity Framework's migrations but with a lightweight, runtime-focused approach.

### When to Use CodeFirst

- **Rapid prototyping**: Quickly iterate on schema changes during development
- **Single-database deployments**: Simple applications with straightforward schema needs
- **Runtime schema management**: Applications that need to create/modify schema on-the-fly
- **Embedded databases**: SQLite applications where schema bundling isn't practical

### When NOT to Use CodeFirst

- **Production migrations**: Use proper migration tools for production schema changes
- **Multi-environment deployments**: Use SQL migration scripts for consistency
- **Complex schema evolution**: Dedicated migration frameworks handle data preservation better

## Key Features

1. **Automatic Table Creation**: Generate tables from entity classes
2. **Schema Synchronization**: Add missing columns while preserving data
3. **Multiple Init Strategies**: Control how tables are created/updated
4. **Backup Support**: Save existing data before destructive operations
5. **Transaction Safety**: All operations wrapped in transactions
6. **Async Support**: Non-blocking schema operations

## Getting Started

### Basic Setup

```csharp
using AnubisWorks.SQLFactory;
using AnubisWorks.SQLFactory.CodeFirst;

// Define your entity
[Table(Name = "Products")]
public class Product
{
    [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)]
    public int Id { get; set; }
    
    [Column(Name = "Name")]
    public string Name { get; set; }
    
    [Column(Name = "Price")]
    public decimal Price { get; set; }
    
    [Column(Name = "CreatedAt")]
    public DateTime CreatedAt { get; set; }
}

// Initialize database
var database = new Database(connectionString);
var codeFirst = new CodeFirstManager(database);

// Create table if it doesn't exist
codeFirst.InitTable<Product>(InitTableMode.CreateOnly);
```

## InitTable Options

The `InitTable` method supports four modes that control table creation behavior:

### 1. CreateOnly (Default)

Creates the table only if it doesn't exist. Safest option for production.

```csharp
// Table will be created if missing, otherwise no action
codeFirst.InitTable<Product>(InitTableMode.CreateOnly);
```

**Use when:**
- Application first startup
- New tables being added
- Production deployments
- You want to preserve existing data

### 2. CreateAndAddColumns

Creates table if missing OR adds new columns to existing table. Preserves all existing data.

```csharp
// Create table or add missing columns
codeFirst.InitTable<Product>(InitTableMode.CreateAndAddColumns);
```

**Use when:**
- Evolving schema in development
- Adding new columns to existing tables
- Schema upgrades that only add fields
- You need backward compatibility

**Limitations:**
- Cannot remove columns
- Cannot modify column types
- Cannot add/remove constraints

### 3. CreateAndAlter

Attempts to modify existing table structure to match entity definition. USE WITH CAUTION!

```csharp
// Create or alter table structure
codeFirst.InitTable<Product>(InitTableMode.CreateAndAlter);
```

**Use when:**
- Development/testing environments only
- Schema is still unstable
- You accept potential data loss
- Quick prototyping

**Warning:**
- May lose data during alterations
- Not all alterations are supported on all databases
- SQLite has limited ALTER support
- Test thoroughly before using

### 4. DropAndRecreate

Drops existing table and recreates from scratch. DESTRUCTIVE OPERATION!

```csharp
// Drop and recreate table (data will be lost!)
codeFirst.InitTable<Product>(InitTableMode.DropAndRecreate);
```

**Use when:**
- Development/testing only
- Starting fresh after major schema changes
- Test data can be regenerated
- Never in production!

**Warning:**
- ALL DATA IS LOST
- No backup is created automatically
- Use `BackupTable` first if data is important

## SyncStructure

Synchronizes table structure with entity definition by adding missing columns.

```csharp
// Add missing columns to existing table
codeFirst.SyncStructure<Product>();

// Async version
await codeFirst.SyncStructureAsync<Product>();
```

**Features:**
- Adds columns present in entity but missing in database
- Preserves all existing data
- Transaction-safe
- Idempotent (safe to run multiple times)

**Example:**

```csharp
// Original entity
[Table(Name = "Products")]
public class Product
{
    [Column(Name = "Id", IsPrimaryKey = true)]
    public int Id { get; set; }
    
    [Column(Name = "Name")]
    public string Name { get; set; }
}

// Table created with Id and Name columns

// Updated entity - added Price column
[Table(Name = "Products")]
public class Product
{
    [Column(Name = "Id", IsPrimaryKey = true)]
    public int Id { get; set; }
    
    [Column(Name = "Name")]
    public string Name { get; set; }
    
    [Column(Name = "Price")]  // NEW COLUMN
    public decimal Price { get; set; }
}

// Synchronize schema
codeFirst.SyncStructure<Product>();
// Price column is added, existing data preserved
```

## BackupTable

Creates a backup copy of table data before destructive operations.

```csharp
// Backup table with timestamp
string backupTableName = codeFirst.BackupTable<Product>();
// Returns: "Products_backup_20260202_153045"

// Async version
string backupTableName = await codeFirst.BackupTableAsync<Product>();
```

**Features:**
- Creates table named `{OriginalTable}_backup_{timestamp}`
- Copies all data and structure
- Independent of original table
- Can be restored manually if needed

**Example:**

```csharp
// Before destructive operation
var backupName = codeFirst.BackupTable<Product>();
Console.WriteLine($"Backup created: {backupName}");

// Perform risky operation
codeFirst.InitTable<Product>(InitTableMode.DropAndRecreate);

// If something goes wrong, manually restore:
// EXEC sp_rename 'Products_backup_20260202_153045', 'Products'
// or
// CREATE TABLE Products AS SELECT * FROM Products_backup_20260202_153045
```

## Best Practices

### 1. Environment-Specific Modes

```csharp
var mode = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development"
    ? InitTableMode.CreateAndAddColumns
    : InitTableMode.CreateOnly;

codeFirst.InitTable<Product>(mode);
```

### 2. Startup Initialization

```csharp
public class DatabaseInitializer
{
    private readonly Database _database;
    private readonly CodeFirstManager _codeFirst;

    public void InitializeSchema()
    {
        // Initialize all tables at startup
        _codeFirst.InitTable<Product>(InitTableMode.CreateOnly);
        _codeFirst.InitTable<Order>(InitTableMode.CreateOnly);
        _codeFirst.InitTable<Customer>(InitTableMode.CreateOnly);
    }
}
```

### 3. Development Workflow

```csharp
#if DEBUG
// In development, sync structure automatically
codeFirst.InitTable<Product>(InitTableMode.CreateAndAddColumns);
#else
// In production, only create missing tables
codeFirst.InitTable<Product>(InitTableMode.CreateOnly);
#endif
```

### 4. Safe Schema Updates

```csharp
public async Task SafeSchemaUpdate<T>() where T : class
{
    try
    {
        // Backup first
        var backupName = await _codeFirst.BackupTableAsync<T>();
        _logger.LogInformation($"Backup created: {backupName}");

        // Sync structure
        await _codeFirst.SyncStructureAsync<T>();
        _logger.LogInformation("Schema updated successfully");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Schema update failed");
        throw;
    }
}
```

### 5. Idempotent Initialization

```csharp
// Safe to run on every startup
public void EnsureSchema()
{
    // These operations are idempotent
    codeFirst.InitTable<Product>(InitTableMode.CreateOnly);
    codeFirst.SyncStructure<Product>();
}
```

## Examples

### Example 1: Simple Table Creation

```csharp
[Table(Name = "Users")]
public class User
{
    [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)]
    public int Id { get; set; }
    
    [Column(Name = "Username")]
    public string Username { get; set; }
    
    [Column(Name = "Email")]
    public string Email { get; set; }
    
    [Column(Name = "CreatedAt")]
    public DateTime CreatedAt { get; set; }
}

var database = new Database("Data Source=myapp.db");
var codeFirst = new CodeFirstManager(database);

// Create table on first run
codeFirst.InitTable<User>(InitTableMode.CreateOnly);

// Insert data
database.Table<User>().Add(new User
{
    Username = "john_doe",
    Email = "john@example.com",
    CreatedAt = DateTime.UtcNow
});
```

### Example 2: Schema Evolution

```csharp
// Version 1.0 - Initial schema
[Table(Name = "Products")]
public class Product
{
    [Column(Name = "Id", IsPrimaryKey = true)]
    public int Id { get; set; }
    
    [Column(Name = "Name")]
    public string Name { get; set; }
    
    [Column(Name = "Price")]
    public decimal Price { get; set; }
}

codeFirst.InitTable<Product>(InitTableMode.CreateOnly);

// ... Time passes, users create data ...

// Version 2.0 - Add new fields
[Table(Name = "Products")]
public class Product
{
    [Column(Name = "Id", IsPrimaryKey = true)]
    public int Id { get; set; }
    
    [Column(Name = "Name")]
    public string Name { get; set; }
    
    [Column(Name = "Price")]
    public decimal Price { get; set; }
    
    // NEW COLUMNS in v2.0
    [Column(Name = "Category")]
    public string Category { get; set; } = "General";
    
    [Column(Name = "Stock")]
    public int Stock { get; set; } = 0;
}

// Safe upgrade - adds columns, preserves data
codeFirst.SyncStructure<Product>();

// Or use CreateAndAddColumns mode
codeFirst.InitTable<Product>(InitTableMode.CreateAndAddColumns);
```

### Example 3: Multi-Table Initialization

```csharp
public class SchemaManager
{
    private readonly CodeFirstManager _codeFirst;

    public void InitializeDatabase()
    {
        // Initialize all tables in dependency order
        var tables = new[]
        {
            typeof(Category),
            typeof(Product),
            typeof(Customer),
            typeof(Order),
            typeof(OrderItem)
        };

        foreach (var tableType in tables)
        {
            var method = typeof(CodeFirstManager)
                .GetMethod(nameof(CodeFirstManager.InitTable))
                .MakeGenericMethod(tableType);
            
            method.Invoke(_codeFirst, new object[] { InitTableMode.CreateOnly });
        }
    }
}
```

### Example 4: Development Reset

```csharp
public class DevelopmentDbReset
{
    private readonly CodeFirstManager _codeFirst;

    public async Task ResetDevelopmentDatabase()
    {
        #if DEBUG
        // Only in development!
        await _codeFirst.InitTableAsync<Product>(InitTableMode.DropAndRecreate);
        await _codeFirst.InitTableAsync<Order>(InitTableMode.DropAndRecreate);
        await _codeFirst.InitTableAsync<Customer>(InitTableMode.DropAndRecreate);

        // Seed test data
        await SeedTestData();
        #endif
    }

    private async Task SeedTestData()
    {
        // Add test products
        await _database.Table<Product>().AddRangeAsync(new[]
        {
            new Product { Name = "Test Product 1", Price = 10.99m },
            new Product { Name = "Test Product 2", Price = 25.50m }
        });
    }
}
```

### Example 5: Safe Production Update

```csharp
public class ProductionSchemaUpdater
{
    private readonly CodeFirstManager _codeFirst;
    private readonly ILogger _logger;

    public async Task<bool> TryUpdateSchema<T>() where T : class
    {
        var backupName = string.Empty;
        
        try
        {
            // 1. Create backup
            backupName = await _codeFirst.BackupTableAsync<T>();
            _logger.LogInformation($"Created backup: {backupName}");

            // 2. Sync structure
            await _codeFirst.SyncStructureAsync<T>();
            _logger.LogInformation("Schema synchronized successfully");

            // 3. Verify data integrity
            var count = await _database.Table<T>().CountAsync();
            _logger.LogInformation($"Verified {count} records");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schema update failed");
            
            if (!string.IsNullOrEmpty(backupName))
            {
                _logger.LogWarning($"Restore from backup: {backupName}");
            }
            
            return false;
        }
    }
}
```

## Safety Considerations

### Data Loss Scenarios

| Operation | Data Loss Risk | Safe for Production? |
|-----------|---------------|---------------------|
| `CreateOnly` | None | ✅ Yes |
| `CreateAndAddColumns` | None (adds only) | ✅ Yes |
| `SyncStructure` | None (adds only) | ✅ Yes |
| `CreateAndAlter` | **HIGH** - column changes | ❌ No |
| `DropAndRecreate` | **TOTAL** - everything | ❌ No |
| `BackupTable` | None | ✅ Yes |

### Transaction Behavior

All CodeFirst operations are wrapped in transactions:

```csharp
// Implicitly transactional
codeFirst.InitTable<Product>(InitTableMode.CreateAndAddColumns);

// Explicitly transactional
using (var tx = database.BeginTransaction())
{
    codeFirst.InitTable<Product>(InitTableMode.CreateOnly);
    codeFirst.InitTable<Order>(InitTableMode.CreateOnly);
    tx.Commit();
}
```

### Error Handling

```csharp
try
{
    await codeFirst.InitTableAsync<Product>(InitTableMode.CreateAndAddColumns);
}
catch (SqlException ex)
{
    // Database-specific errors
    _logger.LogError(ex, "Database error during table initialization");
}
catch (InvalidOperationException ex)
{
    // Entity configuration errors
    _logger.LogError(ex, "Invalid entity configuration");
}
catch (Exception ex)
{
    // Unexpected errors
    _logger.LogError(ex, "Unexpected error during schema operation");
}
```

### Async Best Practices

```csharp
// ✅ Good - proper async/await
public async Task InitializeAsync()
{
    await _codeFirst.InitTableAsync<Product>(InitTableMode.CreateOnly);
    await _codeFirst.InitTableAsync<Order>(InitTableMode.CreateOnly);
}

// ❌ Bad - blocking async
public void Initialize()
{
    _codeFirst.InitTableAsync<Product>(InitTableMode.CreateOnly).Wait();
}

// ✅ Good - batch with Task.WhenAll
public async Task InitializeAllAsync()
{
    await Task.WhenAll(
        _codeFirst.InitTableAsync<Product>(InitTableMode.CreateOnly),
        _codeFirst.InitTableAsync<Order>(InitTableMode.CreateOnly),
        _codeFirst.InitTableAsync<Customer>(InitTableMode.CreateOnly)
    );
}
```

## Troubleshooting

### Common Issues

**1. "Table or view not found"**
- Ensure entity has `[Table(Name = "...")]` attribute
- Check connection string database name
- Verify table name spelling

**2. "Column already exists"**
- Use `CreateOnly` or `CreateAndAddColumns` mode
- Avoid `DropAndRecreate` in production

**3. "Cannot alter table"**
- SQLite has limited ALTER support
- Some changes require table recreation
- Consider manual migration for complex changes

**4. "Data type mismatch"**
- Verify column types match database types
- Check for custom type converters
- Review ConvertTo attribute settings

### Debug Logging

```csharp
// Enable SQL logging
database.Configuration.Log = Console.Out;

// Now see generated SQL
codeFirst.InitTable<Product>(InitTableMode.CreateAndAddColumns);
// Output: CREATE TABLE IF NOT EXISTS Products ...
```

## Limitations

1. **Column Removal**: Cannot remove columns with SyncStructure
2. **Type Changes**: Cannot change column types without recreation
3. **Constraint Changes**: Limited support for modifying constraints
4. **Data Migration**: No automatic data transformation
5. **Database Support**: Features vary by database provider

## Migration Path

For production applications, consider migrating to proper migration tools:

```csharp
// Development: Use CodeFirst
#if DEBUG
codeFirst.InitTable<Product>(InitTableMode.CreateAndAddColumns);
#endif

// Production: Use migrations
#if RELEASE
// Use Entity Framework Migrations
// Or Flyway, Liquibase, DbUp, etc.
#endif
```

## Related Documentation

- [Database API Reference](Database-API.md)
- [Entity Mapping Guide](Entity-Mapping.md)
- [Migration Strategies](Migrations.md)
- [Best Practices](Best-Practices.md)
