#
# Publishes the four shipped Windows executables and assembles them into one folder,
# release\win-x64\<config>, which is what the release archive is made of.
#
# Every step is now checked, and both the intermediate publish folders and the assembled
# folder are emptied first. Neither used to be: dotnet publish does not clean its -o
# directory and the merge was robocopy /E without /PURGE, so a file that stopped being part
# of the product stayed in the release folder and went into the archive. Since a Dirigent
# upgrade is done by copying the new binaries over the old ones, it would then never leave
# the target machines either.
#
Param( $buildconf = "Release", $clean = 1 )

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\build-lib.ps1"

& "$PSScriptRoot\build-win.ps1" $buildconf $clean

$framework = "net8.0-windows"
$runtime = "win-x64"

# The four things an operator actually runs. Everything else in the solution reaches the
# release folder as a dependency of one of these.
$apps = @(
	"Dirigent.CLI"
	"Dirigent.Agent.WinForms"
	"Dirigent.Gui.ImGui"
	"Dirigent.Agent.Starter"
)

# 1. Publish each application on its own, into its own folder.
foreach( $app in $apps )
{
	$outDir = "$PSScriptRoot\publish\$app\$runtime\$buildconf"
	Reset-Directory $outDir

	"Publishing $app"
	Invoke-Checked "publish of $app" {
		dotnet publish "$PSScriptRoot\src\$app\$app.csproj" `
			--nologo -c $buildconf -f $framework -r $runtime --no-self-contained -o $outDir
	}
}

# 2. Assemble them into one folder. Done only now that every publish has succeeded, so a
#    failed build cannot leave a half-updated release folder behind.
$releasePath = "$PSScriptRoot\release\$runtime\$buildconf"
Reset-Directory $releasePath

foreach( $app in $apps )
{
	"Merging $app"
	Copy-Item -Path "$PSScriptRoot\publish\$app\$runtime\$buildconf\*" -Destination $releasePath -Recurse -Force
}

Copy-Item "$PSScriptRoot\VersionStamp.txt" -Destination $releasePath -Force

# 3. The publish script used to delete thirteen language folders by name here. They are no
#    longer produced; if they reappear, say so rather than tidying them away.
Assert-NoSatelliteFolders $releasePath

"Assembled $releasePath"
