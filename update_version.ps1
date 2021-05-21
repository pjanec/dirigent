Param( $ver="" )

$verfile = "version.txt"

if( [string]::IsNullOrEmpty( $ver ) )
{
    $ver = (Get-Content -path $verfile)
}


function UpdateVersion
{
    Param( [string]$csprojFileName, [string]$newVerStr )

    $baseName = [io.path]::GetFileNameWithoutExtension($csprojFileName)

    "$baseName => $newVerStr"

    (Get-Content -path $csprojFileName) | % {
      $_ -Replace '<Version>[^\<]*</Version>', "<Version>$newVerStr</Version>"
     } |
     Out-File -encoding utf8 $csprojFileName
}


Get-ChildItem "$PSScriptRoot" -Filter *.csproj -Recurse | 
Foreach-Object {
    $fname = $_.FullName
    UpdateVersion $fname $ver
}


#UpdateVersion "src\Dirigent.Common\Dirigent.Common.csproj"                  $ver
#UpdateVersion "src\Dirigent.Agent.Core\Dirigent.Agent.Core.csproj"          $ver
#UpdateVersion "src\Dirigent.Agent.Console\Dirigent.Agent.Console.csproj"    $ver
#UpdateVersion "src\Dirigent.Agent.WinForms\Dirigent.Agent.WinForms.csproj"  $ver
#UpdateVersion "src\Dirigent.Gui.ImGui\Dirigent.Gui.ImGui.csproj"            $ver
#UpdateVersion "src\Dirigent.CLI.Core\Dirigent.CLI.Core.csproj"              $ver
#UpdateVersion "src\Dirigent.CLI\Dirigent.CLI.csproj"                        $ver

