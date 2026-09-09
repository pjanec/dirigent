#
# Produces the release archives for Windows.
#
#   .\make_release.ps1                 # test, build, publish, pack
#   .\make_release.ps1 -SkipTests      # when the tests were already run in this state
#
# The version comes from version.txt, which increment-revno.ps1 bumps. It is no longer
# passed to the build through an environment variable - Directory.Build.props reads the file
# directly, so every build carries the same number.
#
# Linux is deliberately not built here: a full Linux pass roughly doubled the wall clock for
# a platform that has no consumers yet. build-linux.ps1 and publish-linux-*.ps1 are kept but
# unverified; reviving them is a task of its own.
#
Param(
	[string]$buildconf = "Release",
	[switch]$SkipTests
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\build-lib.ps1"

$version = (Get-Content -Path "$PSScriptRoot\version.txt" -Raw).Trim()
if( $version -notmatch '^\d+\.\d+\.\d+\.\d+$' )
{
	throw "version.txt does not hold a four-part version number: '$version'"
}

"Releasing $version ($buildconf)"

# 1. Run the tests. The release procedure used to go straight from the version stamp to the
#    archives, so nothing stood between a broken build and a shipped .7z.
if( $SkipTests )
{
	"Skipping tests (-SkipTests)"
}
else
{
	"Running the test suite"
	Invoke-Checked "test suite" { dotnet test "$PSScriptRoot\src\Dirigent.NetCore.sln" -c $buildconf --nologo }
}

# 2. Record what is being released: version, origin, commit, commit date, and any local
#    modifications. Generated before the build, which is safe now that the build no longer
#    edits tracked files itself.
& "$PSScriptRoot\gen-ver-stamp.ps1" "$PSScriptRoot\VersionStamp.txt" $version

# 3. Build, publish and assemble release\win-x64\$buildconf.
& "$PSScriptRoot\publish-win-x64.ps1" $buildconf

$releasePath = "$PSScriptRoot\release\win-x64\$buildconf"
$sevenZip = Get-SevenZip

#
# The operator-editable files, which go into the configs archive and are kept out of the
# binaries archive so that an upgrade can be done by copying binaries over an existing
# installation. Assert-ExpectedConfigFiles explains why the set is named rather than matched
# by extension; anything else with those extensions fails the release.
#
$configFiles = @(
	"Dirigent.Agent.dll.config"
	"Dirigent.CLI.dll.config"
	"Dirigent.ImGui.dll.config"
	"LocalConfig.xml"
	"SharedConfig.xml"
)

Assert-ExpectedConfigFiles -Path $releasePath -Expected $configFiles

# 4. Binaries: everything except the configuration files, the logs and any stray archives.
$binZip = "$PSScriptRoot\Dirigent-$version-win-x64.7z"
Remove-Item -Path $binZip -Force -ErrorAction SilentlyContinue
Invoke-Checked "packing $binZip" {
	& $sevenZip a -r -t7z $binZip "$releasePath\*" `
		"-xr!*.log" "-xr!*.config" "-xr!*.xml" "-xr!*.zip" "-xr!*.7z"
}

# 5. Configuration: the application configuration files plus the sample configurations.
$cfgZip = "$PSScriptRoot\Dirigent-$version-win-x64-configs.7z"
Remove-Item -Path $cfgZip -Force -ErrorAction SilentlyContinue
$cfgPaths = $configFiles | ForEach-Object { Join-Path $releasePath $_ }
Invoke-Checked "packing $cfgZip" { & $sevenZip a -t7z $cfgZip $cfgPaths }

""
"Done:"
Get-Item $binZip, $cfgZip | ForEach-Object { "  {0}  ({1:N0} bytes)" -f $_.Name, $_.Length }
