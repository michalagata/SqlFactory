using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Linq.Mapping;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using Newtonsoft.Json;
using RealLifeExample.Tips;

namespace RealLifeExample
{
    class Program
    {
        static TestDatabase db;

        public static void PerformConnection(string connectionString,TextWriter log)
        {

            db = new TestDatabase(connectionString)
            {
                Configuration = {
               Log = log
            }
            };
        }

        public static void Transactions_TransactionScope(LeaseAgreement input)
        {

            try
            {
                using (var tx = new TransactionScope())
                {
                    using (db.EnsureConnectionOpen())
                    {
                        // Connection AutoOpen()
                        Transactions_DoWork(input);
                        tx.Complete();
                    }
                    // Connection AutoClose()
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception occured: {0}", ex.Message);
            }
        }

        static void Transactions_DoWork(LeaseAgreement inn)
        {
            try
            {
                db.LAg.Add(inn);
                //method 1
                int ID1 = db.LAg.Find(inn).EstateId;
                //method 2
                int ID = (int)db.LastInsertId();
                inn.LeaseArea = 10m;
                db.LAg.Update(inn);
                db.LAg.Remove(inn);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception occured in transation: {0}", ex.Message);
            }
        }

        static void Main(string[] args)
        {

            Console.WriteLine("System.DateTime Maximum possible: {0}, Minimum Possible: {1}", System.Data.SqlTypes.SqlDateTime.MaxValue, System.Data.SqlTypes.SqlDateTime.MinValue);
            var result = JsonConvert.DeserializeObject<LeaseAgreement>(returnJSON());
            PerformConnection(ConfigurationManager.ConnectionStrings["database"].ConnectionString, null);
            Transactions_TransactionScope(result);
        }

        public static string returnJSON()
        {
            string text;
            using (var streamReader = new StreamReader(@"LeaseAgreement.json", Encoding.UTF8))
            {
                text = streamReader.ReadToEnd();
            }
            return text;
        }
    }
}
