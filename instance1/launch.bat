@echo off
start "" "%~dp0..\vscode\Code.exe" --user-data-dir "%~dp0user-data" --extensions-dir "%~dp0..\vscode\data\extensions" %*
