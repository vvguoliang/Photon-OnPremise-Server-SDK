@echo off
:: The available verbosity levels are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic]
set verbosity=minimal
set buildfile=deploy.proj
set configuration=Release

:: MSBuild location 
set vswhere="%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"

if not exist %vswhere% (
   echo VS not installed in %vswhere%.
   pause
   goto eof
)

::VS17 and lower
for /f "usebackq tokens=*" %%i in (`%vswhere% -latest -products * -requires Microsoft.Component.MSBuild -property installationPath`) do (
  set InstallDir=%%i
)
::VS19 and higher
if not exist "%InstallDir%\MSBuild\15.0\Bin\MSBuild.exe" (
for /f "usebackq tokens=*" %%i in (`%vswhere% -latest -requires Microsoft.Component.MSBuild -find MSBuild\*\Bin\MSBuild.exe`) do (
  set msbuild="%%i"
)) else (
    set msbuild="%InstallDir%\MSBuild\15.0\Bin\MSBuild.exe"
)

if not exist %msbuild% (
  echo %msbuild% not found.
  pause
  goto eof
)

:start
cls
echo.
echo            ************************************************************
echo            *                     Build Prompt                         *
echo            ************************************************************
echo.
echo            Build and Copy Server Binaries To Deploy Folder
echo            Configuration: %configuration%
echo            Buildfile:     %buildfile%
echo.
echo            1.  Counter Publisher
echo            2.  Loadbalancing
echo            3.  Nameserver
echo.
echo            9.  All
echo.
echo            0.  Exit
echo.

:begin
IF NOT EXIST .\log\ MD .\log
set eof=
set choice=
set /p choice=Enter option 
if not '%choice%'=='' set choice=%choice:~0,1%
if '%choice%'=='1' call :counterpublisher
if '%choice%'=='2' call :loadbalancing
if '%choice%'=='3' call :nameserver
if '%choice%'=='9' call :buildall
if '%choice%'=='0' goto eof
if '%eof%'=='' ECHO "%choice%" is not valid please try again
if '%eof%'=='' goto begin
pause
goto start

:counterpublisher
set rootpath=CounterPublisher
set slnfile=CounterPublisher.sln
set binpath=
set dst=CounterPublisher
%msbuild% %buildfile% /verbosity:%verbosity% /l:FileLogger,Microsoft.Build.Engine;logfile=log\%rootpath%Build.log;verbosity=%verbosity%;performancesummary /property:slnfile="%slnfile%" /property:binp="%binpath%" /property:rootpath="%rootpath%" /property:dst="%dst%" /t:BuildAndCopyForDeploy
goto done

:mmo
set rootpath=Mmo
set slnfile=Photon.Mmo.sln
set binpath=Photon.MmoDemo.Server\\
set dst=MmoDemo
%msbuild% %buildfile% /verbosity:%verbosity% /l:FileLogger,Microsoft.Build.Engine;logfile=log\%rootpath%Build.log;verbosity=%verbosity%;performancesummary /property:slnfile="%slnfile%" /property:binp="%binpath%" /property:rootpath="%rootpath%" /property:dst="%dst%" /t:BuildAndCopyForDeploy
goto done

:loadbalancing
%msbuild% %buildfile% /verbosity:%verbosity% /l:FileLogger,Microsoft.Build.Engine;logfile=log\LoadbalancingBuild.log;verbosity=%verbosity%;performancesummary /property:Configuration="%configuration%" /t:BuildLoadbalancing
goto done

:nameserver
set rootpath=NameServer
set slnfile=NameServer.sln
set binpath=Photon.NameServer\\
set dst=NameServer
%msbuild% %buildfile% /verbosity:%verbosity% /l:FileLogger,Microsoft.Build.Engine;logfile=log\%rootpath%Build.log;verbosity=%verbosity%;performancesummary /property:slnfile="%slnfile%" /property:binp="%binpath%" /property:rootpath="%rootpath%" /property:dst="%dst%" /t:BuildAndCopyForDeploy
goto done

:buildall
%msbuild% %buildfile% /verbosity:%verbosity% /l:FileLogger,Microsoft.Build.Engine;logfile=log\Build.log;verbosity=%verbosity%;performancesummary /property:Configuration="%configuration%" /t:BuildAndCopyForDeployComplete

REM call :policy
REM call :counterpublisher
REM call :mmo

:done
:eof
set eof=1
