Param( $buildconf="Release" )

## increment version number in the master version file
#& "$PSScriptRoot\increment-revno.ps1"

# generate version stamp file to be lated added to the release
$version = (Get-Content -path "$PSScriptRoot\version.txt")
& "$PSScriptRoot\gen-ver-stamp.ps1" "$PSScriptRoot\VersionStamp.txt" $version

$exePath = "$PSScriptRoot\release\win-x64\$buildconf"

# set the verison for the build processes started below
$env:RELEASE_VERSION = $version

# rebuild & publish for all platforms

& "$PSScriptRoot\publish-linux-x64.ps1" $buildconf

& "$PSScriptRoot\publish-linux-arm64.ps1" $buildconf

& "$PSScriptRoot\publish-win-x64.ps1" $buildconf

# make zip for windows binaries
$zippath = "$PSScriptRoot\Dirigent-$version-win-x64.7z"
Remove-Item -Path $zippath -Force -ErrorAction SilentlyContinue
& "C:\Program Files\7-Zip\7z.exe" a -r -t7z $zippath "$exePath\*" "-xr!*.log" "-xr!*.config" "-xr!*.zip" "-xr!*.7z"

# make zip for sample configs
$zippath = "$PSScriptRoot\Dirigent-$version-win-x64-configs.7z"
Remove-Item -Path $zippath -Force -ErrorAction SilentlyContinue
& "C:\Program Files\7-Zip\7z.exe" a -t7z $zippath "$exePath\*.config"


