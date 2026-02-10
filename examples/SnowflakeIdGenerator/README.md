# Snowflake ID Generator Example

Demonstrates globally unique, distributed ID generation using the Twitter Snowflake algorithm.

## Features Demonstrated

- ✅ Configure datacenter and worker IDs
- ✅ Generate 64-bit sortable unique IDs
- ✅ Parse IDs to extract metadata
- ✅ High-throughput ID generation (>100k IDs/sec)
- ✅ Clock drift protection
- ✅ Thread-safe operation

## Running the Example

```bash
cd examples/SnowflakeIdGenerator
dotnet run
```

## Expected Output

```
=== Snowflake ID Generator Example ===

Configuration:
  Datacenter ID: 1
  Worker ID: 5
  Epoch: 2024-01-01 00:00:00 UTC

Generated IDs:
  ID 1: 1234567890123456789
  ID 2: 1234567890123456790
  ID 3: 1234567890123456791

Parsed ID 1:
  Timestamp: 2025-01-15 10:30:45.123
  Datacenter ID: 1
  Worker ID: 5
  Sequence: 0

Performance Test:
  Generated 100,000 IDs in 850ms
  Throughput: 117,647 IDs/second
  All IDs unique: ✓
  All IDs monotonically increasing: ✓
```

## Use Cases

### 1. Distributed Systems
Generate unique IDs across multiple servers without coordination:
- 1024 unique workers (32 datacenters × 32 workers)
- No database/Redis needed
- No network calls

### 2. Microservices
Each service instance gets its own worker ID:
```csharp
// Service A (Worker 1)
var generator1 = new SnowflakeIdGenerator(new SnowflakeConfig(datacenterId: 1, workerId: 1));
var orderId = generator1.NextId();

// Service B (Worker 2)
var generator2 = new SnowflakeIdGenerator(new SnowflakeConfig(datacenterId: 1, workerId: 2));
var paymentId = generator2.NextId();

// IDs are globally unique and sortable
```

### 3. Database Sharding
Use Snowflake IDs as shard keys:
```csharp
var orderId = generator.NextId();
var shardId = orderId % 10;  // 10 shards
```

### 4. Time-Series Data
IDs contain timestamp - sort by ID = sort by time:
```sql
SELECT * FROM Orders 
ORDER BY OrderId DESC  -- Chronologically sorted!
LIMIT 10
```

## Architecture Notes

**ID Structure (64 bits):**
```
| 41 bits: Timestamp | 5 bits: Datacenter | 5 bits: Worker | 12 bits: Sequence |
```

**Capacity:**
- 2^41 timestamps = 69 years
- 2^5 datacenters = 32
- 2^5 workers = 32
- 2^12 sequence = 4,096 IDs/millisecond/worker

**Total: 1,024 workers × 4,096 IDs/ms = 4,194,304 IDs/second**

## Best Practices

1. **Assign unique worker IDs** - Use configuration management (e.g., Consul, etcd)
2. **Monitor clock drift** - Log `ClockDriftException` and alert
3. **Use NTP** - Keep server clocks synchronized
4. **Handle exceptions** - Retry or use alternative ID generation
5. **Document worker assignments** - Maintain registry of datacenter/worker IDs
