:: NOte: rebuild the solution first to get same build/revision numbers for the asseblies

SET MASKS=*.dll,*.exe,*.config,*.pdb,*.md,Ver*.txt

mkdir release
del /q release\*.*

pushd binaries
for %%a in (%MASKS%) do del /q %%a
call _update.cmd %*
for %%a in (%MASKS%) do copy /y %%a ..\release\%%a
popd


  