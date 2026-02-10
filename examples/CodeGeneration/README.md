# SQLFactory-CodeGen Example

This example demonstrates the usage of the **SQLFactory-CodeGen** CLI tool for generating entity classes from existing databases.

## Installation

```bash
# Install globally as .NET tool
dotnet tool install --global SQLFactory-CodeGen
```

## Usage Examples

### 1. SQLite Database

```bash
# Basic generation
sqlfactory-codegen \
  --connection "Data Source=./Northwind.db" \
  --provider sqlite \
  --output ./Generated

# With namespace
sqlfactory-codegen \
  --connection "Data Source=./Northwind.db" \
  --provider sqlite \
  --output ./Generated \
  --namespace MyApp.Data.Entities
```

### 2. SQL Server Database

```bash
sqlfactory-codegen \
  --connection "Server=localhost;Database=Northwind;Trusted_Connection=true" \
  --provider sqlserver \
  --output ./Generated \
  --namespace MyCompany.Domain
```

### 3. PostgreSQL Database

```bash
sqlfactory-codegen \
  --connection "Host=localhost;Database=northwind;Username=postgres;Password=secret" \
  --provider postgresql \
  --output ./Generated
```

### 4. MySQL Database

```bash
sqlfactory-codegen \
  --connection "Server=localhost;Database=northwind;Uid=root;Pwd=secret" \
  --provider mysql \
  --output ./Generated
```

## Generated Output

The tool generates:

### Entity Classes

```csharp
// Generated/Product.cs
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int CategoryId { get; set; }
    
    // Navigation properties
    public virtual Category? Category { get; set; }
    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}
```

### Database Context

```csharp
// Generated/AppDbContext.cs
public class AppDbContext : Database
{
    public AppDbContext(IDbConnection connection) : base(connection) { }
    
    public SqlTable<Product> Products => Table<Product>();
    public SqlTable<Category> Categories => Table<Category>();
    public SqlTable<Order> Orders => Table<Order>();
}
```

## Command Line Options

```
Options:
  -c, --connection <connection>   Database connection string [required]
  -p, --provider <provider>       Provider: sqlite, sqlserver, postgresql, mysql [required]
  -o, --output <output>          Output directory [required]
  -n, --namespace <namespace>    Namespace for generated classes [default: GeneratedEntities]
  -t, --table <table>            Generate single table only
  -r, --repository               Generate repository pattern classes
  -h, --help                     Show help
```

## Advanced Features

### 1. Generate Repository Pattern

```bash
sqlfactory-codegen \
  --connection "Data Source=./db.sqlite" \
  --provider sqlite \
  --output ./Generated \
  --repository
```

Generates:

```csharp
public interface IProductRepository
{
    Task<Product?> GetByIdAsync(int id);
    Task<IEnumerable<Product>> GetAllAsync();
    Task<Product> InsertAsync(Product entity);
    Task UpdateAsync(Product entity);
    Task DeleteAsync(int id);
}

public class ProductRepository : IProductRepository
{
    private readonly Database _db;
    
    public ProductRepository(Database db) => _db = db;
    
    // Implementation...
}
```

### 2. Single Table Generation

```bash
sqlfactory-codegen \
  --connection "Data Source=./db.sqlite" \
  --provider sqlite \
  --output ./Generated \
  --table Products
```

### 3. Custom Templates

Create `.sqlfactory/templates/entity.template`:

```csharp
using System;
using System.ComponentModel.DataAnnotations;

namespace {{namespace}};

/// <summary>
/// Entity for {{tableName}} table
/// </summary>
public class {{className}}
{
    {{#properties}}
    {{#isPrimaryKey}}
    [Key]
    {{/isPrimaryKey}}
    public {{type}} {{name}} { get; set; }{{#hasDefault}} = {{default}};{{/hasDefault}}
    {{/properties}}
}
```

## Integration with SQLFactory

After generation, use the entities:

```csharp
using Microsoft.Data.Sqlite;
using GeneratedEntities;

var connection = new SqliteConnection("Data Source=./db.sqlite");
connection.Open();

var db = new Database(connection);

// Use generated entities
var products = db.Sql<Product>("SELECT * FROM Products").ToList();
var product = new Product
{
    Name = "New Product",
    Price = 99.99m,
    CategoryId = 1
};
db.Insert(product);
```

## Troubleshooting

### Connection Issues

```bash
# Test connection first
sqlfactory-codegen \
  --connection "..." \
  --provider sqlite \
  --output ./Test \
  --test-connection
```

### Permission Issues

```bash
# Check file permissions
chmod +w ./Generated
```

### Provider Not Found

```bash
# Install provider package
dotnet add package Microsoft.Data.Sqlite
# or
dotnet add package Npgsql
# or
dotnet add package MySqlConnector
```

## Links

- [SQLFactory-CodeGen NuGet](https://www.nuget.org/packages/SQLFactory-CodeGen/)
- [Source Code](https://github.com/anubisworks/sqlfactory)
- [Documentation](../../docs/)
