Param( $buildconf="Release" )

$framework = "net5.0"

function ReplaceTargetPlatform
{
    Param( [string]$csprojFileName, [string]$newPlatform )

    (Get-Content -path $csprojFileName) | % {
      $_ -Replace '<TargetFramework>[^\<]*</TargetFramework>', "<TargetFramework>$newPlatform</TargetFramework>"
     } |
     Out-File -encoding utf8 $csprojFileName
}

function ReplaceOutputType
{
    Param( [string]$csprojFileName, [string]$newPlatform )

    (Get-Content -path $csprojFileName) | % {
      $_ -Replace '<OutputType>[^\<]*</OutputType>', "<OutputType>$newPlatform</OutputType>"
     } |
     Out-File -encoding utf8 $csprojFileName
}

$projects = @(
  "src\Dirigent.Common\Dirigent.Common.csproj"
  "src\Dirigent.Agent.Core\Dirigent.Agent.Core.csproj"
  "src\Dirigent.Agent.Console\Dirigent.Agent.Console.csproj"
  "src\Dirigent.Gui.ImGui\Dirigent.Gui.ImGui.csproj"
  "src\Dirigent.CLI.Core\Dirigent.CLI.Core.csproj"
  "src\Dirigent.CLI\Dirigent.CLI.csproj"
)

Foreach ($proj in $projects)
{
    "Retargetting $proj => $framework"
    ReplaceTargetPlatform $proj $framework
}

# switch to classic console app (linux build does not support WinExe output)
ReplaceOutputType "src\Dirigent.Gui.ImGui\Dirigent.Gui.ImGui.csproj" "Exe"

Foreach ($proj in $projects)
{
    "Cleaning $proj"
    dotnet clean --nologo -c $buildconf -f $framework -v m $proj
}

Foreach ($proj in $projects)
{
    "Building $proj"
    dotnet build --nologo -c $buildconf -f $framework $proj
}
