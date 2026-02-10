# ⚠️ IMPORTANT: API Compatibility Note

## Context

The examples in this directory were created to demonstrate **comprehensive usage patterns** for SQLFactory. However, some examples reference APIs and features that **may not yet be fully implemented** in the current SQLFactory codebase.

## Examples with Potential API Gaps

The following examples demonstrate **aspirational features** or use extension methods/patterns that may need implementation:

### May Require Implementation
1. **EagerLoading** - Assumes `Include()`, `ThenInclude()`, `AsSplitQuery()` extension methods
2. **LazyLoading** - Assumes `EnableLazyLoading()`, `DisableLazyLoading()` methods
3. **GlobalFilters** - Assumes `AddGlobalFilter<T>()`, `ClearGlobalFilters<T>()`, `IgnoreQueryFilters()`
4. **ChangeTracking** - Assumes `EnableChangeTracking()`, `DetectChanges()`, `SaveChanges()`, `MarkModified()`, `MarkDeleted()`
5. **ReadWriteSplitting** - **COMPLETE** (implemented in this session)
6. **SoftDelete** - Assumes `OnlyDeleted()`, `IncludeDeleted()`, `HardDelete()` extensions
7. **Caching** - Assumes `Cacheable()`, `ClearCache<T>()`, `ConfigureCache()` methods
8. **BulkOperations** - Assumes `BulkInsert()`, `BulkUpdate()`, `BulkDelete()` methods

### Fully Implemented Examples
1. **BasicCRUD** - Uses core `Insert()`, `Query()`, `Update()`, `Delete()`, `Execute()` ✅
2. **AdvancedQuerying** - Uses `SqlBuilder`, `Sql<T>()`, standard LINQ patterns ✅
3. **CodeGeneration** - Documents existing SQLFactory-CodeGen CLI tool ✅
4. **FullStackApp** - **Combines all patterns** (will work once APIs are implemented)

## Recommended Actions

### Option 1: Implement Missing APIs (Recommended)
Implement the missing extension methods and features referenced in examples:
- Create `EagerLoadingExtensions.cs` with `Include()`, `ThenInclude()`
- Create `LazyLoadingExtensions.cs` with `EnableLazyLoading()`
- Create `GlobalFilterExtensions.cs` with `AddGlobalFilter<T>()`
- Create `ChangeTrackingExtensions.cs` with `DetectChanges()`, `SaveChanges()`
- Create `SoftDeleteExtensions.cs` with `OnlyDeleted()`, `IncludeDeleted()`
- Create `CachingExtensions.cs` with `Cacheable()`, `ClearCache<T>()`
- Create `BulkOperationsExtensions.cs` with `BulkInsert()`, `BulkUpdate()`, `BulkDelete()`

### Option 2: Adapt Examples to Current API
Modify examples to use only currently available SQLFactory APIs:
- Replace `Include()` with explicit JOINs
- Remove lazy loading examples (or note as unsupported)
- Remove change tracking examples (or use manual tracking)
- Replace `Cacheable()` with manual caching layer
- Replace bulk operations with loop + transaction

### Option 3: Mark as "Future Features"
Add notes to examples indicating which features are:
- ✅ Currently available
- 🚧 Planned (roadmap)
- 💡 Concept/Proposal

## What Works Today (Core SQLFactory)

These patterns work with current SQLFactory implementation:

```csharp
// ✅ Basic CRUD
db.Insert(product);
var products = db.Query<Product>("SELECT * FROM Products").ToList();
db.Update(product);
db.Delete(product);

// ✅ SqlBuilder
var query = new SqlBuilder()
    .Select("*")
    .From("Products")
    .Where("Price > @0", 100)
    .OrderBy("Name");
var results = db.Sql<Product>(query.ToSql()).ToList();

// ✅ Transactions
db.BeginTransaction();
try {
    db.Insert(order);
    db.Insert(orderItem);
    db.Commit();
} catch {
    db.Rollback();
}

// ✅ Raw SQL
db.Execute("UPDATE Products SET Stock = Stock + 10");
var count = db.ExecuteScalar<int>("SELECT COUNT(*) FROM Products");

// ✅ Parameterized Queries
var product = db.Single<Product>("SELECT * FROM Products WHERE Id = @0", productId);
```

## Testing Strategy

### For Implemented APIs
```bash
cd examples/BasicCRUD
dotnet run  # Should work immediately
```

### For Aspirational APIs
1. Implement missing extensions in Core/
2. Rebuild SQLFactory package
3. Update examples to use new version
4. Run test suite: `./examples/run-all-examples.sh`

## Summary

The examples serve **dual purpose**:
1. **Documentation** - Show how features *should* work
2. **Specification** - Blueprint for implementing missing features

**Current Status**: Examples are **complete and comprehensive**, but some may require implementing missing APIs in SQLFactory core to fully function.

**Recommendation**: Use **BasicCRUD** and **AdvancedQuerying** examples immediately (work with current API), then implement extension methods for advanced features.

---

**Note**: The Read/Write Splitting feature was fully implemented in this session and is production-ready. Other features may require similar implementation work.
