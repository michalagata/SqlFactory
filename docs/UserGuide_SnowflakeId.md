# Snowflake ID Generator User Guide

## Overview

SQLFactory's Snowflake ID generator provides distributed, time-ordered, unique 64-bit identifiers. Perfect for multi-server environments, microservices, and multi-tenant systems where traditional auto-increment IDs aren't suitable.

## Why Snowflake IDs?

### Problems with Auto-Increment IDs
- **Single Point of Failure**: Requires centralized sequence generators
- **Not Distributed-Friendly**: Conflicts when multiple servers generate IDs
- **No Time Information**: Can't determine when an entity was created from its ID
- **Predictability**: Sequential IDs expose business information

### Snowflake ID Benefits
- ✅ **Globally Unique**: No collisions across distributed systems
- ✅ **Time-Ordered**: IDs are sortable by creation time
- ✅ **High Performance**: Lock-free generation, millions of IDs/second
- ✅ **Compact**: 64-bit long integers, database-friendly
- ✅ **Decentralized**: Each node generates independently

## ID Structure

Snowflake IDs are composed of:
```
|--Timestamp (41 bits)--|--DatacenterId (5 bits)--|--WorkerId (5 bits)--|--Sequence (12 bits)--|
```

- **Timestamp**: Milliseconds since epoch (2024-01-01), sorted chronologically
- **DatacenterId**: Datacenter identifier (0-31)
- **WorkerId**: Worker/server identifier (0-31)
- **Sequence**: Per-millisecond counter (0-4095)

This structure supports:
- 32 datacenters × 32 workers = **1,024 independent generators**
- **4,096 IDs per millisecond** per worker = ~4 million IDs/second per worker

## Basic Usage

### 1. Simple ID Generation

```csharp
using AnubisWorks.SQLFactory.Snowflake;

// Create a generator (workerId = 1, datacenterId = 0)
var generator = new SnowflakeIdGenerator(workerId: 1);

// Generate IDs
long id1 = generator.NextId();
long id2 = generator.NextId();

Console.WriteLine($"ID 1: {id1}");  // e.g., 1234567890123456
Console.WriteLine($"ID 2: {id2}");  // e.g., 1234567890123457
```

### 2. Multi-Server Configuration

Each server gets a unique workerId:

```csharp
// Server 1
var generator1 = new SnowflakeIdGenerator(workerId: 1, datacenterId: 0);

// Server 2
var generator2 = new SnowflakeIdGenerator(workerId: 2, datacenterId: 0);

// Server 3 (different datacenter)
var generator3 = new SnowflakeIdGenerator(workerId: 1, datacenterId: 1);

// All generate unique, non-colliding IDs
```

### 3. Extract Timestamp from ID

```csharp
var generator = new SnowflakeIdGenerator(1);
long id = generator.NextId();

DateTime timestamp = generator.GetTimestamp(id);
Console.WriteLine($"ID {id} was created at {timestamp}");
```

### 4. Custom Epoch

```csharp
// Use a custom epoch (must be in the past)
var customEpoch = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
var generator = new SnowflakeIdGenerator(workerId: 1, datacenterId: 0, epoch: customEpoch);

long id = generator.NextId();
```

## Integration with Database Entities

### Manual ID Assignment

```csharp
public class Product {
    public long Id { get; set; }  // Snowflake ID
    public string Name { get; set; }
    public decimal Price { get; set; }
}

var generator = new SnowflakeIdGenerator(workerId: 1);
var db = new Database("Data Source=products.db");

var product = new Product {
    Id = generator.NextId(),  // Assign Snowflake ID manually
    Name = "Widget",
    Price = 19.99m
};

db.Table<Product>().Add(product);
```

### Automated ID Generation (Recommended)

Use a base class with automatic ID generation:

```csharp
public abstract class SnowflakeEntity {
    private static readonly SnowflakeIdGenerator _generator = 
        new SnowflakeIdGenerator(workerId: GetWorkerIdFromConfig());
    
    public long Id { get; set; }
    
    protected SnowflakeEntity() {
        if (Id == 0) {  // Generate only if not already set
            Id = _generator.NextId();
        }
    }
    
    private static int GetWorkerIdFromConfig() {
        // Read from environment variable or config file
        return int.Parse(Environment.GetEnvironmentVariable("WORKER_ID") ?? "1");
    }
}

public class Product : SnowflakeEntity {
    public string Name { get; set; }
    public decimal Price { get; set; }
}

// Usage - ID is auto-assigned
var product = new Product {
    Name = "Widget",
    Price = 19.99m
};  // Id is automatically generated

db.Table<Product>().Add(product);
```

## Complete Example: Distributed Order System

```csharp
using System;
using AnubisWorks.SQLFactory;
using AnubisWorks.SQLFactory.Snowflake;

public class DistributedOrderSystem {
    private readonly SnowflakeIdGenerator _idGenerator;
    private readonly Database _database;
    
    public DistributedOrderSystem(int workerId, int datacenterId) {
        _idGenerator = new SnowflakeIdGenerator(workerId, datacenterId);
        _database = new Database("Data Source=orders.db");
    }
    
    public Order CreateOrder(string customerName, decimal total) {
        var order = new Order {
            Id = _idGenerator.NextId(),  // Globally unique ID
            CustomerName = customerName,
            Total = total,
            CreatedAt = DateTime.UtcNow
        };
        
        _database.Table<Order>().Add(order);
        
        Console.WriteLine($"Order {order.Id} created at {order.CreatedAt}");
        return order;
    }
    
    public DateTime GetOrderTimestamp(long orderId) {
        return _idGenerator.GetTimestamp(orderId);
    }
    
    public List<Order> GetRecentOrders(int minutes) {
        // Snowflake IDs are time-ordered, so we can filter by ID range
        var cutoffTime = DateTime.UtcNow.AddMinutes(-minutes);
        var minId = _idGenerator.GetIdFromTimestamp(cutoffTime);
        
        return _database.Table<Order>()
            .Where(o => o.Id >= minId)
            .OrderByDescending(o => o.Id)
            .ToList();
    }
}

public class Order {
    public long Id { get; set; }  // Snowflake ID
    public string CustomerName { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Usage across multiple servers
class Program {
    static void Main() {
        // Each server has a unique workerId
        int workerId = int.Parse(Environment.GetEnvironmentVariable("WORKER_ID") ?? "1");
        
        var orderSystem = new DistributedOrderSystem(workerId, datacenterId: 0);
        
        // Create orders - IDs are guaranteed unique even across servers
        var order1 = orderSystem.CreateOrder("Alice", 99.99m);
        var order2 = orderSystem.CreateOrder("Bob", 149.99m);
        
        // Extract creation timestamp from ID
        var timestamp = orderSystem.GetOrderTimestamp(order1.Id);
        Console.WriteLine($"Order created at: {timestamp}");
    }
}
```

## Best Practices

### 1. Worker ID Assignment
```csharp
// Environment variable (recommended for containers/cloud)
int workerId = int.Parse(Environment.GetEnvironmentVariable("WORKER_ID") ?? "1");

// Configuration file
int workerId = configuration.GetValue<int>("Snowflake:WorkerId");

// Machine name hash (for development)
int workerId = Math.Abs(Environment.MachineName.GetHashCode()) % 32;
```

### 2. Singleton Pattern
```csharp
public class SnowflakeService {
    private static readonly Lazy<SnowflakeIdGenerator> _instance = 
        new Lazy<SnowflakeIdGenerator>(() => {
            int workerId = GetWorkerIdFromConfig();
            return new SnowflakeIdGenerator(workerId);
        });
    
    public static SnowflakeIdGenerator Instance => _instance.Value;
}

// Usage
long id = SnowflakeService.Instance.NextId();
```

### 3. Database Schema
```sql
CREATE TABLE Orders (
    Id BIGINT PRIMARY KEY,  -- Snowflake ID (64-bit)
    CustomerName TEXT NOT NULL,
    Total DECIMAL(10,2) NOT NULL,
    CreatedAt DATETIME NOT NULL
);

-- Index on Id is automatic (PRIMARY KEY)
-- IDs are time-ordered, so range queries are efficient
```

## Performance Characteristics

- **Generation Speed**: ~1-2 million IDs/second per generator
- **Memory Overhead**: < 100 bytes per generator instance
- **Thread Safety**: Fully thread-safe with lock-free fast path
- **Clock Dependency**: Requires system clock (handles clock drift)

### Benchmarks

```
BenchmarkDotNet=v0.13.0, OS=Windows 10
Intel Core i7-9700K CPU 3.60GHz

|           Method |     Mean |   Error |  StdDev |
|----------------- |---------:|--------:|--------:|
|  SingleThreaded  |  520 ns  |  2.1 ns |  1.9 ns |
|  ParallelGeneration | 890 ns | 4.3 ns | 3.8 ns |
```

## Clock Synchronization

⚠️ **Important**: Snowflake IDs depend on system clocks being synchronized.

- Use NTP (Network Time Protocol) to keep clocks in sync
- Monitor for clock drift (>1 second is problematic)
- Generator will wait if clock moves backward

## Troubleshooting

### "Clock moved backwards"
**Cause**: System clock was adjusted backward  
**Solution**: Wait for time to catch up, or use a different epoch

### "Invalid workerId or datacenterId"
**Cause**: IDs must be 0-31  
**Solution**: Ensure configuration provides valid values

### IDs Colliding
**Cause**: Multiple generators using the same workerId + datacenterId  
**Solution**: Ensure each server/process has a unique combination

## Comparison with Other ID Strategies

| Strategy | Pros | Cons |
|----------|------|------|
| **Auto-Increment** | Simple, compact | Centralized, not distributed-friendly |
| **GUID/UUID** | Globally unique | 128-bit (large), not time-ordered, random |
| **Snowflake** | Time-ordered, compact, distributed | Requires clock sync, worker configuration |

## Related Features

- [Multi-Tenant Support](./MultiTenant.md) - Use Snowflake IDs for tenant-specific entities
- [Unit of Work](./UnitOfWork.md) - Transaction management with Snowflake entities
- [AOP Events](./AOP.md) - Audit Snowflake ID generation

## API Reference

### SnowflakeIdGenerator Constructor
```csharp
SnowflakeIdGenerator(int workerId, int datacenterId = 0, DateTime? epoch = null)
```

### Methods
- `long NextId()` - Generate next Snowflake ID
- `DateTime GetTimestamp(long id)` - Extract timestamp from ID
- `long GetIdFromTimestamp(DateTime timestamp)` - Get minimum ID for a given timestamp

### Properties
- `int WorkerId` - Worker identifier (0-31)
- `int DatacenterId` - Datacenter identifier (0-31)
- `DateTime Epoch` - Custom epoch (defaults to 2024-01-01)

### Exceptions
- `ArgumentOutOfRangeException` - Invalid workerId or datacenterId
- `InvalidOperationException` - Clock moved backwards
