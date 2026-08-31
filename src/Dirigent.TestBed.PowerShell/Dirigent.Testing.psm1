<#
.SYNOPSIS
    Tier-2 test driver for Dirigent: brings up a world of real Dirigent processes on this machine
    and talks to it over the command-line interface, the way an operator or a CI job would.

.DESCRIPTION
    The worlds themselves are described once, in C#, by the scenario model in Dirigent.TestBed.
    Dirigent.TestBed.Gen renders one to a folder; everything here works from that folder and the
    manifest in it, so PowerShell never keeps a second copy of what a world looks like.

    Windows PowerShell 5.1, no modules to install.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---- finding the built binaries -------------------------------------------------------

function Get-DirigentRepoRoot
{
    <#  .SYNOPSIS The repository root, found by walking up from this module. #>
    $dir = $PSScriptRoot
    while ( $dir )
    {
        if ( Test-Path ( Join-Path $dir 'src\Dirigent.NetCore.sln' ) ) { return $dir }
        $dir = Split-Path $dir -Parent
    }
    throw "Could not find the repository root above '$PSScriptRoot'."
}

function Get-DirigentTool
{
    <#  .SYNOPSIS The most recently built copy of an executable belonging to a project. #>
    param(
        [Parameter(Mandatory)][string] $Project,
        [Parameter(Mandatory)][string] $Exe
    )

    $projectDir = Join-Path ( Get-DirigentRepoRoot ) "src\$Project"
    if ( -not ( Test-Path $projectDir ) ) { throw "No such project folder: $projectDir" }

    $found = Get-ChildItem -Path $projectDir -Filter $Exe -Recurse -File -ErrorAction SilentlyContinue |
                Sort-Object LastWriteTimeUtc -Descending |
                Select-Object -First 1

    if ( -not $found )
    {
        throw "$Exe not found under $projectDir. Build the solution first: " +
              "dotnet build src\Dirigent.NetCore.sln"
    }

    return $found.FullName
}

function Get-DirigentFreePort
{
    <#  .SYNOPSIS A TCP port nobody is listening on right now. #>
    $listener = New-Object -TypeName System.Net.Sockets.TcpListener `
                    -ArgumentList ( [System.Net.IPAddress]::Loopback, 0 )
    $listener.Start()
    try   { return ( [System.Net.IPEndPoint] $listener.LocalEndpoint ).Port }
    finally { $listener.Stop() }
}

# ---- the world ------------------------------------------------------------------------

function New-DirigentWorldFiles
{
    <#  .SYNOPSIS Renders a scenario to a folder and returns its manifest. #>
    param(
        [Parameter(Mandatory)][string] $Scenario,
        [Parameter(Mandatory)][string] $Root
    )

    $gen = Get-DirigentTool -Project 'Dirigent.TestBed.Gen' -Exe 'Dirigent.TestBed.Gen.exe'

    New-Item -ItemType Directory -Force -Path $Root | Out-Null
    $output = & $gen --scenario $Scenario --out $Root --force 2>&1
    if ( $LASTEXITCODE -ne 0 )
    {
        throw "generating scenario '$Scenario' failed: $output"
    }

    $manifestPath = Join-Path $Root 'world.json'
    if ( -not ( Test-Path $manifestPath ) ) { throw "no manifest written to $manifestPath" }

    return ( Get-Content $manifestPath -Raw | ConvertFrom-Json )
}

function Start-DirigentWorld
{
    <#
    .SYNOPSIS
        Starts a master (hosting the first machine's agent) and one agent process per further
        machine, and returns once they are all connected.

    .PARAMETER Gui
        Run the first machine as the WinForms tray GUI instead of the console daemon, so a human
        can watch and click. Use with Invoke-DirigentTests.ps1 -KeepAlive.

    .PARAMETER WithHttp
        Also open the web server, for tests of the REST surface.

    .PARAMETER Visible
        Leave the agent console windows in a normal window instead of minimized. Off by default:
        a test run must not throw windows at whoever is using the machine.
    #>
    [CmdletBinding()]
    param(
        [string] $Scenario = 'LoggingWorld',
        [string] $Root,
        [switch] $Gui,
        [switch] $WithHttp,
        [switch] $Visible,
        [int]    $TimeoutSec = 40
    )

    if ( -not $Root )
    {
        $tag = [Guid]::NewGuid().ToString( 'N' ).Substring( 0, 6 )
        $Root = Join-Path $env:TEMP "DirigentTier2\$tag"
    }

    $manifest = New-DirigentWorldFiles -Scenario $Scenario -Root $Root

    $world = [pscustomobject] @{
        Scenario   = $Scenario
        Root       = $manifest.Root
        Manifest   = $manifest
        MasterPort = Get-DirigentFreePort
        CliPort    = Get-DirigentFreePort
        HttpPort   = -1     # -1 disables the web server; 0 would fall back to 8877
        Master     = @( $manifest.Machines )[0]
        Processes  = @()
        Gui        = [bool] $Gui
    }

    if ( $WithHttp ) { $world.HttpPort = Get-DirigentFreePort }

    $windowStyle = 'Minimized'
    if ( $Visible ) { $windowStyle = 'Normal' }

    foreach ( $machine in @( $manifest.Machines ) )
    {
        $isMaster = ( $machine -eq $world.Master )

        $exe  = Get-DirigentTool -Project 'Dirigent.Agent.Console' -Exe 'Dirigent.Agent.exe'
        $mode = 'daemon'
        if ( $isMaster -and $Gui )
        {
            $exe  = Get-DirigentTool -Project 'Dirigent.Agent.WinForms' -Exe 'Dirigent.Agent.exe'
            $mode = 'trayGui'
        }

        $argList = @(
            '--machineId', $machine
            '--mode', $mode
            '--masterIp', '127.0.0.1'
            '--masterPort', $world.MasterPort
            '--sharedConfigFile', $manifest.SharedConfig
            '--agentStatusFolder', $manifest.AgentStatusFolder
            '--downloadFolder', $manifest.DownloadFolder
            '--logFile', ( Join-Path $manifest.LogFolder "$machine.log" )
            '--rootForRelativePaths', $manifest.Root
            '--localConfigFile', $manifest.LocalConfig
        )

        if ( $isMaster )
        {
            $argList += @( '--isMaster', '1', '--CLIPort', $world.CliPort, '--httpPort', $world.HttpPort )
        }
        else
        {
            $argList += @( '--isMaster', '0' )
        }

        Write-Verbose "starting $machine : $exe $($argList -join ' ')"

        $proc = Start-Process -FilePath $exe -ArgumentList $argList `
                    -WorkingDirectory $manifest.Root -WindowStyle $windowStyle -PassThru

        $world.Processes += [pscustomobject] @{ Machine = $machine; Process = $proc; IsMaster = $isMaster }
    }

    try
    {
        Wait-DirigentCondition -World $world -TimeoutSec $TimeoutSec `
            -Because "the master answers and every agent is connected" -Condition {
                param( $w )

                $lines = Invoke-DirigentCli -World $w -Command 'GetAllClientsState' -List -TimeoutSec 3
                foreach ( $machine in @( $w.Manifest.Machines ) )
                {
                    $connected = $lines | Where-Object { $_ -like "CLIENT:${machine}:1:*" }
                    if ( -not $connected ) { return $false }
                }
                return $true
            }
    }
    catch
    {
        # a world that never came up is worse than useless; take it down and say why
        $message = $_.Exception.Message
        Write-Host ( Get-DirigentWorldLog -World $world ) -ForegroundColor DarkGray
        Stop-DirigentWorld -World $world -KeepRoot
        throw "the world did not come up: $message`nIts folder was kept: $($world.Root)"
    }

    return $world
}

function Stop-DirigentWorld
{
    <#
    .SYNOPSIS
        Kills everything the world started - the agents, and any application they left running -
        and removes its folder.

    .DESCRIPTION
        Dirigent deliberately leaves managed applications running when an agent goes away, which is
        right in production and wrong here. The applications are found before the agents are killed,
        while they are still identifiable as their children.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] $World,
        [switch] $KeepRoot
    )

    # ask nicely first, so the agents kill their applications the way they normally would
    try   { Invoke-DirigentCli -World $World -Command 'KillAll' -TimeoutSec 5 | Out-Null }
    catch { Write-Verbose "KillAll did not get through: $($_.Exception.Message)" }

    $survivors = @( Get-DirigentWorldProcesses -World $World )

    foreach ( $entry in @( $World.Processes ) )
    {
        try
        {
            if ( -not $entry.Process.HasExited ) { $entry.Process.Kill() }
            $entry.Process.WaitForExit( 5000 ) | Out-Null
        }
        catch { Write-Verbose "killing $($entry.Machine) failed: $($_.Exception.Message)" }
    }

    foreach ( $processId in $survivors )
    {
        try   { Stop-Process -Id $processId -Force -ErrorAction Stop }
        catch { }   # already gone, which is the outcome we wanted anyway
    }

    if ( -not $KeepRoot )
    {
        # the processes need a moment to let go of their files
        for ( $attempt = 0; $attempt -lt 15; $attempt++ )
        {
            try
            {
                Remove-Item -Recurse -Force -Path $World.Root -ErrorAction Stop
                break
            }
            catch { Start-Sleep -Milliseconds 200 }
        }

        if ( Test-Path $World.Root )
        {
            Write-Warning "could not remove $($World.Root)"
        }
    }
}

function Get-DirigentWorldProcesses
{
    <#
    .SYNOPSIS
        The pids of the applications this world is running.

    .DESCRIPTION
        By parent process id, because the test application's executable is shared by every world and
        its command line does not always mention one. Only children of this world's agents count,
        so a run cannot kill anything belonging to another run - or to the real installation.
    #>
    param( [Parameter(Mandatory)] $World )

    $agentPids = @( @( $World.Processes ) | ForEach-Object { $_.Process.Id } )
    if ( -not $agentPids ) { return @() }

    $children = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
                    Where-Object { $agentPids -contains $_.ParentProcessId }

    # plus anything, at any depth, whose command line points into this world - a child of a child
    $root = $World.Root
    $rooted = Get-CimInstance Win32_Process -Filter "Name='Dirigent.TestApp.exe'" -ErrorAction SilentlyContinue |
                    Where-Object { $_.CommandLine -and $_.CommandLine.Contains( $root ) }

    return @( @( $children ) + @( $rooted ) | ForEach-Object { $_.ProcessId } | Sort-Object -Unique )
}
function Get-DirigentWorldLog
{
    <#  .SYNOPSIS The tail of every agent log, for a failure that needs explaining. #>
    param(
        [Parameter(Mandatory)] $World,
        [int] $Lines = 25
    )

    $text = ""
    foreach ( $log in ( Get-ChildItem -Path $World.Manifest.LogFolder -Filter '*.log' -ErrorAction SilentlyContinue ) )
    {
        $text += "`n--- $($log.Name) (last $Lines lines) ---`n"
        $text += ( Get-Content $log.FullName -Tail $Lines -ErrorAction SilentlyContinue ) -join "`n"
    }

    if ( -not $text ) { $text = "(no agent logs under $($World.Manifest.LogFolder))" }
    return $text
}

# ---- talking to it --------------------------------------------------------------------

function Invoke-DirigentCliExe
{
    <#
    .SYNOPSIS
        Runs the shipped Dirigent.CLI.exe against the world's master and returns what it printed.

    .DESCRIPTION
        Invoke-DirigentCli talks to the command port directly, which is what most tests want. This
        one goes through the executable, so that its own read loop and exit code are what is being
        tested - $LASTEXITCODE is left set for the caller to check.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] $World,
        [Parameter(Mandatory)][string] $Command
    )

    $exe = Get-DirigentTool -Project 'Dirigent.CLI' -Exe 'Dirigent.CLI.exe'

    $output = & $exe '--masterIp' '127.0.0.1' '--CLIPort' $World.CliPort $Command 2>&1

    return @( $output | ForEach-Object { "$_".Trim() } | Where-Object { $_ -ne '' } )
}

function Invoke-DirigentCli
{
    <#
    .SYNOPSIS
        Sends one text command to the master and returns its answer.

    .PARAMETER List
        The command answers with a list terminated by "END" - GetAllAppsState and friends. The
        terminator is not returned.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] $World,
        [Parameter(Mandatory)][string] $Command,
        [switch] $List,
        [int] $TimeoutSec = 10
    )

    $client = New-Object System.Net.Sockets.TcpClient
    try
    {
        $connect = $client.BeginConnect( '127.0.0.1', $World.CliPort, $null, $null )
        if ( -not $connect.AsyncWaitHandle.WaitOne( [TimeSpan]::FromSeconds( $TimeoutSec ) ) )
        {
            throw "no connection to the command port $($World.CliPort) within $TimeoutSec s"
        }
        $client.EndConnect( $connect )

        $stream = $client.GetStream()
        $stream.ReadTimeout = $TimeoutSec * 1000

        $reqId  = 'ps'
        $writer = New-Object System.IO.StreamWriter( $stream, ( New-Object System.Text.UTF8Encoding( $false ) ) )
        $writer.NewLine = "`n"
        $writer.AutoFlush = $true
        $writer.WriteLine( "[$reqId] $Command" )

        $reader = New-Object System.IO.StreamReader( $stream, [System.Text.Encoding]::UTF8 )

        $answers = @()
        while ( $true )
        {
            $raw = $reader.ReadLine()
            if ( $null -eq $raw ) { throw "the master closed the connection while answering '$Command'" }

            $line = $raw.Trim()
            if ( $line.StartsWith( "[$reqId] " ) ) { $line = $line.Substring( $reqId.Length + 3 ).Trim() }

            if ( $line.StartsWith( 'ERROR:' ) ) { throw "'$Command' was refused: $line" }

            if ( -not $List ) { return $line }
            if ( $line -eq 'END' ) { return $answers }
            $answers += $line
        }
    }
    finally
    {
        $client.Close()
    }
}

function Invoke-DirigentScript
{
    <#
    .SYNOPSIS
        Runs a Dirigent script and returns its result, deserialized.

    .DESCRIPTION
        StartScript with JSON arguments, then GetScriptState until the script is over - the whole
        non-GUI interface to the file subsystem, and to anything else scripted.

    .PARAMETER Arguments
        JSON, deserializable into the script's own argument class. Never free text.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] $World,
        [Parameter(Mandatory)][string] $Script,
        [string] $Arguments,
        [int] $TimeoutSec = 60
    )

    $guid = [Guid]::NewGuid().ToString()

    $request = "StartScript $guid $Script"
    if ( $Arguments ) { $request += " '$Arguments'" }

    $ack = Invoke-DirigentCli -World $World -Command $request
    if ( $ack -ne 'ACK' ) { throw "starting $Script was not acknowledged: '$ack'" }

    $state = $null
    Wait-DirigentCondition -World $World -TimeoutSec $TimeoutSec -Because "$Script finishes" -Condition {
        param( $w )
        $state = Get-DirigentScriptState -World $w -Guid $guid
        if ( $null -eq $state ) { return $false }
        return @( 'Finished', 'Failed', 'Cancelled' ) -contains $state.Status
    }

    # the closure above cannot write back to our scope, so read the final state once more
    $state = Get-DirigentScriptState -World $World -Guid $guid

    if ( $state.Status -ne 'Finished' )
    {
        throw "$Script ended as $($state.Status): $($state.Text) $($state.Data)"
    }

    if ( -not $state.Data ) { return $null }
    return ( $state.Data | ConvertFrom-Json )
}

function Get-DirigentScriptState
{
    <#  .SYNOPSIS The state of one script instance, or $null while the master does not know it. #>
    param(
        [Parameter(Mandatory)] $World,
        [Parameter(Mandatory)][string] $Guid
    )

    $line = Invoke-DirigentCli -World $World -Command "GetScriptState $Guid"
    if ( -not $line ) { return $null }

    $match = [regex]::Match( $line, '^SCRIPT:([0-9a-fA-F\-]{36}):(.*)$' )
    if ( -not $match.Success ) { throw "unexpected answer to GetScriptState: '$line'" }

    return ( $match.Groups[2].Value | ConvertFrom-Json )
}

function Wait-DirigentCondition
{
    <#
    .SYNOPSIS
        Waits for a condition to become true, and says what it was waiting for when it does not.

    .DESCRIPTION
        There is no virtual time in Dirigent, so a fixed sleep is a guess that fails on a loaded
        machine. Everything that waits, waits on a condition.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] $World,
        [Parameter(Mandatory)][scriptblock] $Condition,
        [Parameter(Mandatory)][string] $Because,
        [int] $TimeoutSec = 30,
        [int] $PollMs = 250
    )

    $deadline = [DateTime]::UtcNow.AddSeconds( $TimeoutSec )
    $lastError = $null

    while ( [DateTime]::UtcNow -lt $deadline )
    {
        try
        {
            if ( & $Condition $World ) { return }
            $lastError = $null
        }
        catch
        {
            # a world still starting up refuses connections; keep trying until the deadline
            $lastError = $_.Exception.Message
        }

        Start-Sleep -Milliseconds $PollMs
    }

    $detail = ""
    if ( $lastError ) { $detail = "`nlast error: $lastError" }
    throw "timed out after $TimeoutSec s waiting until $Because$detail"
}

function Wait-DirigentAppState
{
    <#  .SYNOPSIS Waits until an application's state flags match, e.g. "R" for running. #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] $World,
        [Parameter(Mandatory)][string] $App,
        [Parameter(Mandatory)][string] $Flags,

        # generous on purpose: this waits for a real process to start on a machine that may be
        # busy with a build or another suite, and a ceiling costs nothing when things are quick
        [int] $TimeoutSec = 90
    )

    Wait-DirigentCondition -World $World -TimeoutSec $TimeoutSec `
        -Because "$App reports '$Flags'" -Condition {
            param( $w )
            $line = Invoke-DirigentCli -World $w -Command "GetAppState $App"
            if ( -not $line ) { return $false }

            # APP:<idTuple>:<flags>:<exitCode>:<age>:...
            $parts = $line.Split( ':' )
            if ( $parts.Length -lt 3 ) { return $false }

            $actual = $parts[2]
            foreach ( $flag in $Flags.ToCharArray() )
            {
                if ( -not $actual.Contains( $flag ) ) { return $false }
            }
            return $true
        }
}

function Get-DirigentWorldSummary
{
    <#  .SYNOPSIS What to tell a human who wants to poke at a world by hand. #>
    param( [Parameter(Mandatory)] $World )

    $text = @"
Dirigent tier-2 world '$($World.Scenario)'
  folder        $($World.Root)
  shared config $($World.Manifest.SharedConfig)
  master port   $($World.MasterPort)
  command port  $($World.CliPort)
"@

    if ( $World.HttpPort -gt 0 ) { $text += "`n  web server    http://127.0.0.1:$($World.HttpPort)/" }

    $text += "`n  machines      " + ( @( $World.Manifest.Machines ) -join ', ' )
    $text += "`n  applications  " + ( ( @( $World.Manifest.Apps ) | ForEach-Object { $_.IdTuple } ) -join ', ' )
    $text += "`n  file nodes    " + ( @( $World.Manifest.VfsNodes ) -join ', ' )
    $text += @"

Talk to it:
  Invoke-DirigentCli -World `$w -Command 'GetAllAppsState' -List
  Invoke-DirigentCli -World `$w -Command 'StartApp $( @( $World.Manifest.Apps )[0].IdTuple )'
  Invoke-DirigentScript -World `$w -Script 'BuiltIns/DownloadZipped.cs' -Arguments '{"Node":{"Id":"logs.all"}}'
  Stop-DirigentWorld -World `$w
"@

    return $text
}

# ---- a test runner, so no module has to be installed ----------------------------------

$script:TestResults = @()
$script:TestFilter = $null

function Reset-TestResults
{
    $script:TestResults = @()
}

function Set-TestFilter
{
    <#  .SYNOPSIS Run only the tests whose name contains this. Empty runs all of them. #>
    param( [string] $Filter )
    $script:TestFilter = $Filter
}

function Test-Case
{
    <#  .SYNOPSIS Runs one test, reports it, and keeps going when it fails. #>
    param(
        [Parameter(Mandatory)][string] $Name,
        [Parameter(Mandatory)][scriptblock] $Body
    )

    if ( $script:TestFilter -and ( $Name -notlike "*$($script:TestFilter)*" ) )
    {
        return
    }

    Write-Host ( "  {0,-58}" -f $Name ) -NoNewline
    $watch = [Diagnostics.Stopwatch]::StartNew()

    try
    {
        & $Body
        Write-Host ( " PASS {0,5:0.0}s" -f $watch.Elapsed.TotalSeconds ) -ForegroundColor Green
        $script:TestResults += [pscustomobject] @{ Name = $Name; Passed = $true; Error = $null }
    }
    catch
    {
        Write-Host ( " FAIL {0,5:0.0}s" -f $watch.Elapsed.TotalSeconds ) -ForegroundColor Red
        Write-Host "        $($_.Exception.Message)" -ForegroundColor Red
        $script:TestResults += [pscustomobject] @{ Name = $Name; Passed = $false; Error = $_.Exception.Message }
    }
}

function Get-TestResults
{
    return $script:TestResults
}

function Expect-True
{
    param(
        [Parameter(Mandatory)] $Condition,
        [Parameter(Mandatory)][string] $Because
    )
    if ( -not $Condition ) { throw "expected $Because" }
}

function Expect-Equal
{
    param(
        $Expected,
        $Actual,
        [Parameter(Mandatory)][string] $Because
    )
    if ( $Expected -ne $Actual )
    {
        throw "expected $Because to be '$Expected', was '$Actual'"
    }
}

function Expect-Match
{
    param(
        [string[]] $Lines,
        [Parameter(Mandatory)][string] $Pattern,
        [Parameter(Mandatory)][string] $Because
    )

    $hit = $Lines | Where-Object { $_ -like $Pattern }
    if ( -not $hit )
    {
        throw "expected $Because (a line like '$Pattern'), got: $( $Lines -join ' | ' )"
    }
}

Export-ModuleMember -Function @(
    'Get-DirigentRepoRoot', 'Get-DirigentTool', 'Get-DirigentFreePort',
    'New-DirigentWorldFiles', 'Start-DirigentWorld', 'Stop-DirigentWorld',
    'Get-DirigentWorldProcesses', 'Get-DirigentWorldLog', 'Get-DirigentWorldSummary',
    'Invoke-DirigentCli', 'Invoke-DirigentCliExe', 'Invoke-DirigentScript', 'Get-DirigentScriptState',
    'Wait-DirigentCondition', 'Wait-DirigentAppState',
    'Reset-TestResults', 'Set-TestFilter', 'Test-Case', 'Get-TestResults',
    'Expect-True', 'Expect-Equal', 'Expect-Match'
)
