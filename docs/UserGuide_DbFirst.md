# SQLFactory DbFirst - User Guide

## Table of Contents
- [Overview](#overview)
- [Key Features](#key-features)
- [Getting Started](#getting-started)
- [GenerateAllEntities](#generateallentities)
- [GenerateEntity](#generateentity)
- [Configuration Options](#configuration-options)
- [Customization](#customization)
- [Integration Examples](#integration-examples)
- [Best Practices](#best-practices)

## Overview

SQLFactory DbFirst automatically generates C# entity classes from existing database tables. It reads table metadata (columns, types, primary keys, foreign keys) and produces strongly-typed POCOs ready for use with SQLFactory's ORM features.

### When to Use DbFirst

- **Existing databases**: Legacy databases with established schemas
- **Database-first development**: Schema designed in SQL first
- **Rapid development**: Generate entities quickly from tables
- **Brownfield projects**: Integrating with existing data sources
- **Schema documentation**: Generate code that documents database structure

### When NOT to Use DbFirst

- **Greenfield projects**: CodeFirst may be more appropriate
- **Frequent schema changes**: Manual class maintenance may be easier
- **Complex mappings**: Custom entity designs may be needed
- **Non-standard schemas**: Highly denormalized or unconventional structures

## Key Features

1. **Automatic Type Mapping**: Converts SQL types to C# types
2. **Primary Key Detection**: Identifies and marks primary keys
3. **Foreign Key Navigation**: Generates navigation properties
4. **Nullable Handling**: Correctly maps nullable columns
5. **Naming Conventions**: Configurable table/column naming
6. **Batch Generation**: Generate all tables at once
7. **Customizable Output**: Control namespace, annotations, virtual properties

## Getting Started

### Basic Setup

```csharp
using AnubisWorks.SQLFactory.DbFirst;

// Create database connection
var database = new Database("Data Source=myapp.db");

// Create generator
var generator = new DbFirstGenerator(database);

// Generate entities for all tables
string code = generator.GenerateAllEntities();

// Save to file
File.WriteAllText("Entities.cs", code);
```

### Generated Output Example

For a table like:

```sql
CREATE TABLE Products (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Price DECIMAL(10,2),
    Category TEXT,
    Stock INTEGER DEFAULT 0
);
```

Generates:

```csharp
using System;
using AnubisWorks.SQLFactory;

namespace MyApp.Data
{
    [Table(Name = "Products")]
    public class Product
    {
        [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)]
        public int Id { get; set; }

        [Column(Name = "Name")]
        public string Name { get; set; }

        [Column(Name = "Price")]
        public decimal? Price { get; set; }

        [Column(Name = "Category")]
        public string? Category { get; set; }

        [Column(Name = "Stock")]
        public int Stock { get; set; }
    }
}
```

## GenerateAllEntities

Generates entity classes for all tables in the database.

```csharp
var generator = new DbFirstGenerator(database);

// Basic usage
string allEntities = generator.GenerateAllEntities();

// With options
var options = new DbFirstOptions
{
    Namespace = "MyCompany.MyApp.Data",
    GenerateNavigationProperties = true,
    UseVirtualProperties = true,
    ExcludeTables = new List<string> { "__migrations", "sysdiagrams" }
};

string allEntities = generator.GenerateAllEntities(options);
```

### Output Structure

```csharp
using System;
using System.Collections.Generic;
using AnubisWorks.SQLFactory;

namespace MyCompany.MyApp.Data
{
    // First entity
    [Table(Name = "Categories")]
    public class Category
    {
        [Column(Name = "Id", IsPrimaryKey = true)]
        public int Id { get; set; }
        
        [Column(Name = "Name")]
        public string Name { get; set; }
        
        // Navigation property
        public virtual ICollection<Product> Products { get; set; }
    }

    // Second entity
    [Table(Name = "Products")]
    public class Product
    {
        // ... properties ...
        
        [Column(Name = "CategoryId")]
        public int? CategoryId { get; set; }
        
        // Foreign key navigation
        public virtual Category? Category { get; set; }
    }

    // ... more entities ...
}
```

## GenerateEntity

Generates an entity class for a specific table.

```csharp
var generator = new DbFirstGenerator(database);

// Generate single entity
string productEntity = generator.GenerateEntity("Products");

// With options
var options = new DbFirstOptions
{
    Namespace = "MyApp.Entities",
    SingularizeClassNames = true
};

string productEntity = generator.GenerateEntity("Products", options);
```

### Use Cases

```csharp
// 1. Generate only needed tables
var coreTables = new[] { "Users", "Products", "Orders" };
foreach (var table in coreTables)
{
    string entity = generator.GenerateEntity(table);
    File.WriteAllText($"{table}.cs", entity);
}

// 2. Separate files per entity
foreach (var table in database.GetTableNames())
{
    var entity = generator.GenerateEntity(table);
    var className = SingularizeName(table);
    File.WriteAllText($"Entities/{className}.cs", entity);
}

// 3. Preview single entity
string preview = generator.GenerateEntity("Products");
Console.WriteLine(preview);
```

## Configuration Options

### DbFirstOptions Class

```csharp
public class DbFirstOptions
{
    // Namespace for generated classes
    public string Namespace { get; set; } = "GeneratedEntities";

    // Singularize table names (Products → Product)
    public bool SingularizeClassNames { get; set; } = true;

    // Generate navigation properties for foreign keys
    public bool GenerateNavigationProperties { get; set; } = true;

    // Use 'virtual' keyword on navigation properties
    public bool UseVirtualProperties { get; set; } = true;

    // Add DataAnnotations attributes
    public bool AddDataAnnotations { get; set; } = true;

    // Tables to exclude from generation
    public List<string> ExcludeTables { get; set; } = new();

    // Table name prefix to remove (e.g., "tbl" → "tblProducts" becomes "Product")
    public string TablePrefix { get; set; } = string.Empty;
}
```

### Namespace Configuration

```csharp
// Default namespace
var generator = new DbFirstGenerator(database);
string code = generator.GenerateAllEntities(); // namespace GeneratedEntities

// Custom namespace
var options = new DbFirstOptions
{
    Namespace = "MyCompany.Data.Entities"
};
string code = generator.GenerateAllEntities(options);
// namespace MyCompany.Data.Entities
```

### Singularization

```csharp
var options = new DbFirstOptions
{
    SingularizeClassNames = true
};

// Table: Products → Class: Product
// Table: Categories → Class: Category
// Table: OrderItems → Class: OrderItem
```

### Navigation Properties

```csharp
// Enabled (default)
var options = new DbFirstOptions
{
    GenerateNavigationProperties = true,
    UseVirtualProperties = true
};

// Generates:
public class Product
{
    public int CategoryId { get; set; }
    public virtual Category? Category { get; set; }
}

public class Category
{
    public virtual ICollection<Product> Products { get; set; }
}

// Disabled
var options = new DbFirstOptions
{
    GenerateNavigationProperties = false
};

// Generates:
public class Product
{
    public int CategoryId { get; set; }
    // No navigation property
}
```

### Table Exclusions

```csharp
var options = new DbFirstOptions
{
    ExcludeTables = new List<string>
    {
        "__EFMigrationsHistory",
        "sysdiagrams",
        "_temp_tables",
        "AspNetRoles",
        "AspNetUsers"
    }
};

string code = generator.GenerateAllEntities(options);
// Excluded tables not generated
```

### Table Prefix Removal

```csharp
var options = new DbFirstOptions
{
    TablePrefix = "tbl"
};

// Table: tblProducts → Class: Product
// Table: tblOrders → Class: Order
// Table: tblCustomers → Class: Customer
```

## Customization

### Type Mapping

Default mappings:

| SQL Type | C# Type |
|----------|---------|
| INTEGER | int |
| BIGINT | long |
| SMALLINT | short |
| TINYINT | byte |
| DECIMAL | decimal |
| NUMERIC | decimal |
| REAL | float |
| FLOAT | double |
| TEXT | string |
| VARCHAR | string |
| CHAR | string |
| BOOLEAN | bool |
| DATE | DateTime |
| DATETIME | DateTime |
| TIMESTAMP | DateTime |
| BLOB | byte[] |

### Custom Type Mappings

```csharp
// Extend generator with custom mappings
public class CustomDbFirstGenerator : DbFirstGenerator
{
    public CustomDbFirstGenerator(Database database) : base(database) { }

    protected override string GetCSharpType(ColumnInfo column)
    {
        // Custom mapping logic
        if (column.DataType == "MONEY")
            return "decimal";
        
        if (column.DataType == "UUID")
            return "Guid";
        
        // Fall back to default
        return base.GetCSharpType(column);
    }
}
```

### Post-Generation Modifications

```csharp
string generatedCode = generator.GenerateAllEntities();

// Add custom using statements
generatedCode = generatedCode.Replace(
    "using AnubisWorks.SQLFactory;",
    @"using AnubisWorks.SQLFactory;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;"
);

// Add base class
generatedCode = generatedCode.Replace(
    "public class Product",
    "public class Product : EntityBase"
);

File.WriteAllText("Entities.cs", generatedCode);
```

### Template-Based Generation

```csharp
public class TemplateDbFirstGenerator
{
    private readonly DbFirstGenerator _generator;

    public string GenerateWithTemplate(string tableName, string template)
    {
        var tableInfo = _generator.GetTableInfo(tableName);
        
        var properties = string.Join("\n", tableInfo.Columns.Select(c =>
            $"    public {GetCSharpType(c)} {c.Name} {{ get; set; }}"
        ));

        return template
            .Replace("{{ClassName}}", SingularizeName(tableName))
            .Replace("{{TableName}}", tableName)
            .Replace("{{Properties}}", properties);
    }
}

string template = @"
namespace MyApp.Entities
{
    [Table(Name = ""{{TableName}}"")]
    public class {{ClassName}}
    {
{{Properties}}
    }
}";

string code = templateGenerator.GenerateWithTemplate("Products", template);
```

## Integration Examples

### Example 1: Generate Entities to Separate Files

```csharp
public class EntityGenerator
{
    private readonly DbFirstGenerator _generator;
    private readonly string _outputPath;

    public EntityGenerator(Database database, string outputPath)
    {
        _generator = new DbFirstGenerator(database);
        _outputPath = outputPath;
    }

    public void GenerateAllToFiles()
    {
        var options = new DbFirstOptions
        {
            Namespace = "MyApp.Data.Entities",
            SingularizeClassNames = true,
            GenerateNavigationProperties = true
        };

        var tables = GetTableNames();
        
        foreach (var table in tables)
        {
            var entity = _generator.GenerateEntity(table, options);
            var className = SingularizeName(table);
            var filePath = Path.Combine(_outputPath, $"{className}.cs");
            
            File.WriteAllText(filePath, entity);
            Console.WriteLine($"Generated: {filePath}");
        }
    }
}

// Usage
var database = new Database(connectionString);
var generator = new EntityGenerator(database, "./Entities");
generator.GenerateAllToFiles();
```

### Example 2: CLI Tool for Entity Generation

```csharp
class Program
{
    static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: DbFirstTool <connectionString> <outputPath>");
            return;
        }

        var connectionString = args[0];
        var outputPath = args[1];

        var database = new Database(connectionString);
        var generator = new DbFirstGenerator(database);

        var options = new DbFirstOptions
        {
            Namespace = "Generated.Entities",
            SingularizeClassNames = true,
            ExcludeTables = new List<string> { "migrations", "sysdiagrams" }
        };

        string code = generator.GenerateAllEntities(options);
        
        Directory.CreateDirectory(outputPath);
        File.WriteAllText(Path.Combine(outputPath, "Entities.cs"), code);

        Console.WriteLine($"Entities generated successfully at {outputPath}");
    }
}
```

### Example 3: Integration with Build Process

```xml
<!-- .csproj -->
<Target Name="GenerateEntities" BeforeTargets="BeforeBuild">
  <Exec Command="dotnet run --project DbFirstTool -- $(ConnectionString) ./Generated" />
</Target>
```

```csharp
// DbFirstTool/Program.cs
public class Program
{
    public static void Main(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionString");
        var outputPath = args[0];

        var database = new Database(connectionString);
        var generator = new DbFirstGenerator(database);
        
        var code = generator.GenerateAllEntities(new DbFirstOptions
        {
            Namespace = "MyApp.Generated"
        });

        File.WriteAllText(Path.Combine(outputPath, "Entities.cs"), code);
    }
}
```

### Example 4: Incremental Generation

```csharp
public class IncrementalGenerator
{
    public void GenerateIfChanged(string tableName, string outputFile)
    {
        var generator = new DbFirstGenerator(_database);
        var newCode = generator.GenerateEntity(tableName);
        
        // Check if file exists and content is different
        if (File.Exists(outputFile))
        {
            var existingCode = File.ReadAllText(outputFile);
            if (existingCode == newCode)
            {
                Console.WriteLine($"{tableName}: No changes");
                return;
            }
        }

        File.WriteAllText(outputFile, newCode);
        Console.WriteLine($"{tableName}: Updated");
    }
}
```

## Best Practices

### 1. Version Control

```csharp
// Generate entities as part of build
public class EntityGenerationTask
{
    public void Execute()
    {
        var generator = new DbFirstGenerator(_database);
        var code = generator.GenerateAllEntities();
        
        // Save with timestamp comment
        var header = $@"//
// Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
// Generator: SQLFactory DbFirst
//

";
        File.WriteAllText("Entities.g.cs", header + code);
    }
}

// .gitignore
Entities.g.cs
```

### 2. Partial Classes for Customization

Generated:
```csharp
// Entities.g.cs (generated, not edited)
namespace MyApp.Data
{
    [Table(Name = "Products")]
    public partial class Product
    {
        [Column(Name = "Id", IsPrimaryKey = true)]
        public int Id { get; set; }
        
        [Column(Name = "Name")]
        public string Name { get; set; }
        
        [Column(Name = "Price")]
        public decimal Price { get; set; }
    }
}
```

Custom:
```csharp
// Product.cs (hand-written customizations)
namespace MyApp.Data
{
    public partial class Product
    {
        // Custom properties
        public string DisplayName => $"{Name} (${Price:F2})";
        
        // Business logic
        public bool IsExpensive() => Price > 100;
        
        // Relationships not in DB
        public List<Review> Reviews { get; set; } = new();
    }
}
```

### 3. Namespace Organization

```csharp
// Organize by module/feature
var options = new DbFirstOptions
{
    Namespace = "MyApp.Sales.Entities" // For sales tables
};

var salesCode = generator.GenerateAllEntities(options);
File.WriteAllText("Sales/Entities.cs", salesCode);

options.Namespace = "MyApp.Inventory.Entities";
var inventoryCode = generator.GenerateAllEntities(options);
File.WriteAllText("Inventory/Entities.cs", inventoryCode);
```

### 4. Documentation Generation

```csharp
public class DocumentedGenerator
{
    public string GenerateWithDocs(string tableName)
    {
        var tableInfo = _generator.GetTableInfo(tableName);
        var docs = $@"
/// <summary>
/// Entity representing {tableName} table.
/// Columns: {tableInfo.Columns.Count}
/// Primary Keys: {string.Join(", ", tableInfo.Columns.Where(c => c.IsPrimaryKey).Select(c => c.Name))}
/// </summary>";

        var entity = _generator.GenerateEntity(tableName);
        return entity.Replace($"public class", $"{docs}\npublic class");
    }
}
```

### 5. Testing Generated Entities

```csharp
[TestFixture]
public class GeneratedEntityTests
{
    [Test]
    public void Product_CanBeCreated()
    {
        var product = new Product
        {
            Name = "Test Product",
            Price = 10.99m
        };

        Assert.That(product.Name, Is.EqualTo("Test Product"));
        Assert.That(product.Price, Is.EqualTo(10.99m));
    }

    [Test]
    public void Product_CanBeSavedToDatabase()
    {
        var database = new Database(connectionString);
        var product = new Product
        {
            Name = "Test",
            Price = 5.00m
        };

        database.Table<Product>().Add(product);
        
        var retrieved = database.Table<Product>()
            .Where(p => p.Id == product.Id)
            .FirstOrDefault();

        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved.Name, Is.EqualTo("Test"));
    }
}
```

## Troubleshooting

### Common Issues

**1. "Table not found"**
- Verify database connection
- Check table name spelling
- Ensure user has SELECT permission

**2. "Unsupported data type"**
- Add custom type mapping
- Update GetCSharpType method
- Report issue with database type

**3. "Foreign key not detected"**
- Ensure foreign keys are defined in database
- Check database supports foreign key metadata
- SQLite: Enable foreign keys with `PRAGMA foreign_keys = ON`

**4. "Namespace conflicts"**
- Use unique namespace per module
- Avoid reserved C# keywords
- Qualify types with full namespace

## Limitations

1. **Complex Types**: Does not handle complex/custom types
2. **Computed Columns**: Generates as regular properties
3. **Triggers**: No code generation for triggers
4. **Stored Procedures**: Not included in entity generation
5. **Database-Specific**: Some features vary by database provider

## Related Documentation

- [CodeFirst Guide](UserGuide_CodeFirst.md)
- [Entity Mapping](Entity-Mapping.md)
- [Database API](Database-API.md)
- [Type Conversion](Type-Conversion.md)
