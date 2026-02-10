# Unit of Work Pattern User Guide

## Overview

SQLFactory's Unit of Work pattern provides robust transaction management with automatic rollback, scope-based lifecycle, and comprehensive error handling. It coordinates multiple database operations into atomic units of work.

## Why Unit of Work?

### Problems with Manual Transactions
- **Easy to Forget Cleanup**: Transactions left open consume resources
- **Complex Error Handling**: Need try-catch-finally everywhere
- **Nested Transaction Issues**: Hard to coordinate multiple operations
- **No Scope Isolation**: Difficult to track operation boundaries

### Unit of Work Benefits
- ✅ **Automatic Cleanup**: Transactions auto-rollback on errors
- ✅ **Scope-Based Lifecycle**: Clear operation boundaries
- ✅ **Explicit Commit**: Only completed scopes are committed
- ✅ **Multiple Operations**: Coordinate inserts, updates, deletes
- ✅ **Connection Pooling**: Efficient resource management

## Basic Usage

### 1. Simple Transaction Scope

```csharp
using AnubisWorks.SQLFactory.UnitOfWork;

var connectionString = "Data Source=mydb.db";

// Create Unit of Work
using (var uow = UnitOfWorkFactory.Create(connectionString)) {
    // Create scope (auto-begins transaction)
    using (var scope = uow.CreateScope()) {
        var db = uow.Database;
        
        // Multiple operations in one transaction
        db.Table<Product>().Add(new Product { Name = "Widget", Price = 19.99m });
        db.Table<Category>().Update(new Category { Id = 1, Name = "Electronics" });
        
        // Mark scope as complete
        scope.Complete();
    }
    // Transaction committed if Complete() was called, rolled back otherwise
}
```

### 2. Automatic Rollback on Error

```csharp
using (var uow = UnitOfWorkFactory.Create(connectionString)) {
    using (var scope = uow.CreateScope()) {
        var db = uow.Database;
        
        db.Table<Product>().Add(new Product { Name = "Widget", Price = 19.99m });
        
        // Exception occurs - no Complete() called
        throw new Exception("Something went wrong");
        
        scope.Complete();  // Never reached
    }
    // Transaction automatically rolled back
}
```

### 3. Multiple Scopes (Nested Operations)

```csharp
using (var uow = UnitOfWorkFactory.Create(connectionString)) {
    // First scope
    using (var scope1 = uow.CreateScope()) {
        uow.Database.Table<Product>().Add(new Product { Name = "Widget" });
        scope1.Complete();
    }
    
    // Second scope (separate transaction)
    using (var scope2 = uow.CreateScope()) {
        uow.Database.Table<Category>().Add(new Category { Name = "Tools" });
        scope2.Complete();
    }
}
```

### 4. Explicit Transaction Control

```csharp
using (var uow = UnitOfWorkFactory.Create(connectionString)) {
    // Manual transaction management
    uow.BeginTransaction();
    
    try {
        uow.Database.Table<Product>().Add(new Product { Name = "Widget" });
        uow.Database.Table<Product>().Add(new Product { Name = "Gadget" });
        
        uow.Commit();  // Commit on success
    }
    catch {
        uow.Rollback();  // Rollback on error
        throw;
    }
}
```

## Advanced Features

### 1. Custom Isolation Levels

```csharp
// Create with specific isolation level
using (var uow = UnitOfWorkFactory.Create(
    connectionString, 
    IsolationLevel.ReadCommitted)) {
    
    using (var scope = uow.CreateScope()) {
        // Operations with ReadCommitted isolation
        var products = uow.Database.Table<Product>().ToList();
        scope.Complete();
    }
}

// Or set per transaction
using (var uow = UnitOfWorkFactory.Create(connectionString)) {
    uow.BeginTransaction(IsolationLevel.Serializable);
    // ... operations
    uow.Commit();
}
```

### 2. SaveChanges Pattern

```csharp
using (var uow = UnitOfWorkFactory.Create(connectionString)) {
    var db = uow.Database;
    
    // Multiple operations
    db.Table<Product>().Add(new Product { Name = "Widget" });
    db.Table<Product>().Add(new Product { Name = "Gadget" });
    
    // SaveChanges creates transaction, commits, and releases it
    uow.SaveChanges();
    
    // More operations
    db.Table<Category>().Add(new Category { Name = "Tools" });
    uow.SaveChanges();
}
```

### 3. Access Current Transaction

```csharp
using (var uow = UnitOfWorkFactory.Create(connectionString)) {
    using (var scope = uow.CreateScope()) {
        // Check if transaction is active
        if (uow.Transaction != null) {
            Console.WriteLine($"Transaction active: {uow.Transaction.IsolationLevel}");
        }
        
        // Use transaction directly if needed
        var transaction = uow.Transaction;
        
        scope.Complete();
    }
}
```

## Complete Example: Order Processing

```csharp
using System;
using System.Collections.Generic;
using AnubisWorks.SQLFactory;
using AnubisWorks.SQLFactory.UnitOfWork;

public class OrderProcessor {
    private readonly string _connectionString;
    
    public OrderProcessor(string connectionString) {
        _connectionString = connectionString;
    }
    
    public void ProcessOrder(Order order, List<OrderItem> items) {
        using (var uow = UnitOfWorkFactory.Create(_connectionString)) {
            using (var scope = uow.CreateScope()) {
                var db = uow.Database;
                
                try {
                    // 1. Insert order
                    db.Table<Order>().Add(order);
                    Console.WriteLine($"Order {order.Id} created");
                    
                    // 2. Insert order items
                    foreach (var item in items) {
                        item.OrderId = order.Id;
                        db.Table<OrderItem>().Add(item);
                        
                        // 3. Update inventory
                        var product = db.Table<Product>().Find(item.ProductId);
                        product.StockQuantity -= item.Quantity;
                        db.Table<Product>().Update(product);
                        
                        Console.WriteLine($"  Item: {item.ProductName} x{item.Quantity}");
                    }
                    
                    // 4. Update order total
                    order.Total = items.Sum(i => i.UnitPrice * i.Quantity);
                    db.Table<Order>().Update(order);
                    
                    Console.WriteLine($"Order total: ${order.Total}");
                    
                    // All operations succeed - commit
                    scope.Complete();
                    Console.WriteLine("Order processed successfully");
                }
                catch (Exception ex) {
                    // Any error - entire transaction rolls back
                    Console.WriteLine($"Order processing failed: {ex.Message}");
                    throw;
                }
            }
        }
    }
    
    public void CancelOrder(int orderId) {
        using (var uow = UnitOfWorkFactory.Create(_connectionString)) {
            using (var scope = uow.CreateScope()) {
                var db = uow.Database;
                
                // 1. Get order and items
                var order = db.Table<Order>().Find(orderId);
                var items = db.Table<OrderItem>()
                    .Where(i => i.OrderId == orderId)
                    .ToList();
                
                // 2. Restore inventory
                foreach (var item in items) {
                    var product = db.Table<Product>().Find(item.ProductId);
                    product.StockQuantity += item.Quantity;
                    db.Table<Product>().Update(product);
                }
                
                // 3. Delete order items
                foreach (var item in items) {
                    db.Table<OrderItem>().Remove(item);
                }
                
                // 4. Delete order
                db.Table<Order>().Remove(order);
                
                scope.Complete();
                Console.WriteLine($"Order {orderId} cancelled");
            }
        }
    }
}

public class Order {
    public int Id { get; set; }
    public string CustomerName { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OrderItem {
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class Product {
    public int Id { get; set; }
    public string Name { get; set; }
    public int StockQuantity { get; set; }
    public decimal Price { get; set; }
}
```

## Best Practices

### 1. Always Use Scopes
```csharp
// ✅ GOOD - Scope ensures cleanup
using (var uow = UnitOfWorkFactory.Create(connectionString)) {
    using (var scope = uow.CreateScope()) {
        // ... operations
        scope.Complete();
    }
}

// ❌ BAD - Manual transaction management is error-prone
using (var uow = UnitOfWorkFactory.Create(connectionString)) {
    uow.BeginTransaction();
    // ... operations (what if exception occurs?)
    uow.Commit();  // May never be reached
}
```

### 2. Call Complete() Only on Success
```csharp
using (var scope = uow.CreateScope()) {
    if (ValidationFails()) {
        // Don't call Complete() - transaction will rollback
        return;
    }
    
    uow.Database.Table<Product>().Add(product);
    
    scope.Complete();  // Only called if validation passes
}
```

### 3. Keep Scopes Short
```csharp
// ✅ GOOD - Short-lived scope
using (var scope = uow.CreateScope()) {
    uow.Database.Table<Product>().Add(product);
    scope.Complete();
}

// ❌ BAD - Long-lived scope holds transaction
using (var scope = uow.CreateScope()) {
    // ... lots of business logic
    // ... API calls
    // ... file operations
    uow.Database.Table<Product>().Add(product);
    scope.Complete();
}
```

### 4. One Database per UnitOfWork
```csharp
// ✅ GOOD - Single database instance
using (var uow = UnitOfWorkFactory.Create(connectionString)) {
    var db = uow.Database;
    
    using (var scope = uow.CreateScope()) {
        db.Table<Product>().Add(product1);
        db.Table<Product>().Add(product2);
        scope.Complete();
    }
}

// ❌ BAD - Accessing Database multiple times
using (var uow = UnitOfWorkFactory.Create(connectionString)) {
    using (var scope = uow.CreateScope()) {
        uow.Database.Table<Product>().Add(product1);  // Creates connection
        uow.Database.Table<Product>().Add(product2);  // Same connection
        scope.Complete();
    }
}
```

## Error Handling

### 1. Catch and Log Errors
```csharp
using (var uow = UnitOfWorkFactory.Create(connectionString)) {
    using (var scope = uow.CreateScope()) {
        try {
            uow.Database.Table<Product>().Add(product);
            scope.Complete();
        }
        catch (Exception ex) {
            logger.LogError($"Failed to add product: {ex.Message}");
            // Transaction automatically rolled back
            throw;  // Re-throw to caller
        }
    }
}
```

### 2. Partial Success Handling
```csharp
var successfulProducts = new List<Product>();
var failedProducts = new List<Product>();

foreach (var product in products) {
    using (var uow = UnitOfWorkFactory.Create(connectionString)) {
        using (var scope = uow.CreateScope()) {
            try {
                uow.Database.Table<Product>().Add(product);
                scope.Complete();
                successfulProducts.Add(product);
            }
            catch {
                failedProducts.Add(product);
            }
        }
    }
}

Console.WriteLine($"Success: {successfulProducts.Count}, Failed: {failedProducts.Count}");
```

## Performance Considerations

- **Connection Pooling**: Connections are reused from the pool
- **Lazy Initialization**: Database connection created on first access
- **Transaction Overhead**: ~1-5ms per transaction (depends on isolation level)
- **Scope Overhead**: Minimal (<100ns for scope creation/disposal)

### Benchmarks

```
BenchmarkDotNet=v0.13.0

|                Method |     Mean |   StdDev |
|---------------------- |---------:|---------:|
|       ScopeCreation   |   80 ns  |   5 ns   |
|  SaveChangesOverhead  | 1.2 ms   |  0.1 ms  |
|  10 Operations/Scope  | 12 ms    |  1 ms    |
```

## Integration with Other Features

### With Multi-Tenant Support
```csharp
var tenantManager = new TenantManager();
tenantManager.AddTenant(new TenantConfig {
    TenantId = "acme",
    ConnectionString = "Data Source=acme.db"
});

// Use UnitOfWork per tenant
tenantManager.WithTenant("acme", db => {
    using (var scope = UnitOfWorkFactory.CreateFromDatabase(db).CreateScope()) {
        db.Table<Product>().Add(product);
        scope.Complete();
    }
});
```

### With Snowflake IDs
```csharp
var generator = new SnowflakeIdGenerator(workerId: 1);

using (var uow = UnitOfWorkFactory.Create(connectionString)) {
    using (var scope = uow.CreateScope()) {
        var product = new Product {
            Id = generator.NextId(),  // Snowflake ID
            Name = "Widget"
        };
        
        uow.Database.Table<Product>().Add(product);
        scope.Complete();
    }
}
```

## Troubleshooting

### "Transaction is already active"
**Cause**: BeginTransaction() called twice  
**Solution**: Use scopes instead of manual transaction control

### "Scope is already completed"
**Cause**: Complete() called twice on same scope  
**Solution**: Call Complete() only once per scope

### "Connection is closed"
**Cause**: Accessing Database after UnitOfWork disposal  
**Solution**: Keep all operations within using block

### Deadlocks
**Cause**: Multiple transactions accessing same resources  
**Solution**: Use consistent locking order, shorter transactions

## Comparison with Repository Pattern

SQLFactory includes a legacy Repository-based UnitOfWork (`Core/Repository/UnitOfWork.cs`) and the new standalone pattern (`Core/UnitOfWork/`).

| Feature | New UnitOfWork | Repository UnitOfWork |
|---------|----------------|----------------------|
| **Independence** | Standalone | Tied to Repository |
| **Scope-based** | ✅ Yes | ❌ No |
| **Auto-rollback** | ✅ Yes | Manual |
| **SaveChanges** | ✅ Yes | Via Repository |
| **Isolation Levels** | ✅ Configurable | Fixed |

**Recommendation**: Use the new standalone UnitOfWork for new code.

## Related Features

- [Multi-Tenant Support](./MultiTenant.md) - Tenant-scoped transactions
- [Snowflake ID Generator](./SnowflakeId.md) - Unique IDs in transactions
- [AOP Events](./AOP.md) - Audit transaction operations

## API Reference

### UnitOfWorkFactory
```csharp
static IUnitOfWork Create(string connectionString, IsolationLevel? isolationLevel = null)
static IUnitOfWork CreateFromDatabase(Database database)
```

### IUnitOfWork Methods
- `IUnitOfWorkScope CreateScope()` - Create transaction scope
- `void BeginTransaction(IsolationLevel? level = null)` - Start transaction
- `void Commit()` - Commit active transaction
- `void Rollback()` - Rollback active transaction
- `void SaveChanges()` - Commit and release transaction

### IUnitOfWork Properties
- `Database Database` - Underlying database connection
- `IDbConnection Connection` - Raw database connection
- `IDbTransaction Transaction` - Active transaction (if any)

### IUnitOfWorkScope Methods
- `void Complete()` - Mark scope as successful (commits on disposal)
- `void Dispose()` - End scope (commits if Complete() was called, rollbacks otherwise)

### Exceptions
- `InvalidOperationException` - Transaction already active, scope already completed
- `ObjectDisposedException` - Operations on disposed UnitOfWork
