using System;
using System.Collections.Generic;
using System.Data.Linq.Mapping;
using System.Linq;
using System.Text;


#if BRE
namespace BRE.UniFlow.Agat.SQLFactory
#else
namespace AnubisWorks.SQLFactory
#endif
{
    public sealed partial class SqlCommandBuilder<TEntity> where TEntity : class
    {
       public string returnPropperTableName(MetaType metaTyper)
       {
           string schema = "";
           string tabName = "";
           if (metaTyper.Table != null && metaTyper.Table.TableName.Contains("."))
           {
               string[] schemTable = metaTyper.Table.TableName.Split(Convert.ToChar("."));
               for (int i = 0; i < schemTable.Length; i++)
               {
                   if (i == 0) schema = schemTable[0];
                   else
                   {
                       tabName += schemTable[i];
                   }
               }
               return QuoteIdentifier(schema) + "." + QuoteIdentifier(tabName);
           }
           return metaTyper.Table != null ? metaTyper.Table.TableName : null;
       }
    }
}