@echo off
set com2019="C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\amd64\MsBuild.exe"
set pro2019="C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\amd64\MSBuild.exe"
set com2017="C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\amd64\MSBuild.exe"
echo ##################################
echo       Build Kuriimu2 Nugets
echo ##################################
echo.
echo ### Build Kuriimu2 Libraries ###
echo.
echo Building Kuriimu2.sln... 
if exist %com2019% (call %com2019% ..\..\Kuriimu2.sln /p:Configuration=Debug /p:WarningLevel=0 > nul 2>&1) ^
else if exist %pro2019% (call %pro2019% ..\..\Kuriimu2.sln /p:Configuration=Debug /p:WarningLevel=0 > nul 2>&1) ^
else (call %com2017% ..\..\Kuriimu2.sln /p:Configuration=Debug /p:WarningLevel=0 > nul 2>&1)
echo.
echo ### Build Nugets ###
echo.
nuget.exe pack ..\..\src\Kontract\Kontract.csproj -Properties Configuration=Debug
nuget.exe pack ..\..\src\Komponent\Komponent.csproj -Properties Configuration=Debug
nuget.exe pack ..\..\src\Kanvas\Kanvas.csproj -Properties Configuration=Debug
nuget.exe pack ..\..\src\Kryptography\Kryptography.csproj -Properties Configuration=Debug
nuget.exe pack ..\..\src\Kore\Kore.csproj -Properties Configuration=Debug
copy /y *.nupkg ..\..\nuget\*
del *.nupkg
echo.
echo ##################################
echo       Build Nugets Complete
echo ##################################
echo.
pause