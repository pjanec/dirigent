#
# Builds the projects that are meant to run on Linux (the console agent and the CLI).
#
# UNVERIFIED. Linux support is currently on paper: nothing here is exercised by the release
# procedure - make_release.ps1 builds Windows only, because a full Linux pass roughly doubled
# the wall clock for a platform that has no consumers yet. Reviving it is a task of its own.
#
# What this script no longer does is damage the working tree. It used to regex-replace
# <TargetFramework> in six .csproj files with net8.0 and flip Dirigent.Gui.ImGui to
# OutputType=Exe, and it never put either back - so one Linux build left the whole repository
# retargeted and the GUI turned into a console application. The framework now travels as a
# command-line property and OutputType is conditional inside the project file.
#
# The retarget needs its own restore, because the assets file is written per target
# framework; switching back to a Windows build will restore again.
#
Param( $buildconf = "Release", $clean = 1 )

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\build-lib.ps1"

$framework = "net8.0"

$projects = @(
	"src\Dirigent.Common\Dirigent.Common.csproj"
	"src\Dirigent.Agent.Core\Dirigent.Agent.Core.csproj"
	"src\Dirigent.Agent.Console\Dirigent.Agent.Console.csproj"
	"src\Dirigent.Gui.ImGui\Dirigent.Gui.ImGui.csproj"
	"src\Dirigent.CLI.Core\Dirigent.CLI.Core.csproj"
	"src\Dirigent.CLI\Dirigent.CLI.csproj"
)

if( $clean )
{
	Foreach ($proj in $projects)
	{
		"Cleaning $proj"
		Invoke-Checked "clean of $proj" { dotnet clean --nologo -c $buildconf -p:TargetFramework=$framework -v m $proj }
	}
}

Foreach ($proj in $projects)
{
	"Restoring $proj for $framework"
	Invoke-Checked "restore of $proj" { dotnet restore --nologo -p:TargetFramework=$framework $proj }
}

Foreach ($proj in $projects)
{
	"Building $proj"
	Invoke-Checked "build of $proj" { dotnet build --nologo -c $buildconf -p:TargetFramework=$framework --no-restore $proj }
}
