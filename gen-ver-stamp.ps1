Param( $outFile="", $version="" )
if( !$outFile ) { "Missing output file full path as parameter"; exit 2 }

$PSDefaultParameterValues['Out-File:Encoding'] = 'ascii'
New-Item -Path $outFile -ItemType File -Force | Out-Null

if( $version )
{
  "$version" >> $outFile 
  "" >> $outFile 
}

"Source Code:`r`n" >> $outFile 

git config --get remote.origin.url >> $outFile 
git rev-parse HEAD >> $outFile 
git log -1 --date=iso --format=%cd >> $outFile 
"" >> $outFile 


Push-Location "$PSScriptRoot"
$localMods = $(git status --untracked-files=no --porcelain)
Pop-Location
if( $localMods )
{
  "LOCAL MODIFICATIONS!" >> $outFile 
  foreach( $line in $localMods )
  {
    "$line" >> $outFile 
  }
}
