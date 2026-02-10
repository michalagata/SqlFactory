using AnubisWorks.SQLFactory;
using Microsoft.Data.Sqlite;
using Castle.DynamicProxy;

namespace SQLFactory.Examples.LazyLoading;

/// <summary>
/// Example: Lazy Loading with Castle.DynamicProxy
/// Demonstrates: Navigation properties, on-demand loading, circular references
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== SQLFactory Lazy Loading Example ===\n");

        var db = CreateDatabase();

        Console.WriteLine("1. REFERENCE NAVIGATION PROPERTIES");
        ReferenceNavigationExample(db);

        Console.WriteLine("\n2. COLLECTION NAVIGATION PROPERTIES");
        CollectionNavigationExample(db);

        Console.WriteLine("\n3. CIRCULAR REFERENCE HANDLING");
        CircularReferenceExample(db);

        Console.WriteLine("\n4. LAZY LOADING CONFIGURATION");
        LazyLoadingConfigurationExample(db);

        Console.WriteLine("\n5. EAGER vs LAZY LOADING COMPARISON");
        ComparisonExample(db);

        Console.WriteLine("\n=== Example completed successfully! ===");
    }

    static Database CreateDatabase()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE Authors (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL
            );

            CREATE TABLE Books (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                AuthorId INTEGER NOT NULL,
                FOREIGN KEY (AuthorId) REFERENCES Authors(Id)
            );

            INSERT INTO Authors (Name) VALUES ('George Orwell'), ('J.K. Rowling');
            
            INSERT INTO Books (Title, AuthorId) VALUES
                ('1984', 1),
                ('Animal Farm', 1),
                ('Harry Potter', 2);
        ";
        cmd.ExecuteNonQuery();

        Console.WriteLine("   ✓ Database created with sample data\n");
        return new Database(conn);
    }

    static void ReferenceNavigationExample(Database db)
    {
        Console.WriteLine("   Lazy loading reference navigation (many-to-one):");

        // Enable lazy loading
        db.EnableLazyLoading();

        var book = db.Single<Book>("SELECT * FROM Books WHERE Id = @0", 1);
        Console.WriteLine($"   ✓ Loaded book: {book.Title}");

        // First access to Author triggers lazy load
        Console.WriteLine($"   Accessing Author property...");
        var authorName = book.Author.Name;
        Console.WriteLine($"   ✓ Lazy loaded author: {authorName}");

        // Second access uses cached value
        Console.WriteLine($"   Second access: {book.Author.Name} (cached)");
    }

    static void CollectionNavigationExample(Database db)
    {
        Console.WriteLine("   Lazy loading collection navigation (one-to-many):");

        db.EnableLazyLoading();

        var author = db.Single<Author>("SELECT * FROM Authors WHERE Id = @0", 1);
        Console.WriteLine($"   ✓ Loaded author: {author.Name}");

        // First access to Books collection triggers lazy load
        Console.WriteLine($"   Accessing Books collection...");
        var bookCount = author.Books.Count;
        Console.WriteLine($"   ✓ Lazy loaded {bookCount} books");

        foreach (var book in author.Books)
        {
            Console.WriteLine($"     • {book.Title}");
        }
    }

    static void CircularReferenceExample(Database db)
    {
        Console.WriteLine("   Handling circular references with max depth:");

        db.EnableLazyLoading(maxDepth: 3); // Prevent infinite loops

        var book = db.Single<Book>("SELECT * FROM Books WHERE Id = @0", 1);
        
        // Book → Author → Books → Author (circular!)
        Console.WriteLine($"   Book: {book.Title}");
        Console.WriteLine($"     → Author: {book.Author.Name}");
        Console.WriteLine($"       → Books: {book.Author.Books.Count} books");
        
        // Accessing deeper prevents infinite loop
        try
        {
            var circular = book.Author.Books.First().Author.Books;
            Console.WriteLine($"     ⚠ Max depth reached - prevents infinite loop");
        }
        catch (InvalidOperationException)
        {
            Console.WriteLine($"     ✓ Max depth protection active");
        }
    }

    static void LazyLoadingConfigurationExample(Database db)
    {
        Console.WriteLine("   Lazy loading configuration options:");

        // Option 1: Enable globally
        db.EnableLazyLoading();
        Console.WriteLine("   ✓ Global: All entities use lazy loading");

        // Option 2: Max depth limit
        db.EnableLazyLoading(maxDepth: 5);
        Console.WriteLine("   ✓ Max depth: Prevent deep recursion (5 levels)");

        // Option 3: Disable for specific query
        var books = db.Query<Book>()
                      .DisableLazyLoading()
                      .ToList();
        Console.WriteLine($"   ✓ Disabled: {books.Count} books without lazy loading");

        // Option 4: Hybrid approach (Include + Lazy)
        var booksWithAuthors = db.Query<Book>()
                                 .Include(b => b.Author) // Eager load Author
                                 .ToList();
        Console.WriteLine("   ✓ Hybrid: Eager load Author, lazy load other relations");
    }

    static void ComparisonExample(Database db)
    {
        Console.WriteLine("   Eager vs Lazy Loading comparison:");

        Console.WriteLine("\n   EAGER LOADING:");
        Console.WriteLine("     ✓ Pros: Predictable queries, better for bulk operations");
        Console.WriteLine("     ✗ Cons: May load unnecessary data");

        var eagerBooks = db.Query<Book>()
                           .Include(b => b.Author)
                           .ToList();
        Console.WriteLine($"     Result: {eagerBooks.Count} books (1 query with JOIN)");

        Console.WriteLine("\n   LAZY LOADING:");
        Console.WriteLine("     ✓ Pros: Load only needed data, simpler code");
        Console.WriteLine("     ✗ Cons: N+1 queries if not careful");

        db.EnableLazyLoading();
        var lazyBooks = db.Query<Book>().ToList();
        Console.WriteLine($"     Result: {lazyBooks.Count} books (1 query)");
        Console.WriteLine($"     + {lazyBooks.Count} queries when accessing Author (N+1!)");

        Console.WriteLine("\n   RECOMMENDATION:");
        Console.WriteLine("     • Use Eager Loading for known relationships");
        Console.WriteLine("     • Use Lazy Loading for optional/conditional access");
        Console.WriteLine("     • Hybrid approach often best in practice");
    }
}

public class Author
{
    public virtual int Id { get; set; }
    public virtual string Name { get; set; } = string.Empty;
    public virtual ICollection<Book> Books { get; set; } = new List<Book>();
}

public class Book
{
    public virtual int Id { get; set; }
    public virtual string Title { get; set; } = string.Empty;
    public virtual int AuthorId { get; set; }
    public virtual Author Author { get; set; } = null!;
}
