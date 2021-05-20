$verfile = "version.txt"
$verstr = (Get-Content -path $verfile)
$match = $verstr | select-string "(\d+)\.(\d+)\.(\d+)\.(\d+)"
if( $match.matches.groups.Count -ge 4 )
{
    $v1 = $match.matches.groups[1].value -as [int]
    $v2 = $match.matches.groups[2].value -as [int]
    $v3 = $match.matches.groups[3].value -as [int]
    $v4 = $match.matches.groups[4].value -as [int]

    $v4 = $v4 + 1

    "$v1.$v2.$v3.$v4" | Out-File -encoding ascii $verfile
}
else
{
    Write-Error "Version number parsing error. $verstr"
}