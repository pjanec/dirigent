#
# Exercises the helpers in build-lib.ps1, including the two guards that would otherwise only
# ever be seen not firing.
#
#   powershell -NoProfile -File .\test-build-lib.ps1
#
# Every check is written to fail as well as to pass: a guard that has never been observed
# rejecting anything is indistinguishable from a guard that does nothing.
#
$ErrorActionPreference = "Stop"
. "$PSScriptRoot\build-lib.ps1"

$failed = 0
$passed = 0

function Check
{
	Param( [string]$What, [scriptblock]$Body )

	try
	{
		& $Body
		$script:passed++
		"  ok    $What"
	}
	catch
	{
		$script:failed++
		"  FAIL  $What"
		"          $($_.Exception.Message)"
	}
}

#
# PowerShell resolves variables up the call stack, not lexically, so a parameter here whose
# name matches a variable used inside $Body shadows it. Naming this one $Expected made
# -Expected $expected at the call site resolve to this parameter's value, and two of the
# tests below passed for the wrong reason. Hence $Pattern.
#
function Should-Throw
{
	Param( [string]$Pattern, [scriptblock]$Body )

	try
	{
		& $Body
	}
	catch
	{
		if( $_.Exception.Message -notlike "*$Pattern*" )
		{
			throw "threw, but not about '$Pattern': $($_.Exception.Message)"
		}
		return
	}
	throw "did not throw, expected something about '$Pattern'"
}

$root = Join-Path $env:TEMP ("build-lib-tests-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $root | Out-Null

try
{
	"Invoke-Checked"

	Check "passes a zero exit code" {
		Invoke-Checked "success" { cmd /c "exit 0" }
	}
	Check "throws on a non-zero exit code" {
		Should-Throw "exit code 3" { Invoke-Checked "failure" { cmd /c "exit 3" } }
	}
	Check "accepts the codes it is told to accept" {
		Invoke-Checked "robocopy-style" { cmd /c "exit 3" } -OkExitCodes @( 0, 1, 2, 3, 4, 5, 6, 7 )
	}
	Check "still throws outside the accepted set" {
		Should-Throw "exit code 8" {
			Invoke-Checked "robocopy-style" { cmd /c "exit 8" } -OkExitCodes @( 0, 1, 2, 3, 4, 5, 6, 7 )
		}
	}

	"Reset-Directory"

	Check "creates a directory that is not there" {
		$d = Join-Path $root "created"
		Reset-Directory $d
		if( -not (Test-Path $d) ) { throw "not created" }
	}
	Check "leaves an already empty directory alone" {
		$d = Join-Path $root "created"
		Reset-Directory $d
		if( @(Get-ChildItem $d).Count -ne 0 ) { throw "not empty" }
	}
	Check "empties a directory with nested content" {
		$d = Join-Path $root "full"
		New-Item -ItemType Directory -Force -Path (Join-Path $d "a\b") | Out-Null
		Set-Content -Path (Join-Path $d "top.txt") -Value "x"
		Set-Content -Path (Join-Path $d "a\b\buried.txt") -Value "y"
		Reset-Directory $d
		if( -not (Test-Path $d) ) { throw "directory itself was removed" }
		if( @(Get-ChildItem $d -Recurse).Count -ne 0 ) { throw "still holds something" }
	}
	Check "removes read-only files too" {
		$d = Join-Path $root "readonly"
		New-Item -ItemType Directory -Force -Path $d | Out-Null
		$f = Join-Path $d "ro.txt"
		Set-Content -Path $f -Value "z"
		(Get-Item $f).IsReadOnly = $true
		Reset-Directory $d
		if( @(Get-ChildItem $d -Recurse).Count -ne 0 ) { throw "read-only file survived" }
	}

	"Get-SevenZip"

	Check "finds 7-Zip on this machine" {
		$z = Get-SevenZip
		if( -not (Test-Path $z) ) { throw "returned a path that does not exist: $z" }
	}

	"Assert-ExpectedConfigFiles"

	# A stand-in for an assembled release folder: some binaries, the configuration files.
	$rel = Join-Path $root "release"
	New-Item -ItemType Directory -Force -Path (Join-Path $rel "Icons") | Out-Null
	"bin"  | Set-Content (Join-Path $rel "Dirigent.Agent.exe")
	"bin"  | Set-Content (Join-Path $rel "Dirigent.Agent.dll")
	"icon" | Set-Content (Join-Path $rel "Icons\app.ico")
	"cfg"  | Set-Content (Join-Path $rel "Dirigent.Agent.dll.config")
	"cfg"  | Set-Content (Join-Path $rel "SharedConfig.xml")

	$expected = @( "Dirigent.Agent.dll.config", "SharedConfig.xml" )

	Check "passes when the set matches exactly" {
		Assert-ExpectedConfigFiles -Path $rel -Expected $expected
	}
	Check "rejects an extra .xml nobody classified" {
		$stray = Join-Path $rel "NewThing.xml"
		"?" | Set-Content $stray
		try
		{
			Should-Throw "NewThing.xml" { Assert-ExpectedConfigFiles -Path $rel -Expected $expected }
		}
		finally { Remove-Item $stray -Force }
	}
	Check "rejects an extra .config nobody classified" {
		$stray = Join-Path $rel "Dirigent.New.dll.config"
		"?" | Set-Content $stray
		try
		{
			Should-Throw "Dirigent.New.dll.config" { Assert-ExpectedConfigFiles -Path $rel -Expected $expected }
		}
		finally { Remove-Item $stray -Force }
	}
	Check "notices one buried in a subfolder" {
		$stray = Join-Path $rel "Icons\buried.xml"
		"?" | Set-Content $stray
		try
		{
			Should-Throw "buried.xml" { Assert-ExpectedConfigFiles -Path $rel -Expected $expected }
		}
		finally { Remove-Item $stray -Force }
	}
	Check "rejects an expected file that went missing" {
		Should-Throw "Missing.xml" {
			Assert-ExpectedConfigFiles -Path $rel -Expected ( $expected + "Missing.xml" )
		}
	}

	"Assert-NoSatelliteFolders"

	Check "passes on a folder with no language subfolders" {
		Assert-NoSatelliteFolders $rel
	}
	Check "rejects a two-letter language folder" {
		$d = Join-Path $rel "de"
		New-Item -ItemType Directory -Force -Path $d | Out-Null
		try { Should-Throw "de" { Assert-NoSatelliteFolders $rel } }
		finally { Remove-Item $d -Recurse -Force }
	}
	Check "rejects a language-region folder" {
		$d = Join-Path $rel "zh-Hans"
		New-Item -ItemType Directory -Force -Path $d | Out-Null
		try { Should-Throw "zh-Hans" { Assert-NoSatelliteFolders $rel } }
		finally { Remove-Item $d -Recurse -Force }
	}
	Check "does not mistake a real folder for a culture" {
		# Icons, Resources and Scripts all ship in the release folder and must survive.
		foreach( $name in @( "Icons", "Resources", "Scripts" ) )
		{
			New-Item -ItemType Directory -Force -Path (Join-Path $rel $name) | Out-Null
		}
		Assert-NoSatelliteFolders $rel
	}
}
finally
{
	Remove-Item -Path $root -Recurse -Force -ErrorAction SilentlyContinue
}

""
"$passed passed, $failed failed"
exit ( [int]($failed -gt 0) )
