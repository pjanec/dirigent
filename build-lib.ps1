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

#
# Checks that the operator-editable files in an assembled release folder are exactly the ones
# expected, and nothing else with those extensions is present.
#
# The release ships two archives split by upgrade behaviour rather than by file type: an
# upgrade is done by copying the binaries over an existing installation, so the binaries
# archive must carry nothing an operator may have edited - no .config, no .xml. Both sides of
# that split used to be wildcards, which quietly misfiles anything new: a .xml the binaries
# genuinely needed would have gone into the configs archive instead, and nobody would have
# found out until a site unpacked it. Naming the set turns that into a failed release.
#
function Assert-ExpectedConfigFiles
{
	Param(
		[Parameter(Mandatory)] [string]$Path,
		[Parameter(Mandatory)] [string[]]$Expected
	)

	# The base has to come from Get-Item rather than Resolve-Path: a child's FullName is built
	# from the parent's, so trimming anything else off it is arithmetic on two different
	# spellings of the same path. Resolve-Path expands an 8.3 name like PETR~1.JAN to the long
	# form, which is two characters longer, and the names come out with their first characters
	# eaten.
	$base = (Get-Item -LiteralPath $Path).FullName.TrimEnd( '\' )

	$found = @( Get-ChildItem -Path $Path -Recurse -File |
		Where-Object { $_.Extension -eq ".config" -or $_.Extension -eq ".xml" } |
		ForEach-Object { $_.FullName.Substring( $base.Length + 1 ) } |
		Sort-Object )

	$unexpected = @( $found | Where-Object { $Expected -notcontains $_ } )
	if( $unexpected.Count -gt 0 )
	{
		throw ( "Not sure which archive these belong in: {0}. Add them to the expected list in " +
			"make_release.ps1 if an operator may edit them, or to the exclusion list of the " +
			"binaries archive if they ship with the code." ) -f ($unexpected -join ", ")
	}

	$missing = @( $Expected | Where-Object { $found -notcontains $_ } )
	if( $missing.Count -gt 0 )
	{
		throw "Expected configuration files are not in ${Path}: $($missing -join ", ")"
	}
}

#
# Complains about localized resource assemblies instead of deleting them.
#
# SatelliteResourceLanguages in Directory.Build.props stops them being built, which replaced
# a loop in the publish script that deleted thirteen language folders by name. If some
# dependency ever ignores that property the folders come back, and silently deleting them
# from a hardcoded list is how that would stay invisible.
#
function Assert-NoSatelliteFolders
{
	Param( [Parameter(Mandatory)] [string]$Path )

	$cultureLike = @( Get-ChildItem -Path $Path -Directory |
		Where-Object { $_.Name -match '^[a-z]{2}(-[A-Za-z]{2,4})?$' } )

	if( $cultureLike.Count -gt 0 )
	{
		$names = ($cultureLike | ForEach-Object { $_.Name }) -join ", "
		throw ( "Unexpected localized resource folders in ${Path}: $names. " +
			"SatelliteResourceLanguages in Directory.Build.props should have prevented these." )
	}
}
