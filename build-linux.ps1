Param( $buildconf="Release" )

function ReplaceTargetPlatform
{
    Param( [string]$csprojFileName, [string]$newPlatform )

    (Get-Content -path $csprojFileName) | % {
      $_ -Replace '<TargetFramework>[^\<]*</TargetFramework>', "<TargetFramework>$newPlatform</TargetFramework>"
     } |
     Out-File -encoding utf8 $csprojFileName
}


ReplaceTargetPlatform "src\Dirigent.Agent\Dirigent.Agent.csproj"             "net5.0"
ReplaceTargetPlatform "src\Dirigent.Agent.Core\Dirigent.Agent.Core.csproj"   "net5.0"
ReplaceTargetPlatform "src\Dirigent.Common\Dirigent.Common.csproj"           "net5.0"
ReplaceTargetPlatform "src\Dirigent.Gui.ImGui\Dirigent.Gui.ImGui.csproj"     "net5.0"

dotnet build -c $buildconf --no-incremental src\Dirigent.Common\Dirigent.Common.csproj
dotnet build -c $buildconf --no-incremental src\Dirigent.Agent.Core\Dirigent.Agent.Core.csproj
dotnet build -c $buildconf --no-incremental src\Dirigent.Agent\Dirigent.Agent.csproj


