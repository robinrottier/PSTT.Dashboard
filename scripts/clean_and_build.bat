@echo off
setlocal
set dp0=%~dp0
cd /d %dp0%\..
if not exist *.slnx (
	echo No solution file found in the current directory.
	exit /b 1
)

set sln=.\MqttDashboard.slnx
if not exist "%sln%" (
	echo Solution file "%sln%" not found.
	exit /b 1
)

set gt=
for /f "delims=" %%i in ('git status --porcelain') do set gt=1&&echo Modified: %%i
if defined gt (
	echo Uncommitted changes detected. Please commit or stash your changes before running this script.
	exit /b 1
)

echo Cleaning the .vs directory...
rd /s/q .vs
echo Cleaning the project...
dotnet clean %sln%
echo Building the project...
dotnet build %sln%
echo Build completed.
