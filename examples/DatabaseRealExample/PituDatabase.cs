using System.Runtime.Serialization;
using System.ComponentModel.DataAnnotations.Schema;

namespace AnubisWorks.SQLFactory.DatabaseRealExample;

public class PituDatabase : AnubisWorks.SQLFactory.Database
{
    public PituDatabase(string connectionString)
        : base(connectionString) { }

    
    public PituDatabase(string connectionString, string providerName)
        : base(connectionString, providerName) { }

    public AnubisWorks.SQLFactory.SqlTable<CMO_PODMIOT> CmoPodmiot => this.Table<CMO_PODMIOT>();
    
}

[Table(Name = nameof(CMO_PODMIOT))]
[DataContract]
public class CMO_PODMIOT
{
    [Column]
    [DataMember]
    public Int64 ID { get; set; }
    
    [Column]
    [DataMember]
    public bool DO_KIR { get; set; }
}
