# Multi-Tenant Support User Guide

## Overview

SQLFactory provides first-class multi-tenancy support with complete database isolation per tenant. Each tenant can have its own database connection, allowing true physical data separation.

## Key Components

### 1. TenantManager
Central manager for tenant configurations and connection routing.

```csharp
var tenantManager = new TenantManager();

// Add tenants with their database configurations
tenantManager.AddTenant(new TenantConfig {
    TenantId = "acme",
    ConnectionString = "Data Source=acme.db",
    ProviderName = "Microsoft.Data.Sqlite"
});

tenantManager.AddTenant(new TenantConfig {
    TenantId = "globex",
    ConnectionString = "Data Source=globex.db",
    ProviderName = "Microsoft.Data.Sqlite"
});
```

### 2. Tenant-Scoped Database Access

#### ForTenant() - Direct Access
```csharp
// Create a database instance for a specific tenant
using (var db = tenantManager.ForTenant("acme")) {
    var products = db.Table<Product>().ToList();
    Console.WriteLine($"ACME has {products.Count} products");
}
```

#### WithTenant() - Scoped Execution
```csharp
// Execute actions with automatic disposal
tenantManager.WithTenant("acme", db => {
    var product = new Product { Name = "Widget", Price = 19.99m };
    db.Table<Product>().Add(product);
});

// Execute functions with return values
var count = tenantManager.WithTenant("globex", db => {
    return db.Table<Product>().Count();
});
```

### 3. Ambient Tenant Context (Advanced)

Use `AmbientTenantResolver` for automatic tenant resolution:

```csharp
var resolver = new AmbientTenantResolver();
var tenantManager = new TenantManager(resolver);

// Set ambient tenant context
using (resolver.BeginScope("acme")) {
    // All operations within this scope use the ACME tenant
    var db = tenantManager.ForTenant(resolver.GetCurrentTenantId());
    var products = db.Table<Product>().ToList();
}
```

### 4. Tenant Filtering with Attributes

Mark entities to enable automatic tenant filtering:

```csharp
[Tenant("TenantId")]
public class Order {
    public int Id { get; set; }
    public string TenantId { get; set; }  // Tenant identifier column
    public decimal Total { get; set; }
}

// Apply automatic filtering
using (var db = tenantManager.ForTenant("acme")) {
    db.ApplyTenantFilter("acme");
    
    // Only returns orders where TenantId = "acme"
    var orders = db.Table<Order>().ToList();
}
```

## Complete Example: Multi-Tenant E-Commerce

```csharp
using AnubisWorks.SQLFactory;
using AnubisWorks.SQLFactory.MultiTenant;

public class MultiTenantStore {
    private readonly TenantManager _tenantManager;
    
    public MultiTenantStore() {
        _tenantManager = new TenantManager();
        
        // Register tenants
        _tenantManager.AddTenant(new TenantConfig {
            TenantId = "store-a",
            ConnectionString = "Data Source=store_a.db",
            ProviderName = "Microsoft.Data.Sqlite"
        });
        
        _tenantManager.AddTenant(new TenantConfig {
            TenantId = "store-b",
            ConnectionString = "Data Source=store_b.db",
            ProviderName = "Microsoft.Data.Sqlite"
        });
    }
    
    public void ProcessOrder(string tenantId, Order order) {
        _tenantManager.WithTenant(tenantId, db => {
            // Order is automatically saved to the correct tenant database
            db.Table<Order>().Add(order);
            
            Console.WriteLine($"Order #{order.Id} processed for tenant {tenantId}");
        });
    }
    
    public List<Product> GetProducts(string tenantId) {
        return _tenantManager.WithTenant(tenantId, db => {
            return db.Table<Product>().ToList();
        });
    }
}

[Tenant("TenantId")]
public class Product {
    public int Id { get; set; }
    public string TenantId { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
}

[Tenant("TenantId")]
public class Order {
    public int Id { get; set; }
    public string TenantId { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

## Security Best Practices

1. **Validate Tenant IDs**: Always validate incoming tenant identifiers against a whitelist
2. **Use Separate Databases**: For maximum isolation, use physically separate database files/servers
3. **Audit Tenant Access**: Log all tenant context switches for compliance
4. **Fail Secure**: If no tenant context is available, operations should fail rather than default

## Performance Considerations

- **Connection Pooling**: Each tenant has its own connection pool
- **Lazy Loading**: Tenant connections are created on-demand
- **Disposal**: Always use `using` or `WithTenant()` to ensure proper cleanup

## Thread Safety

All TenantManager operations are thread-safe. Multiple threads can:
- Access different tenants simultaneously
- Switch contexts independently using `AmbientTenantResolver`
- Share a single TenantManager instance safely

## Async Support

Ambient tenant context flows correctly through async/await:

```csharp
var resolver = new AmbientTenantResolver();
var tenantManager = new TenantManager(resolver);

using (resolver.BeginScope("acme")) {
    await ProcessOrdersAsync();  // Context preserved across await
}

async Task ProcessOrdersAsync() {
    // Tenant context is still "acme" here
    var db = tenantManager.ForTenant(resolver.GetCurrentTenantId());
    var orders = await db.Table<Order>().ToListAsync();
}
```

## Troubleshooting

### "Tenant 'X' not found"
Ensure the tenant is registered with `TenantManager.AddTenant()` before use.

### "No current tenant context available"
When using `AmbientTenantResolver`, ensure operations are within a `BeginScope()` block.

### Connection Errors
Verify ConnectionString and ProviderName in `TenantConfig` are correct for your database.

## Related Features

- [Snowflake ID Generator](./SnowflakeId.md) - Distributed unique IDs for multi-tenant systems
- [Unit of Work](./UnitOfWork.md) - Transaction management across tenant databases
- [AOP Events](./AOP.md) - Audit tenant operations

## API Reference

### TenantManager
- `AddTenant(TenantConfig)` - Register a new tenant
- `RemoveTenant(tenantId)` - Unregister a tenant
- `GetTenant(tenantId)` - Retrieve tenant configuration
- `TenantExists(tenantId)` - Check if tenant is registered

### TenantManager Extensions
- `ForTenant(tenantId)` - Create Database for specific tenant
- `WithTenant(tenantId, action)` - Execute action with auto-disposal
- `WithTenant<T>(tenantId, func)` - Execute function returning T

### Database Extensions
- `ApplyTenantFilter(tenantId)` - Enable automatic tenant filtering

### TenantConfig Properties
- `TenantId` (required) - Unique tenant identifier
- `ConnectionString` (required) - Database connection string
- `ProviderName` (optional) - ADO.NET provider name (defaults to Microsoft.Data.Sqlite)
