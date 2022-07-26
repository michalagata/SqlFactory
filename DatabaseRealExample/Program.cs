using System.Data.Common;
using System.Data.SqlClient;
//using Microsoft.Data.SqlClient;
using AnubisWorks.SQLFactory.DatabaseRealExample;

Console.WriteLine("Hello, World!");

var connStr = "Application Name=KirCmoDAL;Server=ORION\\SQL;Database=KirCMO;Trusted_Connection=True;MultipleActiveResultSets=True";

// 1) ConnectionStringSettings nie istnieje w netcore - to jest w C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App\3.1.24\System.Configuration.ConfigurationManager.dll
// 2) new SqlConnectionStringBuilder(connStr); nie ma pola .ProviderName

//try {
//    new PituDatabase(
//        connStr
//    );
//} catch (Exception ex) { Console.WriteLine($"KABOOM(1): {ex.Message}"); }

//try {
//    new PituDatabase(
//        connStr,
//        "Microsoft.Data.SqlClient"
//    );
//} catch (Exception ex) { Console.WriteLine($"KABOOM(2): {ex.Message}"); }

//DbProviderFactories.RegisterFactory("System.Data.SqlClient", SqlClientFactory.Instance);

try {
    new PituDatabase(
        connStr,
        "System.Data.SqlClient"
    );
} catch (Exception ex) { Console.WriteLine($"KABOOM(3): {ex.Message}"); }

try {
    // Zamieniając "Microsoft.Data.SqlClient" na "System.Data.SqlClient" też nie działa

    //AnubisWorks.SQLFactory.DatabaseConfiguration.DefaultProviderInvariantName = "Microsoft.Data.SqlClient";
    //System.Data.Common.DbProviderFactories.RegisterFactory(AnubisWorks.SQLFactory.DatabaseConfiguration.DefaultProviderInvariantName, SqlClientFactory.Instance);

    new PituDatabase(
        connStr,
        "Microsoft.Data.SqlClient"
    );
} catch (Exception ex) { Console.WriteLine($"KABOOM(4) [mowi, że nie dziedziczy a nie ma innej alternatywy dla System.Data.Common.DbProviderFactory]: {ex.Message}"); }

