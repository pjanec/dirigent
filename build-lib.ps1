#
# Helpers shared by the build, publish and release scripts. Dot-source it:
#
#   . "$PSScriptRoot\build-lib.ps1"
#

#
# Runs a command and throws if it reported failure.
#
# Native tools - dotnet, 7z, robocopy - return an exit code and do not raise, so nothing
# stops a script that ignores $LASTEXITCODE. The release scripts ignored it everywhere,
# which meant a failed publish still produced a .7z: the archive was built from whatever
# survived in the release folder from the previous run, under the new version number.
#
function Invoke-Checked
{
	Param(
		[Parameter(Mandatory)] [string]$What,
		[Parameter(Mandatory)] [scriptblock]$Action,
		# Robocopy reports success as a bitmask: 0-7 mean copied/skipped/extra, 8 and above
		# are real failures. Callers that need that pass their own set of acceptable codes.
		[int[]]$OkExitCodes = @( 0 )
	)

	& $Action
	if( $OkExitCodes -notcontains $LASTEXITCODE )
	{
		throw "$What failed with exit code $LASTEXITCODE"
	}
}

#
# Finds 7-Zip. The release script hardcoded C:\Program Files\7-Zip\7z.exe and did not check
# that it existed, so a machine without it produced a release with no archives and no
# complaint.
#
function Get-SevenZip
{
	$onPath = Get-Command "7z.exe" -ErrorAction SilentlyContinue
	if( $onPath ) { return $onPath.Source }

	$candidates = @(
		"$env:ProgramFiles\7-Zip\7z.exe"
		"${env:ProgramFiles(x86)}\7-Zip\7z.exe"
		"$env:ChocolateyInstall\bin\7z.exe"
	)
	foreach( $c in $candidates )
	{
		if( $c -and (Test-Path $c) ) { return $c }
	}

	throw "7-Zip not found. Install it, or put 7z.exe on the PATH."
}

#
# Empties a directory without deleting the directory itself, and creates it if missing.
#
# Both the publish outputs and the merged release folder used to accumulate: dotnet publish
# does not clean its -o directory and the merge was robocopy /E without /PURGE. A file that
# stopped being part of the product therefore stayed in the release folder, and from there
# went into the archive - and because a Dirigent upgrade is done by copying the new binaries
# over the old ones, it would then never leave the target machines either.
#
function Reset-Directory
{
	Param( [Parameter(Mandatory)] [string]$Path )

	if( Test-Path $Path )
	{
		Remove-Item -Path "$Path\*" -Recurse -Force -ErrorAction Stop
	}
	else
	{
		New-Item -ItemType Directory -Force -Path $Path | Out-Null
	}
}
