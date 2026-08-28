# script prints all dirigent clients

$DirigentCliExe = "$PSScriptRoot\..\src\Dirigent.CLI\bin\Debug\net8.0-windows\Dirigent.CLI.exe"

# Parses the output of Dirigent.CLI's GetAllClientsState.
# Returns a list of data structures containing fields Name, IsConnected, LastStatusAge, IP
function ParseClientsState( [string[]]$lines )
{
    $result = @()

    foreach ($line in $lines) {
        if ($line -match '^CLIENT:(.*):(.*):(.*):(.*)$') {
            $client = @{
                Name = $Matches[1]
                IsConnected = ($Matches[2] -eq '1')
                LastStatusAge = [double]::Parse($Matches[3])
                IP = $Matches[4]
            }
            $result += $client
        }
    }

    return $result
}

# runs the dirigent CLI program and returns the standard output as a list of lines
function RunDirigentCLI( [string]$arguments )
{
    # run a.exe and saves its output to a variable $output
    $output = & $DirigentCliExe $arguments
    return $output -split "`r`n"
}

# Get the list of connected machines as data structures
function GetClients {
    $lines = RunDirigentCLI GetAllClientsState
    return ParseClientsState -lines $lines
}

# prints the clients to the console
function PrintClients( [System.Collections.ArrayList]$clients )
{
    foreach ($client in $clients) {
        Write-Host "Name: $($client.Name), IsConnected: $($client.IsConnected), LastStatusAge: $($client.LastStatusAge), IP: $($client.IP)"
    }
}

$clients = GetClients
PrintClients $clients
