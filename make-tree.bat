@echo off
cd /d "%~dp0"
tree /F /A | findstr /I /V "\.meta$" > tree.txt
echo Wrote "%~dp0tree.txt"
pause