# Multi-Tenant Support Example

Demonstrates complete data isolation using database-per-tenant architecture for SaaS applications.

## Features Demonstrated

- ✅ Configure multiple tenants with separate databases
- ✅ Automatic tenant context management
- ✅ Scoped execution with `WithTenant()`
- ✅ Ambient tenant resolution (AsyncLocal-based)
- ✅ Thread-safe tenant switching
- ✅ Query filtering by tenant

## Running the Example

```bash
cd examples/MultiTenant
dotnet run
```

## Expected Output

```
=== Multi-Tenant Support Example ===

Configuring tenants...
  ✓ Added tenant: customer-a (Acme Corp)
  ✓ Added tenant: customer-b (Beta Inc)
  ✓ Added tenant: customer-c (Gamma LLC)

=== Scenario 1: Scoped Tenant Execution ===
Executing in customer-a context:
  Products: Laptop ($1299.99), Mouse ($29.99)
  
Executing in customer-b context:
  Products: Server ($4999.99), Router ($599.99)

=== Scenario 2: Tenant Isolation ===
Total products across all tenants: 8
  customer-a: 3 products
  customer-b: 2 products
  customer-c: 3 products

=== Scenario 3: Ambient Tenant Resolution ===
Current tenant: customer-a
  Querying products... Found: 3
  
Switching to customer-b...
  Querying products... Found: 2
  
Restored to customer-a
  Querying products... Found: 3

=== Scenario 4: Concurrent Access ===
Simulating 3 concurrent requests:
  Request 1 (customer-a): 3 products
  Request 2 (customer-b): 2 products
  Request 3 (customer-c): 3 products
All requests isolated successfully ✓
```

## Architecture Patterns

### 1. Database-Per-Tenant (Highest Isolation)

```csharp
var tenantManager = new TenantManager();

// Each tenant gets its own database
tenantManager.AddTenant(new TenantConfig
{
    TenantId = "customer-a",
    ConnectionString = "Server=db1;Database=CustomerA;...",
    Description = "Acme Corp Production"
});

tenantManager.AddTenant(new TenantConfig
{
    TenantId = "customer-b",
    ConnectionString = "Server=db2;Database=CustomerB;...",
    Description = "Beta Inc Production"
});

// Complete data isolation - can't accidentally access other tenant's data
```

### 2. Scoped Execution

```csharp
// Automatic context management
tenantManager.WithTenant("customer-a", (db) =>
{
    var products = db.Query<Product>("SELECT * FROM Products").ToList();
    // Queries executed against customer-a database only
});
// Context automatically cleaned up

// Async support
await tenantManager.WithTenantAsync("customer-a", async (db) =>
{
    var products = await db.QueryAsync<Product>("SELECT * FROM Products");
    await ProcessProducts(products);
});
```

### 3. Ambient Tenant Resolution

```csharp
var resolver = new AmbientTenantResolver();

// Set ambient tenant (flows through async/await)
resolver.SetCurrentTenant("customer-a");

// All code in this context uses customer-a
var products = GetProducts();  // Uses customer-a

// Temporary tenant override
using (resolver.BeginScope("customer-b"))
{
    var betaProducts = GetProducts();  // Uses customer-b
}
// Automatically reverts to customer-a
```

### 4. Request-Scoped Tenants (ASP.NET Core)

```csharp
// In ASP.NET Core middleware
app.Use(async (context, next) =>
{
    var tenantId = context.Request.Headers["X-Tenant-ID"];
    var resolver = context.RequestServices.GetService<ITenantResolver>();
    
    resolver.SetCurrentTenant(tenantId);
    
    await next();
});

// In controllers/services - automatic tenant context
public class ProductController : Controller
{
    private readonly TenantManager _tenantManager;
    private readonly ITenantResolver _resolver;
    
    public IActionResult GetProducts()
    {
        var tenantId = _resolver.GetCurrentTenant();
        var db = _tenantManager.ForTenant(tenantId);
        
        return Ok(db.Query<Product>("SELECT * FROM Products").ToList());
    }
}
```

## Security Best Practices

1. **Never trust tenant ID from client** - Validate against authenticated user's tenants
2. **Use connection pooling** - One pool per tenant connection string
3. **Implement tenant allowlist** - Restrict which tenants a user can access
4. **Log tenant context** - Include tenant ID in all logs/traces
5. **Test cross-tenant access** - Automated tests to prevent data leakage

## Use Cases

### SaaS Applications
- Complete database isolation per customer
- Independent schema migrations per tenant
- Separate backup/restore per tenant

### Enterprise Multi-Subsidiary
- Each business unit gets separate database
- Centralized user management with tenant access control
- Consolidated reporting across tenants

### Regional Data Compliance
- Tenant databases in specific regions (GDPR, data residency)
- Control data location per customer

## Performance Considerations

| Aspect | Database-Per-Tenant | Schema-Per-Tenant | Row-Level (Single DB) |
|--------|-------------------|-------------------|---------------------|
| Isolation | ★★★★★ (Complete) | ★★★★☆ | ★★☆☆☆ |
| Scalability | ★★★★★ (Horizontal) | ★★★☆☆ | ★★★★☆ |
| Maintenance | ★★☆☆☆ (Complex) | ★★★☆☆ | ★★★★★ (Simple) |
| Cost | $$$$$ | $$$ | $ |

**Recommendation:** Database-per-tenant for <1000 tenants, hybrid approach for larger scale.

## Related Features

- **Unit of Work** - Coordinate multi-table operations per tenant
- **Global Filters** - Add tenant ID filtering for row-level isolation
- **Sharding** - Partition tenant databases across multiple servers
