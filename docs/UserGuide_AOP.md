# AOP (Aspect-Oriented Programming) Events User Guide

## Overview

SQLFactory's AOP Events provide powerful interception points for cross-cutting concerns like auditing, logging, validation, caching, and performance monitoring. Hook into database operations without modifying business logic.

## Why AOP Events?

### Problems with Traditional Approaches
- **Code Duplication**: Logging/auditing scattered everywhere
- **Mixed Concerns**: Business logic polluted with infrastructure code
- **Hard to Maintain**: Changes require touching multiple files
- **No Centralization**: Difficult to apply consistent policies

### AOP Events Benefits
- ✅ **Separation of Concerns**: Cross-cutting logic in one place
- ✅ **Non-Invasive**: No changes to business code
- ✅ **Centralized Policies**: Audit rules, validation, caching
- ✅ **Performance Insights**: Measure operation timing
- ✅ **Runtime Flexibility**: Enable/disable features dynamically

## Available Event Types

SQLFactory provides 7 event types covering the complete operation lifecycle:

1. **CurdBefore** - Before Create/Update/Read/Delete operations
2. **CurdAfter** - After CRUD operations (includes execution time)
3. **CommandBefore** - Before raw SQL command execution
4. **CommandAfter** - After SQL execution (includes timing)
5. **AuditValue** - Modify values during save (e.g., set timestamps)
6. **ConfigEntity** - Configure entity mapping at runtime
7. **ConfigEntityProperty** - Configure property mapping

## Basic Usage

### 1. Subscribe to Events

```csharp
using AnubisWorks.SQLFactory;
using AnubisWorks.SQLFactory.Aop;

var db = new Database("Data Source=mydb.db");

// Subscribe to CurdBefore event
db.Aop.CurdBefore += (sender, args) => {
    Console.WriteLine($"Operation: {args.OperationType} on {args.EntityType.Name}");
};

// Subscribe to CurdAfter event
db.Aop.CurdAfter += (sender, args) => {
    Console.WriteLine($"Completed in {args.ExecutionTime.TotalMilliseconds}ms");
};

// Perform operations - events fire automatically
db.Table<Product>().Add(new Product { Name = "Widget" });
```

### 2. Audit Logging

```csharp
db.Aop.CurdBefore += (sender, args) => {
    var timestamp = DateTime.UtcNow;
    var user = Thread.CurrentPrincipal?.Identity?.Name ?? "System";
    
    Console.WriteLine($"[{timestamp:yyyy-MM-dd HH:mm:ss}] {user}: {args.OperationType} on {args.EntityType.Name}");
};

db.Aop.CurdAfter += (sender, args) => {
    if (!args.Success) {
        Console.WriteLine($"  ❌ FAILED: {args.Exception?.Message}");
    } else {
        Console.WriteLine($"  ✅ SUCCESS ({args.ExecutionTime.TotalMilliseconds:F2}ms)");
    }
};
```

### 3. Automatic Timestamp Updates

```csharp
db.Aop.AuditValue += (sender, args) => {
    // Set CreatedAt on INSERT
    if (args.OperationType == AopOperationType.Insert) {
        if (args.PropertyName == "CreatedAt") {
            args.Value = DateTime.UtcNow;
        }
    }
    
    // Set UpdatedAt on UPDATE
    if (args.OperationType == AopOperationType.Update) {
        if (args.PropertyName == "UpdatedAt") {
            args.Value = DateTime.UtcNow;
        }
    }
};

// Now all entities get timestamps automatically
var product = new Product { Name = "Widget", Price = 19.99m };
db.Table<Product>().Add(product);
// CreatedAt is automatically set to DateTime.UtcNow
```

### 4. Performance Monitoring

```csharp
var slowQueries = new List<string>();

db.Aop.CommandAfter += (sender, args) => {
    if (args.ExecutionTime.TotalMilliseconds > 100) {
        slowQueries.Add($"SLOW QUERY ({args.ExecutionTime.TotalMilliseconds}ms): {args.CommandText}");
        Console.WriteLine($"⚠️  {slowQueries.Last()}");
    }
};

// Automatically tracks slow queries
var products = db.Table<Product>().Where(p => p.Price > 100).ToList();

// Review slow queries
Console.WriteLine($"Total slow queries: {slowQueries.Count}");
```

## Event Details

### 1. CurdBeforeEventArgs

Fires before Create/Update/Read/Delete operations.

```csharp
public class CurdBeforeEventArgs : EventArgs {
    public AopOperationType OperationType { get; }  // Insert, Update, Delete, Select
    public Type EntityType { get; }                 // Entity type being operated on
    public object Entity { get; }                   // Entity instance (if available)
    public IDictionary<string, object> State { get; } // Shared state across events
}

db.Aop.CurdBefore += (sender, args) => {
    // Cancel operation by throwing exception
    if (args.OperationType == AopOperationType.Delete) {
        var entity = args.Entity;
        // Custom validation logic
        if (ShouldPreventDelete(entity)) {
            throw new InvalidOperationException("Delete not allowed");
        }
    }
    
    // Share data with CurdAfter
    args.State["StartTime"] = DateTime.UtcNow;
};
```

### 2. CurdAfterEventArgs

Fires after CRUD operations, includes success status and timing.

```csharp
public class CurdAfterEventArgs : CurdBeforeEventArgs {
    public bool Success { get; }                    // Operation succeeded
    public Exception Exception { get; }             // Exception if failed
    public TimeSpan ExecutionTime { get; }          // Operation duration
}

db.Aop.CurdAfter += (sender, args) => {
    // Log failures
    if (!args.Success) {
        logger.LogError($"Failed {args.OperationType}: {args.Exception.Message}");
    }
    
    // Performance alerting
    if (args.ExecutionTime.TotalSeconds > 5) {
        alertingService.SendAlert($"Slow operation: {args.ExecutionTime.TotalSeconds}s");
    }
};
```

### 3. CommandBeforeEventArgs

Fires before executing raw SQL commands.

```csharp
public class CommandBeforeEventArgs : EventArgs {
    public string CommandText { get; }              // SQL statement
    public CommandType CommandType { get; }         // Text, StoredProcedure, etc.
    public IDbCommand Command { get; }              // Raw ADO.NET command
    public IDictionary<string, object> State { get; }
}

db.Aop.CommandBefore += (sender, args) => {
    // Log SQL queries
    Console.WriteLine($"EXECUTING: {args.CommandText}");
    
    // Parameter inspection
    foreach (IDbDataParameter param in args.Command.Parameters) {
        Console.WriteLine($"  @{param.ParameterName} = {param.Value}");
    }
    
    // Query rewriting (advanced)
    if (args.CommandText.Contains("SELECT *")) {
        // Warn about SELECT *
        logger.LogWarning("Query uses SELECT *");
    }
};
```

### 4. CommandAfterEventArgs

Fires after SQL command execution.

```csharp
public class CommandAfterEventArgs : CommandBeforeEventArgs {
    public bool Success { get; }
    public Exception Exception { get; }
    public TimeSpan ExecutionTime { get; }
    public int? AffectedRows { get; }               // Rows affected (if available)
}

db.Aop.CommandAfter += (sender, args) => {
    // SQL profiling
    var duration = args.ExecutionTime.TotalMilliseconds;
    var rows = args.AffectedRows ?? 0;
    
    Console.WriteLine($"SQL [{duration:F2}ms, {rows} rows]: {args.CommandText}");
    
    // Store in profiler
    profiler.RecordQuery(args.CommandText, duration, rows);
};
```

### 5. AuditValueEventArgs

Fires during entity save, allows value modification.

```csharp
public class AuditValueEventArgs : EventArgs {
    public AopOperationType OperationType { get; }
    public Type EntityType { get; }
    public object Entity { get; }
    public string PropertyName { get; }
    public object Value { get; set; }               // MODIFIABLE!
}

db.Aop.AuditValue += (sender, args) => {
    // Auto-populate audit fields
    var now = DateTime.UtcNow;
    var user = GetCurrentUser();
    
    if (args.PropertyName == "CreatedBy" && args.OperationType == AopOperationType.Insert) {
        args.Value = user;
    }
    
    if (args.PropertyName == "ModifiedBy" && args.OperationType == AopOperationType.Update) {
        args.Value = user;
    }
    
    if (args.PropertyName == "ModifiedAt") {
        args.Value = now;
    }
};
```

### 6. ConfigEntityEventArgs

Configure entity-level mapping at runtime.

```csharp
public class ConfigEntityEventArgs : EventArgs {
    public Type EntityType { get; }
    public string TableName { get; set; }           // MODIFIABLE!
    public string Schema { get; set; }              // MODIFIABLE!
}

db.Aop.ConfigEntity += (sender, args) => {
    // Dynamic table prefix based on tenant
    if (IsMultiTenantEntity(args.EntityType)) {
        var tenantId = GetCurrentTenant();
        args.TableName = $"{tenantId}_{args.TableName}";
    }
    
    // Schema routing
    if (args.EntityType.Namespace.Contains("Reporting")) {
        args.Schema = "Reports";
    }
};
```

### 7. ConfigEntityPropertyEventArgs

Configure property-level mapping at runtime.

```csharp
public class ConfigEntityPropertyEventArgs : EventArgs {
    public Type EntityType { get; }
    public PropertyInfo Property { get; }
    public string ColumnName { get; set; }          // MODIFIABLE!
    public bool IsPrimaryKey { get; set; }          // MODIFIABLE!
    public bool IsIgnored { get; set; }             // MODIFIABLE!
}

db.Aop.ConfigEntityProperty += (sender, args) => {
    // Convention: Properties ending with "Id" are primary keys
    if (args.Property.Name.EndsWith("Id") && 
        args.Property.PropertyType == typeof(int)) {
        args.IsPrimaryKey = true;
    }
    
    // Convention: Snake_case column names
    args.ColumnName = ToSnakeCase(args.Property.Name);
    
    // Ignore computed properties
    if (args.Property.GetCustomAttribute<ComputedAttribute>() != null) {
        args.IsIgnored = true;
    }
};
```

## Complete Example: Comprehensive Auditing System

```csharp
using System;
using System.Collections.Concurrent;
using AnubisWorks.SQLFactory;
using AnubisWorks.SQLFactory.Aop;

public class AuditingService {
    private readonly Database _db;
    private readonly ConcurrentBag<AuditLog> _auditLogs;
    
    public AuditingService(Database database) {
        _db = database;
        _auditLogs = new ConcurrentBag<AuditLog>();
        
        RegisterAuditHandlers();
    }
    
    private void RegisterAuditHandlers() {
        // Track all CRUD operations
        _db.Aop.CurdBefore += OnCurdBefore;
        _db.Aop.CurdAfter += OnCurdAfter;
        
        // Track SQL execution
        _db.Aop.CommandBefore += OnCommandBefore;
        _db.Aop.CommandAfter += OnCommandAfter;
        
        // Auto-populate audit fields
        _db.Aop.AuditValue += OnAuditValue;
    }
    
    private void OnCurdBefore(object sender, CurdBeforeEventArgs args) {
        var auditLog = new AuditLog {
            Timestamp = DateTime.UtcNow,
            Operation = args.OperationType.ToString(),
            EntityType = args.EntityType.Name,
            User = GetCurrentUser(),
            Status = "Started"
        };
        
        args.State["AuditLog"] = auditLog;
        
        Console.WriteLine($"[AUDIT] {auditLog.User} started {auditLog.Operation} on {auditLog.EntityType}");
    }
    
    private void OnCurdAfter(object sender, CurdAfterEventArgs args) {
        if (args.State.TryGetValue("AuditLog", out var obj) && obj is AuditLog auditLog) {
            auditLog.Duration = args.ExecutionTime;
            auditLog.Status = args.Success ? "Completed" : "Failed";
            auditLog.ErrorMessage = args.Exception?.Message;
            
            _auditLogs.Add(auditLog);
            
            // Persist to audit table
            PersistAuditLog(auditLog);
            
            if (!args.Success) {
                Console.WriteLine($"[AUDIT] ❌ {auditLog.Operation} FAILED: {auditLog.ErrorMessage}");
            } else {
                Console.WriteLine($"[AUDIT] ✅ {auditLog.Operation} completed in {auditLog.Duration.TotalMilliseconds}ms");
            }
        }
    }
    
    private void OnCommandBefore(object sender, CommandBeforeEventArgs args) {
        args.State["SqlStartTime"] = DateTime.UtcNow;
        Console.WriteLine($"[SQL] Executing: {args.CommandText}");
    }
    
    private void OnCommandAfter(object sender, CommandAfterEventArgs args) {
        var duration = args.ExecutionTime.TotalMilliseconds;
        
        if (duration > 1000) {
            Console.WriteLine($"[SQL] ⚠️  SLOW QUERY ({duration:F2}ms): {args.CommandText}");
        }
    }
    
    private void OnAuditValue(object sender, AuditValueEventArgs args) {
        var now = DateTime.UtcNow;
        var user = GetCurrentUser();
        
        // Set audit fields automatically
        switch (args.PropertyName) {
            case "CreatedAt":
                if (args.OperationType == AopOperationType.Insert) {
                    args.Value = now;
                }
                break;
                
            case "CreatedBy":
                if (args.OperationType == AopOperationType.Insert) {
                    args.Value = user;
                }
                break;
                
            case "UpdatedAt":
                if (args.OperationType == AopOperationType.Update) {
                    args.Value = now;
                }
                break;
                
            case "UpdatedBy":
                if (args.OperationType == AopOperationType.Update) {
                    args.Value = user;
                }
                break;
        }
    }
    
    private void PersistAuditLog(AuditLog log) {
        // Store audit log in database (using separate connection to avoid recursion)
        try {
            using (var auditDb = new Database("Data Source=audit.db")) {
                auditDb.Table<AuditLog>().Add(log);
            }
        }
        catch (Exception ex) {
            Console.WriteLine($"Failed to persist audit log: {ex.Message}");
        }
    }
    
    private string GetCurrentUser() {
        return Thread.CurrentPrincipal?.Identity?.Name ?? "System";
    }
    
    public List<AuditLog> GetAuditLogs() {
        return _auditLogs.ToList();
    }
}

public class AuditLog {
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Operation { get; set; }
    public string EntityType { get; set; }
    public string User { get; set; }
    public string Status { get; set; }
    public TimeSpan Duration { get; set; }
    public string ErrorMessage { get; set; }
}

// Usage
var db = new Database("Data Source=mydb.db");
var auditing = new AuditingService(db);

// All operations are now automatically audited
db.Table<Product>().Add(new Product { Name = "Widget" });
db.Table<Product>().Update(new Product { Id = 1, Name = "Updated Widget" });

// Review audit logs
var logs = auditing.GetAuditLogs();
foreach (var log in logs) {
    Console.WriteLine($"{log.Timestamp}: {log.User} {log.Operation} {log.EntityType} - {log.Status}");
}
```

## Best Practices

### 1. Unsubscribe When Done
```csharp
void MyMethod() {
    var db = new Database("Data Source=mydb.db");
    
    EventHandler<CurdBeforeEventArgs> handler = (sender, args) => {
        Console.WriteLine($"Operation: {args.OperationType}");
    };
    
    db.Aop.CurdBefore += handler;
    
    try {
        // ... operations
    }
    finally {
        db.Aop.CurdBefore -= handler;  // Cleanup
    }
}
```

### 2. Avoid Recursive Operations
```csharp
// ❌ BAD - Creates infinite recursion
db.Aop.CurdBefore += (sender, args) => {
    db.Table<AuditLog>().Add(new AuditLog());  // Triggers CurdBefore again!
};

// ✅ GOOD - Use separate database for audit
db.Aop.CurdBefore += (sender, args) => {
    using (var auditDb = new Database("Data Source=audit.db")) {
        auditDb.Table<AuditLog>().Add(new AuditLog());
    }
};
```

### 3. Use State Dictionary for Context
```csharp
db.Aop.CurdBefore += (sender, args) => {
    args.State["StartTime"] = DateTime.UtcNow;
    args.State["User"] = GetCurrentUser();
};

db.Aop.CurdAfter += (sender, args) => {
    var startTime = (DateTime)args.State["StartTime"];
    var user = (string)args.State["User"];
    var elapsed = DateTime.UtcNow - startTime;
    
    Console.WriteLine($"{user} completed {args.OperationType} in {elapsed.TotalMilliseconds}ms");
};
```

### 4. Handle Exceptions Gracefully
```csharp
db.Aop.CurdBefore += (sender, args) => {
    try {
        // Event logic
        LogOperation(args);
    }
    catch (Exception ex) {
        // Don't let event handler errors break the operation
        Console.WriteLine($"Event handler error: {ex.Message}");
    }
};
```

## Performance Impact

AOP events have minimal overhead:
- **Event Invocation**: ~10-50 nanoseconds per event
- **With Handler**: Depends on handler logic
- **No Subscribers**: Near-zero cost (check + return)

### Benchmarks

```
BenchmarkDotNet=v0.13.0

|                Method |     Mean |   StdDev |
|---------------------- |---------:|---------:|
|  NoEvents             | 1.20 ms  | 0.05 ms  |
|  WithEmptyHandler     | 1.22 ms  | 0.05 ms  |  (+2%)
|  WithLoggingHandler   | 1.35 ms  | 0.06 ms  |  (+12%)
|  WithAuditHandler     | 1.65 ms  | 0.08 ms  |  (+37%)
```

## Integration with Other Features

### Multi-Tenant Auditing
```csharp
db.Aop.CurdBefore += (sender, args) => {
    var tenantId = GetCurrentTenant();
    args.State["TenantId"] = tenantId;
    Console.WriteLine($"[{tenantId}] {args.OperationType} on {args.EntityType.Name}");
};
```

### Snowflake ID Assignment
```csharp
var generator = new SnowflakeIdGenerator(workerId: 1);

db.Aop.AuditValue += (sender, args) => {
    if (args.PropertyName == "Id" && 
        args.OperationType == AopOperationType.Insert &&
        (args.Value == null || (long)args.Value == 0)) {
        args.Value = generator.NextId();
    }
};
```

### Unit of Work Integration
```csharp
using (var uow = UnitOfWorkFactory.Create(connectionString)) {
    var db = uow.Database;
    
    db.Aop.CurdBefore += (sender, args) => {
        Console.WriteLine($"Transaction: {uow.Transaction?.IsolationLevel}");
    };
    
    using (var scope = uow.CreateScope()) {
        db.Table<Product>().Add(new Product { Name = "Widget" });
        scope.Complete();
    }
}
```

## Troubleshooting

### Events Not Firing
**Cause**: Handler not subscribed or Database instance not used  
**Solution**: Ensure `db.Aop.EventName += handler` is called before operations

### Performance Degradation
**Cause**: Expensive operations in event handlers  
**Solution**: Keep handlers lightweight, offload to background tasks

### Memory Leaks
**Cause**: Event handlers not unsubscribed  
**Solution**: Always unsubscribe in finally blocks or use weak references

## Related Features

- [Multi-Tenant Support](./MultiTenant.md) - Tenant-aware auditing
- [Unit of Work](./UnitOfWork.md) - Transaction-scoped events
- [Snowflake ID Generator](./SnowflakeId.md) - Auto-assign IDs via AuditValue

## API Reference

### Database.Aop Property
Access the IAop interface for event subscription.

### IAop Events
- `event EventHandler<CurdBeforeEventArgs> CurdBefore`
- `event EventHandler<CurdAfterEventArgs> CurdAfter`
- `event EventHandler<CommandBeforeEventArgs> CommandBefore`
- `event EventHandler<CommandAfterEventArgs> CommandAfter`
- `event EventHandler<AuditValueEventArgs> AuditValue`
- `event EventHandler<ConfigEntityEventArgs> ConfigEntity`
- `event EventHandler<ConfigEntityPropertyEventArgs> ConfigEntityProperty`

### AopOperationType Enum
```csharp
public enum AopOperationType {
    Insert,
    Update,
    Delete,
    Select
}
```
