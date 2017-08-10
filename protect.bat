@ECHO OFF
cd DEPLOYMENT
"C:\Program Files (x86)\AnubisWorks\BatchProtector.exe" .
"C:\Program Files (x86)\AnubisWorks\NuGEN.exe" /id SQLFactory /OutPutDir . /dll SQLFactory.dll /authors "Michael Agata" /owners "Michael Agata" /tags "sql,factory,dao,dto,orm,mapper"