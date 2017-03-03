using System;
using System.Collections.Generic;
using System.Data.Linq.Mapping;
using System.IO;
using System.Linq;
using System.Text;
using System.Transactions;
using RealLifeExample.Tips;

namespace RealLifeExample
{
    public class Test
    {
        readonly TestDatabase db;

        public Test(string connectionString, MetaModel mapping, TextWriter log)
        {

            this.db = new TestDatabase(connectionString)
            {
                Configuration = {
               Log = log
            }
            };
        }
    }
}
