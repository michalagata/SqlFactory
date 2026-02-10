# SQLFactory JsonQuery - User Guide

## Table of Contents
- [Overview](#overview)
- [JSON Query Specification](#json-query-specification)
- [Security Model](#security-model)
- [QueryFromJson Methods](#queryfromjson-methods)
- [Operators Reference](#operators-reference)
- [Configuration Options](#configuration-options)
- [Real-World Examples](#real-world-examples)
- [Best Practices](#best-practices)

## Overview

JsonQuery enables **dynamic SQL generation from JSON query specifications**. It translates JSON objects into safe, parameterized SQL queries with built-in security through whitelisting and validation. Perfect for building flexible APIs, report builders, and search interfaces without exposing your database to SQL injection.

### When to Use JsonQuery

- **Dynamic REST APIs**: Allow clients to specify filters, sorting, pagination
- **Report Builders**: User-configurable reports without hardcoding queries
- **Search Interfaces**: Complex multi-field searches with varying criteria
- **Admin Dashboards**: Flexible data exploration tools
- **Third-Party Integrations**: Expose controlled query capabilities

### When NOT to Use JsonQuery

- **Static Queries**: Hardcoded queries are more efficient and maintainable
- **Complex Joins**: Multi-table queries with JOIN logic
- **Performance-Critical**: Hand-tuned queries outperform dynamic generation
- **Unrestricted Access**: If you can't define safe table/column whitelists

## JSON Query Specification

### Complete Format

```json
{
  "table": "Products",
  "select": ["Id", "Name", "Price"],
  "where": {
    "conditions": [
      {
        "field": "Price",
        "operator": ">",
        "value": 10
      },
      {
        "field": "Category",
        "operator": "=",
        "value": "Electronics"
      }
    ],
    "logic": "AND"
  },
  "orderBy": [
    {
      "field": "Price",
      "direction": "DESC"
    },
    {
      "field": "Name",
      "direction": "ASC"
    }
  ],
  "limit": 50,
  "offset": 0
}
```

### Minimal Format

```json
{
  "table": "Products"
}
```
Generates: `SELECT * FROM Products`

### Supported Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `table` | string | ✅ Yes | Table name (must be whitelisted) |
| `select` | string[] | No | Column names (default: all columns) |
| `where` | object | No | Filter conditions |
| `orderBy` | object[] | No | Sort specifications |
| `limit` | int | No | Max rows to return |
| `offset` | int | No | Rows to skip (pagination) |

### WHERE Clause Structure

```json
{
  "where": {
    "conditions": [
      {
        "field": "ColumnName",
        "operator": "=|!=|<|<=|>|>=|LIKE|IS NULL|IS NOT NULL|IN",
        "value": "value or array for IN"
      }
    ],
    "logic": "AND|OR"
  }
}
```

### ORDER BY Structure

```json
{
  "orderBy": [
    {
      "field": "ColumnName",
      "direction": "ASC|DESC"
    }
  ]
}
```

## Security Model

### Whitelisting (CRITICAL)

**All table and column names MUST be whitelisted** to prevent SQL injection and unauthorized data access.

```csharp
var options = new JsonQueryOptions
{
    AllowedTables = new List<string> { "Products", "Categories", "Orders" },
    AllowedColumns = new List<string> { "Id", "Name", "Price", "Category", "Status" }
};
```

### Operator Validation

Only predefined safe operators are allowed:
- `=`, `!=`, `<`, `<=`, `>`, `>=`
- `LIKE` (for pattern matching)
- `IS NULL`, `IS NOT NULL`
- `IN` (for array values)

Any other operator is rejected with `InvalidOperationException`.

### Rate Limiting

```csharp
var options = new JsonQueryOptions
{
    MaxConditions = 10,     // Maximum WHERE conditions per query
    MaxLimit = 1000         // Maximum LIMIT value
};
```

### SQL Injection Prevention

1. **Parameterized Queries**: All values converted to parameters (`{0}`, `{1}`, etc.)
2. **Identifier Validation**: Table/column names checked against whitelist
3. **Type Safety**: Values validated and strongly typed
4. **No Dynamic SQL**: No string concatenation of user input

### Example: Attack Prevention

```json
{
  "table": "Products; DROP TABLE Users;--",  // ❌ BLOCKED: Invalid identifier
  "where": {
    "conditions": [
      {
        "field": "Name",
        "operator": "= 1 OR 1=1--",           // ❌ BLOCKED: Invalid operator
        "value": "'; DELETE FROM Products--"  // ✅ SAFE: Parameterized value
      }
    ]
  }
}
```

Result:
- Table name: **Rejected** (not in whitelist or contains invalid characters)
- Operator: **Rejected** (not in allowed list)
- Value: **Safe** (passed as parameter, never executed as SQL)

## QueryFromJson Methods

### QueryFromJson<T> (Strongly-Typed)

Returns typed entities mapped to class properties.

```csharp
public IEnumerable<T> QueryFromJson<T>(
    string jsonQuery,
    JsonQueryOptions? options = null
);
```

**Example:**

```csharp
var jsonQuery = @"{
    ""table"": ""Products"",
    ""where"": {
        ""conditions"": [
            { ""field"": ""Price"", ""operator"": "">"", ""value"": 10 }
        ]
    },
    ""orderBy"": [
        { ""field"": ""Price"", ""direction"": ""DESC"" }
    ],
    ""limit"": 10
}";

var options = new JsonQueryOptions
{
    AllowedTables = new List<string> { "Products" },
    AllowedColumns = new List<string> { "Id", "Name", "Price" }
};

IEnumerable<Product> products = database.QueryFromJson<Product>(jsonQuery, options);

foreach (var product in products)
{
    Console.WriteLine($"{product.Name}: ${product.Price}");
}
```

### QueryFromJson (Dynamic)

Returns dictionaries with column names as keys.

```csharp
public IEnumerable<Dictionary<string, object?>> QueryFromJson(
    string jsonQuery,
    JsonQueryOptions? options = null
);
```

**Example:**

```csharp
var jsonQuery = @"{
    ""table"": ""Products"",
    ""select"": [""Name"", ""Price""],
    ""limit"": 5
}";

var results = database.QueryFromJson(jsonQuery, options);

foreach (var row in results)
{
    Console.WriteLine($"{row["Name"]}: ${row["Price"]}");
}
```

### When to Use Each

| Method | Use Case |
|--------|----------|
| `QueryFromJson<T>` | Known schema, strongly-typed code, compile-time safety |
| `QueryFromJson` (dynamic) | Unknown schema, flexible APIs, report viewers, admin tools |

## Operators Reference

### Comparison Operators

```json
// Equal
{ "field": "Status", "operator": "=", "value": "Active" }
// SQL: WHERE Status = {0}

// Not Equal
{ "field": "Status", "operator": "!=", "value": "Deleted" }
// SQL: WHERE Status != {0}

// Less Than
{ "field": "Price", "operator": "<", "value": 100 }
// SQL: WHERE Price < {0}

// Less Than or Equal
{ "field": "Stock", "operator": "<=", "value": 10 }
// SQL: WHERE Stock <= {0}

// Greater Than
{ "field": "Price", "operator": ">", "value": 50 }
// SQL: WHERE Price > {0}

// Greater Than or Equal
{ "field": "Rating", "operator": ">=", "value": 4.0 }
// SQL: WHERE Rating >= {0}
```

### Pattern Matching

```json
// LIKE (partial match)
{ "field": "Name", "operator": "LIKE", "value": "%Widget%" }
// SQL: WHERE Name LIKE {0}

// Example patterns:
// "Widget%"    - Starts with "Widget"
// "%Widget"    - Ends with "Widget"
// "%Widget%"   - Contains "Widget"
// "W_dget"     - Single character wildcard
```

### NULL Checks

```json
// IS NULL
{ "field": "DeletedAt", "operator": "IS NULL", "value": null }
// SQL: WHERE DeletedAt IS NULL

// IS NOT NULL
{ "field": "Email", "operator": "IS NOT NULL", "value": null }
// SQL: WHERE Email IS NOT NULL
```

### IN Operator (Array Values)

```json
// IN (multiple values)
{
  "field": "Category",
  "operator": "IN",
  "value": ["Electronics", "Computers", "Phones"]
}
// SQL: WHERE Category IN ({0}, {1}, {2})
```

### Logic Operators

```json
// AND (all conditions must match)
{
  "where": {
    "conditions": [
      { "field": "Price", "operator": ">", "value": 10 },
      { "field": "Stock", "operator": ">", "value": 0 }
    ],
    "logic": "AND"
  }
}
// SQL: WHERE (Price > {0} AND Stock > {1})

// OR (any condition can match)
{
  "where": {
    "conditions": [
      { "field": "Category", "operator": "=", "value": "Sale" },
      { "field": "Featured", "operator": "=", "value": true }
    ],
    "logic": "OR"
  }
}
// SQL: WHERE (Category = {0} OR Featured = {1})
```

## Configuration Options

### JsonQueryOptions Class

```csharp
public class JsonQueryOptions
{
    // Whitelisted table names
    public List<string> AllowedTables { get; set; } = new();

    // Whitelisted column names
    public List<string> AllowedColumns { get; set; } = new();

    // Maximum number of WHERE conditions (default: 50)
    public int MaxConditions { get; set; } = 50;

    // Maximum LIMIT value (default: 1000)
    public int MaxLimit { get; set; } = 1000;

    // Disable validation (DANGEROUS - use only for trusted sources)
    public bool DisableValidation { get; set; } = false;
}
```

### Recommended Production Settings

```csharp
var options = new JsonQueryOptions
{
    AllowedTables = new List<string>
    {
        "Products",
        "Categories",
        "Orders",
        "OrderItems"
    },
    AllowedColumns = new List<string>
    {
        "Id",
        "Name",
        "Description",
        "Price",
        "Stock",
        "Category",
        "CreatedAt",
        "Status"
    },
    MaxConditions = 10,   // Prevent complex queries
    MaxLimit = 500        // Prevent large result sets
};
```

### Environment-Specific Configuration

```csharp
// Development: More permissive
var devOptions = new JsonQueryOptions
{
    AllowedTables = GetAllTableNames(),
    AllowedColumns = GetAllColumnNames(),
    MaxConditions = 100,
    MaxLimit = 10000
};

// Production: Restricted
var prodOptions = new JsonQueryOptions
{
    AllowedTables = new List<string> { "Products", "Categories" },
    AllowedColumns = new List<string> { "Id", "Name", "Price" },
    MaxConditions = 5,
    MaxLimit = 100
};

var options = Environment.IsDevelopment() ? devOptions : prodOptions;
```

## Real-World Examples

### Example 1: REST API Search Endpoint

```csharp
[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly Database _database;
    private readonly JsonQueryOptions _queryOptions;

    public ProductsController(Database database, IConfiguration config)
    {
        _database = database;
        _queryOptions = new JsonQueryOptions
        {
            AllowedTables = new List<string> { "Products" },
            AllowedColumns = new List<string> { "Id", "Name", "Price", "Category", "Stock" },
            MaxConditions = 10,
            MaxLimit = 100
        };
    }

    [HttpPost("search")]
    public IActionResult Search([FromBody] string jsonQuery)
    {
        try
        {
            var products = _database.QueryFromJson<Product>(jsonQuery, _queryOptions);
            return Ok(products);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
```

**Client Request:**

```http
POST /api/products/search
Content-Type: application/json

{
  "table": "Products",
  "select": ["Name", "Price"],
  "where": {
    "conditions": [
      { "field": "Category", "operator": "=", "value": "Electronics" },
      { "field": "Price", "operator": "<", "value": 500 }
    ],
    "logic": "AND"
  },
  "orderBy": [
    { "field": "Price", "direction": "ASC" }
  ],
  "limit": 20
}
```

### Example 2: Report Builder UI

```csharp
public class ReportGenerator
{
    private readonly Database _database;

    public ReportData GenerateReport(ReportRequest request)
    {
        // Convert UI selections to JSON query
        var jsonQuery = BuildJsonQuery(request);

        var options = new JsonQueryOptions
        {
            AllowedTables = new List<string> { "Sales", "Products", "Customers" },
            AllowedColumns = new List<string>
            {
                "OrderDate",
                "ProductName",
                "Quantity",
                "Revenue",
                "CustomerName",
                "Region"
            }
        };

        // Execute dynamic query
        var results = _database.QueryFromJson(jsonQuery, options);

        return new ReportData
        {
            Title = request.ReportTitle,
            Columns = request.SelectedFields,
            Rows = results,
            GeneratedAt = DateTime.UtcNow
        };
    }

    private string BuildJsonQuery(ReportRequest request)
    {
        var query = new
        {
            table = request.DataSource,
            select = request.SelectedFields,
            where = new
            {
                conditions = request.Filters.Select(f => new
                {
                    field = f.Field,
                    @operator = f.Operator,
                    value = f.Value
                }).ToArray(),
                logic = request.FilterLogic
            },
            orderBy = request.SortFields.Select(s => new
            {
                field = s.Field,
                direction = s.Direction
            }).ToArray(),
            limit = request.MaxRows
        };

        return JsonSerializer.Serialize(query);
    }
}
```

### Example 3: Dynamic Pagination

```csharp
public class PaginationService
{
    public PaginatedResult<Product> GetPage(
        int pageNumber,
        int pageSize,
        string? category = null,
        string? searchTerm = null)
    {
        var conditions = new List<object>();

        if (!string.IsNullOrEmpty(category))
        {
            conditions.Add(new
            {
                field = "Category",
                @operator = "=",
                value = category
            });
        }

        if (!string.IsNullOrEmpty(searchTerm))
        {
            conditions.Add(new
            {
                field = "Name",
                @operator = "LIKE",
                value = $"%{searchTerm}%"
            });
        }

        var query = new
        {
            table = "Products",
            where = conditions.Any() ? new
            {
                conditions = conditions,
                logic = "AND"
            } : null,
            orderBy = new[]
            {
                new { field = "Name", direction = "ASC" }
            },
            limit = pageSize,
            offset = (pageNumber - 1) * pageSize
        };

        var jsonQuery = JsonSerializer.Serialize(query);
        var options = new JsonQueryOptions
        {
            AllowedTables = new List<string> { "Products" },
            AllowedColumns = new List<string> { "Id", "Name", "Category", "Price" }
        };

        var items = _database.QueryFromJson<Product>(jsonQuery, options).ToList();
        var totalCount = GetTotalCount(category, searchTerm);

        return new PaginatedResult<Product>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }
}
```

### Example 4: Advanced Filtering UI

```csharp
public class ProductFilterService
{
    public List<Product> ApplyFilters(ProductFilterCriteria criteria)
    {
        var conditions = new List<object>();

        // Price range
        if (criteria.MinPrice.HasValue)
        {
            conditions.Add(new
            {
                field = "Price",
                @operator = ">=",
                value = criteria.MinPrice.Value
            });
        }

        if (criteria.MaxPrice.HasValue)
        {
            conditions.Add(new
            {
                field = "Price",
                @operator = "<=",
                value = criteria.MaxPrice.Value
            });
        }

        // Categories (IN operator)
        if (criteria.Categories?.Any() == true)
        {
            conditions.Add(new
            {
                field = "Category",
                @operator = "IN",
                value = criteria.Categories
            });
        }

        // In stock only
        if (criteria.InStockOnly)
        {
            conditions.Add(new
            {
                field = "Stock",
                @operator = ">",
                value = 0
            });
        }

        // Active only
        if (criteria.ActiveOnly)
        {
            conditions.Add(new
            {
                field = "Status",
                @operator = "=",
                value = "Active"
            });
        }

        var query = new
        {
            table = "Products",
            where = conditions.Any() ? new
            {
                conditions = conditions,
                logic = "AND"
            } : null,
            orderBy = new[]
            {
                new { field = criteria.SortBy ?? "Name", direction = criteria.SortDirection ?? "ASC" }
            },
            limit = criteria.MaxResults ?? 100
        };

        var jsonQuery = JsonSerializer.Serialize(query);
        var options = GetSafeOptions();

        return _database.QueryFromJson<Product>(jsonQuery, options).ToList();
    }
}
```

### Example 5: Multi-Tenant Query API

```csharp
public class MultiTenantQueryService
{
    public IEnumerable<Dictionary<string, object?>> ExecuteQuery(
        int tenantId,
        string jsonQuery)
    {
        // Inject tenant filter automatically
        var queryObj = JsonSerializer.Deserialize<JsonQuerySpec>(jsonQuery);
        
        // Add tenant condition
        queryObj.Where ??= new WhereClause();
        queryObj.Where.Conditions ??= new List<Condition>();
        queryObj.Where.Conditions.Insert(0, new Condition
        {
            Field = "TenantId",
            Operator = "=",
            Value = tenantId
        });

        // Ensure AND logic (tenant filter must apply)
        queryObj.Where.Logic = "AND";

        var modifiedQuery = JsonSerializer.Serialize(queryObj);

        var options = new JsonQueryOptions
        {
            AllowedTables = GetTenantAllowedTables(tenantId),
            AllowedColumns = GetTenantAllowedColumns(tenantId),
            MaxConditions = 20,
            MaxLimit = 500
        };

        return _database.QueryFromJson(modifiedQuery, options);
    }
}
```

### Example 6: Saved Search Feature

```csharp
public class SavedSearchService
{
    public void SaveSearch(int userId, string searchName, string jsonQuery)
    {
        // Validate query first
        ValidateJsonQuery(jsonQuery);

        var savedSearch = new SavedSearch
        {
            UserId = userId,
            Name = searchName,
            QueryJson = jsonQuery,
            CreatedAt = DateTime.UtcNow
        };

        _database.Table<SavedSearch>().Add(savedSearch);
    }

    public IEnumerable<Product> ExecuteSavedSearch(int savedSearchId)
    {
        var savedSearch = _database.Table<SavedSearch>()
            .Where(s => s.Id == savedSearchId)
            .FirstOrDefault();

        if (savedSearch == null)
            throw new NotFoundException("Saved search not found");

        var options = GetUserQueryOptions(savedSearch.UserId);
        return _database.QueryFromJson<Product>(savedSearch.QueryJson, options);
    }
}
```

## Best Practices

### 1. Always Use Whitelists

```csharp
// ❌ BAD: No whitelist (security risk)
var results = database.QueryFromJson(jsonQuery);

// ✅ GOOD: Explicit whitelist
var options = new JsonQueryOptions
{
    AllowedTables = new List<string> { "Products" },
    AllowedColumns = new List<string> { "Id", "Name", "Price" }
};
var results = database.QueryFromJson(jsonQuery, options);
```

### 2. Set Reasonable Limits

```csharp
// ✅ GOOD: Prevent resource exhaustion
var options = new JsonQueryOptions
{
    MaxConditions = 10,   // Prevent overly complex queries
    MaxLimit = 100        // Prevent large result sets
};
```

### 3. Validate JSON Before Processing

```csharp
public IActionResult Search([FromBody] string jsonQuery)
{
    // Validate JSON syntax
    try
    {
        JsonDocument.Parse(jsonQuery);
    }
    catch (JsonException)
    {
        return BadRequest("Invalid JSON format");
    }

    // Execute query
    var results = _database.QueryFromJson(jsonQuery, _options);
    return Ok(results);
}
```

### 4. Use Strongly-Typed Results When Possible

```csharp
// ✅ GOOD: Type safety, IntelliSense, compile-time checks
IEnumerable<Product> products = database.QueryFromJson<Product>(jsonQuery, options);
foreach (var product in products)
{
    Console.WriteLine(product.Name); // Strongly typed
}

// ⚠️ OK: Flexible but less safe
var results = database.QueryFromJson(jsonQuery, options);
foreach (var row in results)
{
    Console.WriteLine(row["Name"]); // Runtime error if key doesn't exist
}
```

### 5. Log Query Execution

```csharp
public IEnumerable<T> QueryWithLogging<T>(string jsonQuery, JsonQueryOptions options)
{
    _logger.LogInformation("Executing JSON query: {Query}", jsonQuery);

    try
    {
        var results = _database.QueryFromJson<T>(jsonQuery, options);
        _logger.LogInformation("Query succeeded");
        return results;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Query failed: {Query}", jsonQuery);
        throw;
    }
}
```

### 6. Cache Query Results

```csharp
public class CachedQueryService
{
    private readonly IMemoryCache _cache;

    public IEnumerable<Product> QueryWithCache(string jsonQuery, TimeSpan cacheDuration)
    {
        var cacheKey = $"jsonquery:{ComputeHash(jsonQuery)}";

        if (!_cache.TryGetValue(cacheKey, out IEnumerable<Product> results))
        {
            results = _database.QueryFromJson<Product>(jsonQuery, _options).ToList();
            _cache.Set(cacheKey, results, cacheDuration);
        }

        return results;
    }
}
```

### 7. Test Edge Cases

```csharp
[TestFixture]
public class JsonQuerySecurityTests
{
    [Test]
    public void Should_RejectUnwhitelistedTable()
    {
        var jsonQuery = @"{ ""table"": ""Users"" }";
        var options = new JsonQueryOptions
        {
            AllowedTables = new List<string> { "Products" }
        };

        Assert.Throws<InvalidOperationException>(() =>
            _database.QueryFromJson(jsonQuery, options).ToList()
        );
    }

    [Test]
    public void Should_RejectSqlInjectionAttempt()
    {
        var jsonQuery = @"{
            ""table"": ""Products; DROP TABLE Users;--""
        }";

        Assert.Throws<InvalidOperationException>(() =>
            _database.QueryFromJson(jsonQuery, _options).ToList()
        );
    }
}
```

## Performance Considerations

- **Parameterization Overhead**: Minimal - parameters are compiled once
- **JSON Parsing**: Use `System.Text.Json` for best performance
- **Large Result Sets**: Use `limit` and `offset` for pagination
- **Complex Conditions**: Keep `MaxConditions` reasonable to prevent slow queries
- **Indexing**: Ensure filtered/sorted columns are indexed in database

## Limitations

1. **Single Table Only**: No JOIN support (by design for security)
2. **Simple WHERE Logic**: One level of AND/OR, no nested conditions
3. **No Aggregations**: No GROUP BY, COUNT, SUM, AVG
4. **No Subqueries**: Simple flat queries only
5. **Static Whitelists**: Tables/columns must be known at runtime

## Related Documentation

- [Database API](Database-API.md)
- [SQL Injection Prevention](Security.md)
- [Pagination Patterns](Pagination.md)
- [REST API Design](API-Design.md)
