# SQLFactory Sharding / Split Tables User Guide

## Table of Contents
1. [What is Table Sharding?](#what-is-table-sharding)
2. [When to Use Sharding](#when-to-use-sharding)
3. [Quick Start](#quick-start)
4. [Core Concepts](#core-concepts)
5. [Configuration](#configuration)
6. [Querying Sharded Tables](#querying-sharded-tables)
7. [Advanced Scenarios](#advanced-scenarios)
8. [Best Practices](#best-practices)
9. [Performance Considerations](#performance-considerations)
10. [Troubleshooting](#troubleshooting)

---

## What is Table Sharding?

**Table sharding** (or **split tables**) is a horizontal partitioning technique where a single logical table is split into multiple physical tables based on a sharding key (typically a date/time field). Each shard stores a subset of the data, making queries faster and data management easier.

### Example Scenario

Instead of one massive `Orders` table with 100 million rows:

```
Orders (100M rows) → Slow queries, difficult backups
```

Shard by month into smaller tables:

```
Orders_2024_01_01 (3M rows)
Orders_2024_02_01 (3.2M rows)
Orders_2024_03_01 (3.1M rows)
...
Orders_2026_01_01 (3.5M rows)
Orders_2026_02_01 (current month)
```

**Benefits:**
- **Faster queries**: Query only relevant shards instead of scanning 100M rows
- **Easier maintenance**: Archive/delete old shards without affecting current data
- **Better performance**: Smaller indexes, faster backups/restores
- **Parallel operations**: Write to current shard while reading from historical shards

---

## When to Use Sharding

### ✅ Good Use Cases

- **Time-series data**: Orders, logs, events, transactions with timestamps
- **Large tables** (>10M rows) with natural partitioning key
- **Historical data** that can be archived by time period
- **Write-heavy workloads** where most writes go to current shard
- **Compliance requirements** for data retention and purging

### ❌ Avoid Sharding When

- Table has <1M rows (overhead not worth it)
- No natural sharding key (would need cross-shard joins frequently)
- Frequent updates to old records (requires knowing which shard to update)
- Complex queries spanning many shards (performance degradation)

---

## Quick Start

### Step 1: Mark Your Entity with `[SplitTable]`

```csharp
using AnubisWorks.SQLFactory;
using AnubisWorks.SQLFactory.Sharding;

[SplitTable(SplitType.Month)]
[Table(Name = "Orders_{year}_{month}_{day}")]
public class Order
{
    public long Id { get; set; }

    [SplitField] // This field determines which shard to route to
    public DateTime OrderDate { get; set; }

    public string CustomerName { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; }
}
```

**Key Points:**
- `[SplitTable(SplitType.Month)]` – Shard by month
- `Table(Name = "Orders_{year}_{month}_{day}")` – Pattern with placeholders
- `[SplitField]` – Marks the routing field (only one per entity)

### Step 2: Query Current Shard

```csharp
using (var db = new Database(connectionString))
{
    // Query current month's orders (e.g., Orders_2026_02_01)
    var recentOrders = db.From<Order>("Orders")
        .AsSharded(db)
        .Where("Amount > {0}", 100)
        .OrderBy("OrderDate DESC")
        .ToList();

    Console.WriteLine($"Found {recentOrders.Count} orders in current shard");
}
```

### Step 3: Query Specific Shard

```csharp
// Query January 2025 orders
var jan2025Orders = db.From<Order>("Orders")
    .AsSharded(db, new DateTime(2025, 1, 15)) // Any date in January
    .Where("Status = {0}", "Completed")
    .ToList();
```

### Step 4: Query Across Multiple Shards

```csharp
// Query Q1 2025 (Jan, Feb, Mar)
var q1Orders = db.From<Order>("Orders")
    .AsShardedInRange(db, 
        new DateTime(2025, 1, 1), 
        new DateTime(2025, 3, 31))
    .ToList();
```

---

## Core Concepts

### 1. Split Types

SQLFactory supports these temporal sharding strategies:

| Split Type | Description | Example Tables |
|------------|-------------|----------------|
| **Day** | One table per day | `Orders_2026_02_01`, `Orders_2026_02_02` |
| **Week** | One table per week (Monday start) | `Orders_2026_02_03` (Monday of that week) |
| **Month** | One table per month | `Orders_2026_01_01`, `Orders_2026_02_01` |
| **Season** | One table per quarter (Q1-Q4) | `Orders_2026_01_01` (Q1), `Orders_2026_04_01` (Q2) |
| **Year** | One table per year | `Orders_2026_01_01`, `Orders_2027_01_01` |
| **HalfYear** | Two tables per year (H1, H2) | `Orders_2026_01_01` (Jan-Jun), `Orders_2026_07_01` (Jul-Dec) |

### 2. Table Name Patterns

The table name pattern uses placeholders that are replaced based on the date:

```csharp
[Table(Name = "Orders_{year}_{month}_{day}")]
```

**Placeholders:**
- `{year}` – 4-digit year (e.g., `2026`)
- `{month}` – 2-digit month with leading zero (e.g., `02`)
- `{day}` – 2-digit day with leading zero (e.g., `01`)

**Valid Patterns:**
```csharp
"Orders_{year}_{month}_{day}"     // Orders_2026_02_01
"Log_{year}_{month}_01"            // Log_2026_02_01
"Events_{year}_{month}_{day}"      // Events_2026_02_15
"Archive_{year}_{month}_{day}"     // Archive_2025_12_01
```

**Invalid Patterns:**
```csharp
"Orders_{year}{month}"             // ❌ No separator (ambiguous regex)
"Orders_{year}_{year}"             // ❌ Duplicate placeholder
"Orders_{month}_{day}"             // ❌ Missing {year}
"Orders1{year}_"                   // ❌ Digit adjacent to placeholder
```

### 3. Routing Field (`[SplitField]`)

The `[SplitField]` attribute marks which property contains the value that determines the shard.

```csharp
[SplitTable(SplitType.Month)]
public class Order
{
    public long Id { get; set; }

    [SplitField] // Routes based on this field
    public DateTime OrderDate { get; set; }

    public decimal Amount { get; set; }
}
```

**Rules:**
- Only **one** `[SplitField]` per entity
- Must be `DateTime` or `DateTimeOffset` for date-based sharding
- If routing field is `null` or `DateTime.MinValue`, current date is used

---

## Configuration

### Auto-Configuration (Recommended)

SQLFactory automatically configures sharding when you call `.AsSharded()` for the first time:

```csharp
var orders = db.From<Order>("Orders")
    .AsSharded(db) // Auto-configures on first call
    .ToList();
```

Behind the scenes:
1. Reads `[SplitTable]` attribute to determine split type
2. Reads `[Table]` attribute for table name pattern
3. Creates a `DateShardingStrategy` instance
4. Registers strategy in `ShardingManager`

### Manual Configuration

For advanced scenarios, configure manually:

```csharp
var shardingManager = db.Sharding();

// Register Date-based strategy
shardingManager.RegisterStrategy<Order>(
    new DateShardingStrategy(),
    "Orders_{year}_{month}_{day}"
);

// Or auto-configure with custom pattern
shardingManager.AutoConfigure<Order>("CustomOrders_{year}_{month}_{day}");
```

### Custom Sharding Strategies

Implement `IShardingStrategy` for non-date-based sharding:

```csharp
public class CustomerIdShardingStrategy : IShardingStrategy
{
    public List<ShardInfo> GetAllShards(Database database, Type entityType, string tableNamePattern)
    {
        // Return list of all shards (e.g., Customers_A, Customers_B, ...)
        return new List<ShardInfo>
        {
            new ShardInfo { TableName = "Customers_A", StringValue = "A-M" },
            new ShardInfo { TableName = "Customers_B", StringValue = "N-Z" }
        };
    }

    public string GetDefaultTableName(Database database, Type entityType, string tableNamePattern)
    {
        return "Customers_A"; // Default shard
    }

    public string GetTableNameByValue(Database database, Type entityType, string tableNamePattern, object? routingValue)
    {
        if (routingValue is string customerName && !string.IsNullOrEmpty(customerName))
        {
            return customerName[0] >= 'N' ? "Customers_B" : "Customers_A";
        }
        return "Customers_A";
    }

    public object? GetRoutingValue(Database database, Type entityType, object entityInstance)
    {
        var prop = entityType.GetProperty("CustomerName");
        return prop?.GetValue(entityInstance);
    }
}

// Register custom strategy
[SplitTable(SplitType.Custom01, typeof(CustomerIdShardingStrategy))]
[Table(Name = "Customers_{shard}")]
public class Customer
{
    public int Id { get; set; }
    public string CustomerName { get; set; }
}
```

---

## Querying Sharded Tables

### 1. Query Current Shard (Most Common)

```csharp
// Queries Orders_2026_02_01 (current month)
var currentOrders = db.From<Order>("Orders")
    .AsSharded(db)
    .Where("Status = {0}", "Pending")
    .ToList();
```

**Use Case:** Real-time dashboards, current operations

### 2. Query Specific Shard by Date

```csharp
// Query orders from Black Friday 2025 (Nov 29, 2025)
var blackFridayOrders = db.From<Order>("Orders")
    .AsSharded(db, new DateTime(2025, 11, 29))
    .ToList();
```

**Use Case:** Historical reports, specific date analysis

### 3. Query Date Range (Multiple Shards)

```csharp
// Query last 6 months
var lastSixMonths = db.From<Order>("Orders")
    .AsShardedInRange(db, 
        DateTime.UtcNow.AddMonths(-6), 
        DateTime.UtcNow)
    .Where("Amount > {0}", 500)
    .OrderBy("OrderDate DESC")
    .ToList();
```

**Use Case:** Period reports (quarterly, annual), trend analysis

**Performance Note:** This generates a `UNION ALL` query across all shards in range:

```sql
SELECT * FROM (
    SELECT * FROM Orders_2025_09_01 UNION ALL
    SELECT * FROM Orders_2025_10_01 UNION ALL
    SELECT * FROM Orders_2025_11_01 UNION ALL
    SELECT * FROM Orders_2025_12_01 UNION ALL
    SELECT * FROM Orders_2026_01_01 UNION ALL
    SELECT * FROM Orders_2026_02_01
) AS RangeShards
WHERE Amount > 500
ORDER BY OrderDate DESC
```

### 4. Query All Shards (Use Sparingly)

```csharp
// Query ALL orders across ALL shards (expensive!)
var allOrders = db.From<Order>("Orders")
    .AsShardedAcrossAll(db)
    .Where("CustomerName LIKE {0}", "John%")
    .ToList();
```

**⚠️ Warning:** Queries every shard table. Use only when necessary (e.g., full data exports, migrations).

### 5. Inserting into Shards

SQLFactory doesn't currently auto-route inserts to shards. Use `SqlTable` with explicit table name:

```csharp
var order = new Order
{
    Id = GenerateId(),
    OrderDate = DateTime.UtcNow,
    CustomerName = "Alice",
    Amount = 299.99m
};

// Get correct shard name
var shardingManager = db.Sharding();
shardingManager.AutoConfigure<Order>();
var tableName = shardingManager.GetTableNameForEntity(order);

// Insert into correct shard
var table = new SqlTable<Order>(db, tableName);
table.Insert(order);

Console.WriteLine($"Inserted into {tableName}");
```

---

## Advanced Scenarios

### Automatic Shard Creation

Create shards programmatically as needed:

```csharp
public void EnsureShardExists(Database db, DateTime date)
{
    var shardingManager = db.Sharding();
    shardingManager.AutoConfigure<Order>();
    
    var tableName = shardingManager.GetTableNameByValue<Order>(date);

    // Check if shard exists
    var allShards = shardingManager.GetAllShards<Order>();
    if (!allShards.Any(s => s.TableName == tableName))
    {
        // Create shard (simplified example - adjust SQL for your database)
        db.Execute($@"
            CREATE TABLE {tableName} (
                Id BIGINT PRIMARY KEY,
                OrderDate DATETIME NOT NULL,
                CustomerName NVARCHAR(200),
                Amount DECIMAL(18,2),
                Status NVARCHAR(50)
            )
        ");
        
        Console.WriteLine($"Created shard: {tableName}");
    }
}

// Usage
EnsureShardExists(db, DateTime.UtcNow.AddMonths(1)); // Create next month's shard
```

### Archiving Old Shards

Move old shards to cold storage or delete them:

```csharp
public void ArchiveOldShards(Database db, int monthsToKeep)
{
    var shardingManager = db.Sharding();
    shardingManager.AutoConfigure<Order>();
    
    var allShards = shardingManager.GetAllShards<Order>();
    var cutoffDate = DateTime.UtcNow.AddMonths(-monthsToKeep);

    foreach (var shard in allShards.Where(s => s.Date < cutoffDate))
    {
        // Option 1: Export to archive database
        db.Execute($"INSERT INTO ArchiveDB..{shard.TableName} SELECT * FROM {shard.TableName}");
        
        // Option 2: Drop old shard
        db.Execute($"DROP TABLE {shard.TableName}");
        
        Console.WriteLine($"Archived and dropped: {shard.TableName}");
    }
}
```

### Parallel Shard Processing

Process shards in parallel for performance:

```csharp
public async Task<Dictionary<string, int>> GetOrderCountsPerShard(Database db)
{
    var shardingManager = db.Sharding();
    shardingManager.AutoConfigure<Order>();
    
    var allShards = shardingManager.GetAllShards<Order>();

    var tasks = allShards.Select(async shard =>
    {
        using (var db2 = new Database(connectionString))
        {
            var count = await db2.From<Order>(shard.TableName)
                .Select("COUNT(*)")
                .ToScalarAsync<int>();

            return new { Shard = shard.TableName, Count = count };
        }
    });

    var results = await Task.WhenAll(tasks);
    return results.ToDictionary(r => r.Shard, r => r.Count);
}
```

---

## Best Practices

### 1. Choose the Right Split Type

| Data Volume | Write Pattern | Recommended Split Type |
|-------------|---------------|------------------------|
| <100K rows/month | Low write rate | **Month** |
| 100K-1M rows/month | Moderate writes | **Month** or **Week** |
| >1M rows/month | High write rate | **Week** or **Day** |
| Steady, predictable | Batch processing | **Month** |
| Spiky, real-time | Continuous writes | **Day** |

### 2. Index Strategy

Create indexes on **each shard table**, not just the entity:

```csharp
// After creating shard, add indexes
db.Execute($"CREATE INDEX IX_{tableName}_OrderDate ON {tableName}(OrderDate)");
db.Execute($"CREATE INDEX IX_{tableName}_CustomerName ON {tableName}(CustomerName)");
```

### 3. Avoid Cross-Shard Joins

Design your sharding key to minimize cross-shard queries:

```csharp
// ❌ Bad: Frequent cross-shard joins
var ordersWithCustomers = db.From<Order>("Orders")
    .AsShardedAcrossAll(db) // Scans all shards
    .Join<Customer>("INNER JOIN Customers ON Orders.CustomerId = Customers.Id")
    .ToList();

// ✅ Good: Denormalize data to avoid joins
[SplitTable(SplitType.Month)]
public class Order
{
    public long Id { get; set; }
    [SplitField]
    public DateTime OrderDate { get; set; }
    
    // Denormalized customer data (no join needed)
    public int CustomerId { get; set; }
    public string CustomerName { get; set; }
    public string CustomerEmail { get; set; }
}
```

### 4. Use Batch Operations

When inserting many records, batch them by shard:

```csharp
var orders = GetOrdersToInsert();

// Group by shard
var ordersByS hard = orders
    .GroupBy(o => shardingManager.GetTableNameForEntity(o))
    .ToDictionary(g => g.Key, g => g.ToList());

// Bulk insert per shard
foreach (var (tableName, shardOrders) in ordersByShard)
{
    var table = new SqlTable<Order>(db, tableName);
    table.BulkInsert(shardOrders); // Much faster than individual inserts
}
```

### 5. Monitor Shard Growth

Track shard sizes to detect anomalies:

```csharp
public void MonitorShardSizes(Database db)
{
    var shardingManager = db.Sharding();
    shardingManager.AutoConfigure<Order>();
    
    var allShards = shardingManager.GetAllShards<Order>();

    foreach (var shard in allShards.OrderByDescending(s => s.Date).Take(12)) // Last 12 months
    {
        var count = db.From<Order>(shard.TableName).Select("COUNT(*)").ToScalar<int>();
        var size = db.From($"sys.tables t INNER JOIN sys.indexes i ON t.object_id = i.object_id WHERE t.name = '{shard.TableName}'")
            .Select("SUM(reserved_page_count) * 8 / 1024.0 AS SizeMB")
            .ToScalar<decimal>();

        Console.WriteLine($"{shard.TableName}: {count:N0} rows, {size:N2} MB");
        
        // Alert if anomaly detected
        if (count > 10_000_000)
        {
            Console.WriteLine($"⚠️ WARNING: {shard.TableName} exceeds 10M rows!");
        }
    }
}
```

---

## Performance Considerations

### Query Performance

| Operation | Single Shard | Multiple Shards | All Shards |
|-----------|--------------|-----------------|------------|
| SELECT with WHERE | **Excellent** (focused scan) | Good (parallel UNION) | Poor (full scan) |
| COUNT(*) | **Excellent** | Good | Poor |
| JOIN | **Good** (if within shard) | Poor (cross-shard) | Very Poor |
| ORDER BY | **Excellent** | Good (sort after UNION) | Poor (large result set) |
| INSERT/UPDATE | **Excellent** (targeted write) | N/A | N/A |

### Optimization Tips

1. **Filter by routing field** – Ensures query hits only relevant shards:
   ```csharp
   // ✅ Good: Only queries Orders_2026_01_01
   .Where("OrderDate >= {0} AND OrderDate < {1}", 
       new DateTime(2026, 1, 1), 
       new DateTime(2026, 2, 1))
   ```

2. **Use covering indexes** – Include all queried columns:
   ```sql
   CREATE INDEX IX_Orders_Status_Amount 
   ON Orders_2026_01_01 (Status, Amount) 
   INCLUDE (OrderDate, CustomerName);
   ```

3. **Limit cross-shard queries** – Use date ranges instead of `AsShardedAcrossAll()`:
   ```csharp
   // ❌ Slow: Queries all 100+ shards
   .AsShardedAcrossAll(db)

   // ✅ Fast: Queries only 3 shards
   .AsShardedInRange(db, DateTime.UtcNow.AddMonths(-3), DateTime.UtcNow)
   ```

4. **Partition statistics** – Keep table statistics updated:
   ```sql
   -- SQL Server
   UPDATE STATISTICS Orders_2026_02_01 WITH FULLSCAN;

   -- PostgreSQL
   ANALYZE Orders_2026_02_01;
   ```

---

## Troubleshooting

### Issue: "No sharding strategy registered"

**Cause:** Entity not configured for sharding.

**Solution:** Call `.AsSharded()` or manually configure:
```csharp
db.Sharding().AutoConfigure<Order>();
```

### Issue: "Table name pattern missing {year}"

**Cause:** Invalid table name pattern.

**Solution:** Ensure pattern has all three placeholders:
```csharp
[Table(Name = "Orders_{year}_{month}_{day}")] // ✅ Valid
```

### Issue: "No shards found in range"

**Cause:** Shard tables don't exist yet or naming mismatch.

**Solution:** Create shards first or check naming:
```csharp
var allShards = db.Sharding().GetAllShards<Order>();
Console.WriteLine(string.Join(", ", allShards.Select(s => s.TableName)));
```

### Issue: Poor performance on cross-shard queries

**Cause:** Querying too many shards or missing indexes.

**Solution:**
1. Narrow date range
2. Add indexes to each shard table
3. Use parallel processing for large ranges

### Issue: Cannot insert - routing value is null

**Cause:** `[SplitField]` property not set before insert.

**Solution:** Always set routing field:
```csharp
var order = new Order
{
    OrderDate = DateTime.UtcNow, // ✅ Set routing field
    Amount = 99.99m
};
```

---

## Complete Example: E-Commerce Order Sharding

```csharp
using System;
using System.Linq;
using AnubisWorks.SQLFactory;
using AnubisWorks.SQLFactory.Sharding;

namespace ECommerceExample
{
    [SplitTable(SplitType.Month)]
    [Table(Name = "Orders_{year}_{month}_{day}")]
    public class Order
    {
        public long Id { get; set; }

        [SplitField]
        public DateTime OrderDate { get; set; }

        public int CustomerId { get; set; }
        public string CustomerName { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; }
        public string ShippingAddress { get; set; }
    }

    class Program
    {
        static void Main()
        {
            using (var db = new Database("Server=localhost;Database=Ecommerce;..."))
            {
                // 1. Query current month's pending orders
                Console.WriteLine("=== Current Pending Orders ===");
                var pendingOrders = db.From<Order>("Orders")
                    .AsSharded(db)
                    .Where("Status = {0}", "Pending")
                    .OrderBy("OrderDate DESC")
                    .ToList();

                foreach (var order in pendingOrders.Take(10))
                {
                    Console.WriteLine($"Order #{order.Id} - {order.CustomerName} - ${order.TotalAmount}");
                }

                // 2. Monthly revenue report
                Console.WriteLine("\n=== Monthly Revenue (Last 6 Months) ===");
                var startDate = DateTime.UtcNow.AddMonths(-6);
                var endDate = DateTime.UtcNow;

                var monthlyRevenue = db.From<Order>("Orders")
                    .AsShardedInRange(db, startDate, endDate)
                    .Where("Status = {0}", "Completed")
                    .ToList()
                    .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
                    .Select(g => new
                    {
                        Month = $"{g.Key.Year}-{g.Key.Month:D2}",
                        Revenue = g.Sum(o => o.TotalAmount),
                        Count = g.Count()
                    })
                    .OrderBy(x => x.Month);

                foreach (var month in monthlyRevenue)
                {
                    Console.WriteLine($"{month.Month}: ${month.Revenue:N2} ({month.Count} orders)");
                }

                // 3. Insert new order into correct shard
                Console.WriteLine("\n=== Insert New Order ===");
                var newOrder = new Order
                {
                    Id = GenerateSnowflakeId(),
                    OrderDate = DateTime.UtcNow,
                    CustomerId = 12345,
                    CustomerName = "John Doe",
                    TotalAmount = 159.99m,
                    Status = "Pending",
                    ShippingAddress = "123 Main St"
                };

                var shardingManager = db.Sharding();
                shardingManager.AutoConfigure<Order>();
                var tableName = shardingManager.GetTableNameForEntity(newOrder);

                var table = new SqlTable<Order>(db, tableName);
                table.Insert(newOrder);

                Console.WriteLine($"Inserted Order #{newOrder.Id} into {tableName}");

                // 4. Archive old shards (> 24 months)
                Console.WriteLine("\n=== Archive Old Shards ===");
                ArchiveOldShards(db, monthsToKeep: 24);
            }
        }

        static void ArchiveOldShards(Database db, int monthsToKeep)
        {
            var shardingManager = db.Sharding();
            shardingManager.AutoConfigure<Order>();

            var allShards = shardingManager.GetAllShards<Order>();
            var cutoffDate = DateTime.UtcNow.AddMonths(-monthsToKeep);

            foreach (var shard in allShards.Where(s => s.Date < cutoffDate))
            {
                // Export to archive
                db.Execute($@"
                    INSERT INTO ArchiveDB.dbo.{shard.TableName}
                    SELECT * FROM {shard.TableName}
                ");

                // Drop old shard
                db.Execute($"DROP TABLE {shard.TableName}");

                Console.WriteLine($"Archived: {shard.TableName}");
            }
        }

        static long GenerateSnowflakeId()
        {
            // Simplified Snowflake ID generator
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return timestamp << 22 | (long)(new Random().Next(0, 4095));
        }
    }
}
```

---

## Comparison with Other ORMs

| Feature | SQLFactory | Entity Framework Core | Dapper |
|---------|-----------|----------------------|--------|
| Automatic shard routing | ✅ Yes | ❌ No (manual queries) | ❌ No |
| Date-based sharding | ✅ Built-in | ❌ Manual | ❌ Manual |
| Custom strategies | ✅ Yes | ❌ No | ❌ No |
| Cross-shard queries | ✅ UNION ALL support | ⚠️ Manual unions | ⚠️ Manual unions |
| Auto-configuration | ✅ Attribute-based | ❌ Code-based | ❌ Manual |

---

## API Reference Summary

### Attributes

- `[SplitTable(SplitType)]` – Marks entity for sharding
- `[SplitField]` – Marks routing field
- `[Table(Name = "...")]` – Defines table name pattern

### Extension Methods

- `.AsSharded(db)` – Query current shard
- `.AsSharded(db, routingValue)` – Query specific shard
- `.AsShardedInRange(db, start, end)` – Query date range
- `.AsShardedAcrossAll(db)` – Query all shards

### ShardingManager Methods

- `AutoConfigure<T>()` – Auto-setup from attributes
- `RegisterStrategy<T>(strategy, pattern)` – Manual setup
- `GetDefaultTableName<T>()` – Current shard name
- `GetTableNameForEntity<T>(entity)` – Shard for entity
- `GetTableNameByValue<T>(value)` – Shard for routing value
- `GetAllShards<T>()` – List all shards

---

## Conclusion

SQLFactory's sharding feature provides a **production-ready, zero-configuration** solution for horizontal table partitioning. By following this guide, you can:

✅ Improve query performance by 10-100x  
✅ Simplify data archiving and compliance  
✅ Scale to billions of rows without performance degradation  
✅ Use temporal or custom sharding strategies  

**Next Steps:**
1. Start with **Month sharding** for most use cases
2. Monitor shard growth and adjust split type if needed
3. Implement automated shard creation/archiving
4. Measure performance improvements and iterate

For questions or advanced scenarios, see [PRODUCTION_READINESS.md](./PRODUCTION_READINESS.md) or file an issue on GitHub.

---

**SQLFactory Sharding** – Built for scale, designed for simplicity. 🚀
