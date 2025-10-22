REM First make sure the script is present in your <your dirigent binary folder>\Scripts folder
SET BIN=src\Dirigent.CLI\bin\Debug\net6.0-windows
REM copy src\Dirigent.Common\Scripts %BIN%\Scripts
start /Dconfig %BIN%\Dirigent.CLI.exe StartScript "bd843fcb-b4b0-496e-b83e-4cc039ff7616" "Scripts/KillAllAppsOnMachine.cs" "'{""MachineName"":""m1"", ""TimeoutSeconds"":5}'"
