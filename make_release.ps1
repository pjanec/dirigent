Param( $buildconf="Release" )

& "$PSScriptRoot\increment-revno.ps1"

& "$PSScriptRoot\publish-win-x64.ps1" $buildconf

& "$PSScriptRoot\publish-linux-x64.ps1" $buildconf

& "$PSScriptRoot\publish-linux-arm64.ps1" $buildconf



