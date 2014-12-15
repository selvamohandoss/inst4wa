@echo off
@echo test cpmmit
setlocal

if "%1"=="/?" goto usage
if "%1"=="-?" goto usage

@echo Building Inst4WA and for external release
@echo Deleting previous build

if exist build rmdir /S/Q build

@echo Building Debug
set Configuration=Debug

msbuild Inst4WA.sln
if errorlevel 1 goto error

mkdir build\Debug\TestData
rem mkdir build\Debug\TestData\SolrPkg\SolrAdminWebRole
rem mkdir build\Debug\TestData\SolrPkg\SolrMasterHostWorkerRole
rem mkdir build\Debug\TestData\SolrPkg\SolrSlaveHostWorkerRole
rem mkdir build\Debug\TestData\CouchDbPkg\WorkerRole

XCOPY /Y/I/Q/S Inst4WA.Test\TestData\WebRole build\Debug\TestData\WebRole
XCOPY /Y/I/Q/S Inst4WA.Test\TestData\WorkerRole build\Debug\TestData\WorkerRole
XCOPY /Y/I/Q/S Inst4WA.Test\TestData\VMRole build\Debug\TestData\VMRole
XCOPY /Y/I/Q/S Inst4WA.Test\bin\Debug build\Debug
XCOPY /Y/I/Q/S Inst4WA.Test\TestCases\* build\Debug\TestCases

@echo Copy contents of Resources directory to root
XCOPY /Y/I/Q/S build\Debug\Resources\* build\Debug
RMDIR /S/Q build\Debug\Resources

setlocal

if "%1"=="/?" goto usage
if "%1"=="-?" goto usage

@echo Building Inst4WA and for external release
@echo Deleting previous build

if exist build rmdir /S/Q build

@echo Building Debug
set Configuration=Debug

msbuild Inst4WA.sln
if errorlevel 1 goto error

mkdir build\Debug\TestData
rem mkdir build\Debug\TestData\SolrPkg\SolrAdminWebRole
rem mkdir build\Debug\TestData\SolrPkg\SolrMasterHostWorkerRole
rem mkdir build\Debug\TestData\SolrPkg\SolrSlaveHostWorkerRole
rem mkdir build\Debug\TestData\CouchDbPkg\WorkerRole

XCOPY /Y/I/Q/S Inst4WA.Test\TestData\WebRole build\Debug\TestData\WebRole
XCOPY /Y/I/Q/S Inst4WA.Test\TestData\WorkerRole build\Debug\TestData\WorkerRole
XCOPY /Y/I/Q/S Inst4WA.Test\TestData\VMRole build\Debug\TestData\VMRole
XCOPY /Y/I/Q/S Inst4WA.Test\bin\Debug build\Debug
XCOPY /Y/I/Q/S Inst4WA.Test\TestCases\* build\Debug\TestCases

@echo Copy contents of Resources directory to root
XCOPY /Y/I/Q/S build\Debug\Resources\* build\Debug
RMDIR /S/Q build\Debug\Resources

COPY /Y Inst4WA.Test\Inst4WaExe\Inst4WA.exe build\Debug\Inst4WA.exe
COPY /Y Build.testsettings build\Debug\Build.testsettings
COPY /Y Inst4WA.Test\Azure.publishsettings  build\Debug

@echo Building Release
set Configuration=Release

RMDIR Inst4WA.Test\TestData

msbuild Inst4WA.sln
if errorlevel 1 goto error

mkdir build\Release\TestData
rem mkdir build\Release\TestData\SolrPkg\SolrAdminWebRole
rem mkdir build\Release\TestData\SolrPkg\SolrMasterHostWorkerRole
rem mkdir build\Release\TestData\SolrPkg\SolrSlaveHostWorkerRole
rem mkdir build\Release\TestData\CouchDbPkg\WorkerRole

XCOPY /Y/I/Q/S Inst4WA.Test\TestData\WebRole build\Release\TestData\WebRole
XCOPY /Y/I/Q/S Inst4WA.Test\TestData\WorkerRole build\Release\TestData\WorkerRole
XCOPY /Y/I/Q/S Inst4WA.Test\TestData\VMRole build\Release\TestData\VMRole
XCOPY /Y/I/Q/S Inst4WA.Test\bin\Release build\Release
XCOPY /Y/I/Q/S Inst4WA.Test\TestCases\* build\Release\TestCases

XCOPY /Y/I/Q/S build\Release\Resources\* build\Release
RMDIR /S/Q build\Release\Resources

COPY /Y Inst4WA.Test\Inst4WaExe\Inst4WA.exe build\Release\Inst4WA.exe
COPY /Y Build.testsettings build\Release\Build.testsettings
COPY /Y Inst4WA.Test\Azure.publishsettings  build\Release

goto noerror

:error
@echo !!! Build Error !!!
goto end

:noerror
exit /b 0

:usage
@echo.
@echo Builds Inst4WA for external release
@echo Usage: build
goto end

:end
endlocal
