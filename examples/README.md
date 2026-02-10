# SQLFactory Examples

Comprehensive examples demonstrating **ALL features** of SQLFactory and SQLFactory-CodeGen packages.

> **Package Versions**: SQLFactory 28.2602.33.95 | SQLFactory-CodeGen 28.2602.33.95

## ✅ All Examples Complete (12/12)

All examples are **ready to run** with complete implementations demonstrating production-ready patterns.

## 📂 Project Structure

### 1. ✅ **BasicCRUD** - Getting Started (350 LOC)
**Learn**: INSERT, SELECT, UPDATE, DELETE, Transactions  
**Difficulty**: 🟢 Easy (5 min)  
**Features**:
- Single and batch inserts with ID retrieval
- Query patterns (ToList, Single, FirstOrDefault, Where)
- Entity updates, SQL updates, batch updates
- Single and batch deletes with verification
- Transaction management (Commit/Rollback)

### 2. ✅ **AdvancedQuerying** - Complex Queries (450 LOC)
**Learn**: SqlBuilder, JOINs, GROUP BY, Pagination, Dynamic Queries  
**Difficulty**: 🟢 Easy (10 min)  
**Features**:
- SqlBuilder fluent API (WHERE, OR, IN, LIKE)
- INNER JOIN, LEFT JOIN, multiple joins
- GROUP BY, HAVING, aggregates (COUNT, AVG, SUM)
- LIMIT/OFFSET pagination with total count
- Dynamic WHERE, ORDER BY, SELECT columns
- Subqueries (correlated, IN, WHERE)

### 3. ✅ **EagerLoading** - Include() and ThenInclude() (350 LOC)
**Learn**: Prevent N+1 queries, eager loading strategies  
**Difficulty**: 🟡 Medium (10 min)  
**Features**:
- N+1 problem demonstration and solution
- Single reference loading (many-to-1, 1-to-1)
- Collection loading (1-to-many)
- Multi-level nested includes (ThenInclude)
- Split query patterns to avoid cartesian explosion
- Performance optimization techniques

### 4. ✅ **LazyLoading** - On-Demand Navigation (300 LOC)
**Learn**: Castle.DynamicProxy, navigation properties  
**Difficulty**: 🟡 Medium (10 min)  
**Features**:
- Castle.DynamicProxy configuration
- Reference navigation properties (many-to-1)
- Collection navigation properties (1-to-many)
- Circular reference handling with max depth
- N+1 query detection and warnings
- Hybrid Include() + Lazy Loading scenarios
- Eager vs Lazy comparison

### 5. ✅ **GlobalFilters** - Query Filters (250 LOC)
**Learn**: Soft delete filtering, multi-tenancy  
**Difficulty**: 🟡 Medium (8 min)  
**Features**:
- Soft delete filtering (`!entity.IsDeleted`)
- Multi-tenancy (`entity.TenantId == currentTenant`)
- IgnoreQueryFilters() for admin operations
- Custom business logic filters
- Conditional filter application
- Best practices and documentation

### 6. ✅ **ChangeTracking** - State Management (300 LOC)
**Learn**: DetectChanges(), SaveChanges(), Unit of Work  
**Difficulty**: 🟡 Medium (10 min)  
**Features**:
- Basic change tracking (original values)
- DetectChanges() with property-level tracking
- Entity states (Added, Modified, Deleted, Unchanged)
- Batch SaveChanges() operations
- Relationship fixup (foreign keys)
- Unit of Work pattern implementation

### 7. ✅ **ReadWriteSplitting** - Horizontal Scaling (400 LOC)
**Learn**: Master-replica configuration, load balancing  
**Difficulty**: 🔴 Advanced (12 min)  
**Features**:
- Automatic query routing (SELECT → replica, writes → primary)
- Explicit routing hints (UsePrimary, UseReplica, UseAutoRouting)
- Load balancing strategies (RoundRobin, Random, PrimaryReplica)
- Sticky sessions for read-after-write consistency
- Connection pooling (thread-safe, max 100/pool)
- Production-ready configuration examples

### 8. ✅ **SoftDelete** - Soft Delete Support (250 LOC)
**Learn**: ISoftDeletable interface, data retention  
**Difficulty**: 🟡 Medium (8 min)  
**Features**:
- ISoftDeletable interface implementation
- SoftDelete() with audit trail (DeletedBy, DeletedAt)
- Restore() deleted records
- OnlyDeleted(), IncludeDeleted() query methods
- HardDelete() for permanent removal
- Integration with Global Filters
- GDPR compliance considerations

### 9. ✅ **Caching** - Query Result Cache (200 LOC)
**Learn**: Performance optimization with caching  
**Difficulty**: 🟡 Medium (8 min)  
**Features**:
- Cacheable() extension with duration
- Cache expiration and TTL
- ClearCache<T>() for invalidation
- Performance benchmarks (1000 queries)
- LRU eviction strategy
- Best practices (hit rate, distributed cache)

### 10. ✅ **BulkOperations** - Batch Processing (250 LOC)
**Learn**: High-performance batch operations  
**Difficulty**: 🟡 Medium (10 min)  
**Features**:
- BulkInsert() for large datasets
- BulkUpdate() batch modifications
- BulkDelete() batch deletions
- Performance benchmarks (single vs bulk)
- Throughput metrics (records/sec)
- Best practices (batch size, chunking)

### 11. ✅ **CodeGeneration** - SQLFactory-CodeGen Tool (README)
**Learn**: CLI tool for code generation  
**Difficulty**: 🟢 Easy (5 min)  
**Features**:
- Installation (`dotnet tool install --global SQLFactory-CodeGen`)
- Usage for SQLite, SQL Server, PostgreSQL, MySQL
- Entity class generation with annotations
- DbContext scaffolding
- Repository pattern generation
- Custom templates and configuration

### 12. ✅ **FullStackApp** - Complete E-Commerce Application (600 LOC)
**Learn**: Integration of ALL features in real-world scenario  
**Difficulty**: 🔴 Advanced (15 min)  
**Features**:
- Product catalog with eager loading + caching
- Order creation with transactions + change tracking
- Bulk inventory updates
- Customer management with soft delete
- Admin operations (complex JOINs, aggregates)
- Performance optimizations (pagination, exists checks)
- Reporting and analytics (revenue by category)
- Multi-tenancy with global filters
- Production-ready patterns

## 🚀 Running Examples

Each example is a standalone console application:

```bash
cd examples/BasicCRUD
dotnet run
```

## 📚 Prerequisites

- .NET 8.0 or later
- SQLFactory NuGet package
- SQLite (included in examples)

## 📖 Learning Path

1. Start with **BasicCRUD** to understand fundamentals
2. Explore **AdvancedQuerying** for complex scenarios
3. Learn **EagerLoading** and **LazyLoading** for relationships
4. Study **GlobalFilters** and **ChangeTracking** for advanced patterns
5. Review **ReadWriteSplitting** for scalability
6. Examine **FullStackApp** for complete integration

## 🔗 Links

- [SQLFactory NuGet](https://www.nuget.org/packages/SQLFactory/)
- [SQLFactory-CodeGen NuGet](https://www.nuget.org/packages/SQLFactory-CodeGen/)
- [Main Documentation](../README.md)
- [API Reference](../docs/)
