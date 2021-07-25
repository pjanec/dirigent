Param( $buildconf="Release" )

## increment version number in the master version file
#& "$PSScriptRoot\increment-revno.ps1"

# generate version stamp file to be lated added to the release
$version = (Get-Content -path "$PSScriptRoot\version.txt")
& "$PSScriptRoot\gen-ver-stamp.ps1" "$PSScriptRoot\VersionStamp.txt" $version

# set the verison for the build processes started below
$env:RELEASE_VERSION = $version

# rebuld & publish for all platforms

& "$PSScriptRoot\publish-linux-x64.ps1" $buildconf

& "$PSScriptRoot\publish-linux-arm64.ps1" $buildconf

& "$PSScriptRoot\publish-win-x64.ps1" $buildconf



