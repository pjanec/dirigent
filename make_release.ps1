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
# The two archives split by upgrade behaviour, not by file type: an upgrade is done by
# copying the binaries over an existing installation, so the binaries archive must not carry
# anything an operator may have edited. Hence no .config and no .xml in it, and both the
# application configuration files and the sample configurations in the other one.
#
# That split used to be expressed as wildcards on both sides, which quietly misfiles anything
# new: a .xml the binaries genuinely need would have landed in the configs archive and nobody
# would have found out until a site unpacked it. The settled set is listed here instead, and
# anything not on the list fails the release.
#
$configFiles = @(
	"Dirigent.Agent.dll.config"
	"Dirigent.CLI.dll.config"
	"Dirigent.ImGui.dll.config"
	"LocalConfig.xml"
	"SharedConfig.xml"
)

$found = @( Get-ChildItem -Path $releasePath -Recurse -File |
	Where-Object { $_.Extension -eq ".config" -or $_.Extension -eq ".xml" } |
	ForEach-Object { $_.FullName.Substring( $releasePath.Length + 1 ) } |
	Sort-Object )

$unexpected = $found | Where-Object { $configFiles -notcontains $_ }
if( $unexpected )
{
	throw "Not sure which archive these belong in: $($unexpected -join ', '). " +
		"Add them to `$configFiles in make_release.ps1 if they are operator-editable, " +
		"or to the exclusion list of the binaries archive if they ship with the code."
}

$missing = $configFiles | Where-Object { $found -notcontains $_ }
if( $missing )
{
	throw "Expected configuration files are not in ${releasePath}: $($missing -join ', ')"
}

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
