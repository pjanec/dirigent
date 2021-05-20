Param( $ver="" )

$verfile = "version.txt"

if( [string]::IsNullOrEmpty( $ver ) )
{
    $ver = (Get-Content -path $verfile)
}


function UpdateVersion
{
    Param( [string]$csprojFileName, [string]$newVerStr )

    (Get-Content -path $csprojFileName) | % {
      $_ -Replace '<Version>[^\<]*</Version>', "<Version>$newVerStr</Version>"
     } |
     Out-File -encoding utf8 $csprojFileName
}


UpdateVersion "src\Dirigent.Agent\Dirigent.Agent.csproj"                  $ver
UpdateVersion "src\Dirigent.Agent.Core\Dirigent.Agent.Core.csproj"        $ver
UpdateVersion "src\Dirigent.Common\Dirigent.Common.csproj"                $ver
UpdateVersion "src\Dirigent.Gui.WinForms\Dirigent.Gui.WinForms.csproj"    $ver
UpdateVersion "src\Dirigent.Gui.ImGui\Dirigent.Gui.ImGui.csproj"          $ver

