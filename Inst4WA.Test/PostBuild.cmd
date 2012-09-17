REM Batch File To Create Folder Structure for HelloWorld Web Roles and Worker Roles
	
@ECHO OFF

setlocal
	
	set solutionDir=%1
	set targetDir=%2
	set testDataDir=%2TestData\
	set config=%3

	IF EXIST %testDataDir% RMDIR /S/Q %testDataDir%

	set webRoleDestRoot=%testDataDir%WebRole\
	set webRoleDestDir=%webRoleDestRoot%Bin
	set webRoleDestCSCFG=%webRoleDestRoot%ServiceConfiguration.Local.cscfg
	set webRoleDestCSDEF=%webRoleDestRoot%ServiceDefinition.csdef

	set webRoleSrcRoot=%1HelloWorld\HelloWorld.WebRole\
	set webRoleSrcBin=%webRoleSrcRoot%WebRole
	set webRoleSrcCSCFG=%webRoleSrcRoot%HelloWorld.WebRole\ServiceConfiguration.Local.cscfg
	set webRoleSrcCSDEF=%webRoleSrcRoot%HelloWorld.WebRole\ServiceDefinition.csdef
	
	IF EXIST %webRoleDestRoot% RMDIR /S/Q %webRoleDestRoot%
	mkdir %webRoleDestDir%
	
	rem set solrPkgDir=%testDataDir%SolrPkg\
	rem IF EXIST %solrPkgDir% RMDIR /S/Q %solrPkgDir%
	rem mkdir %solrPkgDir%\SolrMasterHostWorkerRole
	rem mkdir %solrPkgDir%\SolrSlaveHostWorkerRole
	rem mkdir %solrPkgDir%\SolrAdminWebRole

	rem set couchDbPkgDir=%testDataDir%CouchDbPkg\
	rem IF EXIST %couchDbPkgDir% RMDIR /S/Q %couchDbPkgDir%
	rem mkdir %couchDbPkgDir%\WorkerRole

	XCOPY /Y/I/Q/S %webRoleSrcBin% %webRoleDestDir%
	COPY /Y %webRoleSrcCSCFG% %webRoleDestCSCFG%
	COPY /Y %webRoleSrcCSDEF% %webRoleDestCSDEF%

	set workerRoleDestRoot=%testDataDir%WorkerRole\
	set workerRoleDestDir=%workerRoleDestRoot%Bin
	set workerRoleDestCSCFG=%workerRoleDestRoot%ServiceConfiguration.Local.cscfg
	set workerRoleDestCSDEF=%workerRoleDestRoot%ServiceDefinition.csdef

	set workerRoleSrcRoot=%1HelloWorld\HelloWorld.WorkerRole\
	set workerRoleSrcBin=%workerRoleSrcRoot%WorkerRole\bin\%config%
	set workerRoleSrcCSCFG=%workerRoleSrcRoot%HelloWorld.WorkerRole\ServiceConfiguration.Local.cscfg
	set workerRoleSrcCSDEF=%workerRoleSrcRoot%HelloWorld.WorkerRole\ServiceDefinition.csdef

	IF EXIST %workerRoleDestRoot% RMDIR /S/Q %workerRoleDestRoot%
	mkdir %workerRoleDestRoot%

	XCOPY /Y/I/Q/S %workerRoleSrcBin% %workerRoleDestDir%
	COPY /Y %workerRoleSrcCSCFG% %workerRoleDestCSCFG%
	COPY /Y %workerRoleSrcCSDEF% %workerRoleDestCSDEF%

	REM Copy Cmdline Exe

	set inst4WaExeSrcDir=%solutionDir%Inst4WA\bin\%config%\Inst4WA.exe
	set inst4WaExeDir=%targetDir%Inst4WaExe
	IF EXIST %inst4WaExeDir% RMDIR /S/Q %inst4WaExeDir%
	mkdir %inst4WaExeDir%

	COPY /Y %inst4WaExeSrcDir% %inst4WaExeDir% 

endlocal
GOTO:EOF