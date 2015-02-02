@echo off

set bb.build.msbuild.exe=
for /D %%D in (%SYSTEMROOT%\Microsoft.NET\Framework\v4*) do set msbuild.exe=%%D\MSBuild.exe

if not defined msbuild.exe echo error: can't find MSBuild.exe & goto :eof
if not exist "%msbuild.exe%" echo error: %msbuild.exe%: not found & goto :eof

echo Cleaning
%msbuild.exe% /target:Clean /p:Configuration=Release /nologo

echo Building
%msbuild.exe% /p:Configuration=Release /nologo

echo Collecting binaries

del bin /S /Q

xcopy DocumentDb\bin\Release\DocumentDb.exe bin\*.*
xcopy DocumentDb\bin\Release\DocumentDb.exe.config bin\*.*
xcopy DocumentDb\bin\Release\*.dll bin\*.*

echo Done

:eof