REM Build Solution
cls
dotnet restore SQLFactory.sln
SET CONFIGURATION=%1
set PATH_SOURCE_SLN="%cd%\SQLFactory.sln"
if [%1]==[] (
  SET CONFIGURATION=Release
)
MSBuild %PATH_SOURCE_SLN% /p:Configuration=%CONFIGURATION%
cd DEPLOYMENT\
del /S /Q *.pdb
cd ..