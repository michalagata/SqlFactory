using System;
using System.Collections.Generic;
using System.Data.Linq.Mapping;
using System.Linq;
using System.Text;
using AnubisWorks.SQLFactory;

namespace RealLifeExample.Tips
{
    public class TestDatabase : Database
    {

        public SqlTable<LeaseAgreement> LAg
        {
            get { return Table<LeaseAgreement>(); }
        }
        
        public TestDatabase(string connectionString)
           : base(connectionString) { }

        public TestDatabase(string connectionString, MetaModel mapping)
           : base(connectionString, mapping) { }
    }
}
