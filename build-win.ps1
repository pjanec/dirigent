#
# Builds every shipped project for Windows.
#
# This script used to regex-replace <TargetFramework> and <OutputType> inside the .csproj
# files before building, and never restored them. For Windows that was a no-op on the
# content but still rewrote eight tracked files on every build - which is how a UTF-8 BOM
# ended up in some of them. The framework now comes from the command line and OutputType is
# conditional inside Dirigent.Gui.ImGui.csproj, so the working tree is left alone.
#
Param( $buildconf = "Release", $clean = 1 )

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\build-lib.ps1"

$framework = "net8.0-windows"

$projects = @(
	"src\Dirigent.Common\Dirigent.Common.csproj"
	"src\Dirigent.Agent.Core\Dirigent.Agent.Core.csproj"
	"src\Dirigent.CLI.Core\Dirigent.CLI.Core.csproj"
	"src\Dirigent.CLI\Dirigent.CLI.csproj"
	"src\Dirigent.Gui.ImGui\Dirigent.Gui.ImGui.csproj"
	"src\Dirigent.Agent.Console\Dirigent.Agent.Console.csproj"
	"src\Dirigent.Agent.WinForms\Dirigent.Agent.WinForms.csproj"
	"src\Dirigent.Agent.Starter\Dirigent.Agent.Starter.csproj"
)

if( $clean )
{
	Foreach ($proj in $projects)
	{
		"Cleaning $proj"
		Invoke-Checked "clean of $proj" { dotnet clean --nologo -c $buildconf -f $framework -v m $proj }
	}
}

Foreach ($proj in $projects)
{
	"Building $proj"
	Invoke-Checked "build of $proj" { dotnet build --nologo -c $buildconf -f $framework $proj }
}
