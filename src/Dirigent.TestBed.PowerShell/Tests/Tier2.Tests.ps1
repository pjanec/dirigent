<#
.SYNOPSIS
    Tier-2 tests: real Dirigent processes on this machine, driven over the command-line interface.

.DESCRIPTION
    Deliberately small. Tier 1 covers behaviour in depth, in-process and fast; what only tier 2 can
    show is that the shipped executables start, find their configuration, host a master and an
    agent, answer on their remote-control surfaces, recover their applications after a crash, and
    collect files across processes for real.

    Run through Invoke-DirigentTests.ps1, which imports the module and reports the results.
#>

param(
    [Parameter(Mandatory)] $Module
)

# no -Force here: reloading the module would wipe the state the runner set up in it, including
# the name filter. The runner imports it fresh once per run.
if ( -not ( Get-Module -Name Dirigent.Testing ) )
{
    Import-Module $Module -DisableNameChecking
}

$ErrorActionPreference = 'Stop'

# ---- the hosting model ----------------------------------------------------------------

Test-Case 'the world comes up: master, two agents, applications defined' {
    $world = Start-DirigentWorld -Scenario LoggingWorld
    try
    {
        $clients = Invoke-DirigentCli -World $world -Command 'GetAllClientsState' -List
        Expect-Match -Lines $clients -Pattern 'CLIENT:m1:1:*' -Because 'm1 is connected'
        Expect-Match -Lines $clients -Pattern 'CLIENT:m2:1:*' -Because 'm2 is connected'

        $apps = Invoke-DirigentCli -World $world -Command 'GetAllAppsState' -List
        foreach ( $app in @( 'm1.camera', 'm1.tracker', 'm2.recorder' ) )
        {
            Expect-Match -Lines $apps -Pattern "APP:${app}:*" -Because "$app reached the master"
        }
    }
    finally { Stop-DirigentWorld -World $world }
}

Test-Case 'an application starts and stops on command, on the machine it belongs to' {
    $world = Start-DirigentWorld -Scenario LoggingWorld
    try
    {
        Invoke-DirigentCli -World $world -Command 'StartApp m2.recorder' | Out-Null
        Wait-DirigentAppState -World $world -App 'm2.recorder' -Flags 'R'

        # the other machine's applications were not touched. The flags are field 2 of
        # APP:<idTuple>:<flags>:... - matching the whole line would also hit the "r" in "camera"
        $others = @( Invoke-DirigentCli -World $world -Command 'GetAllAppsState' -List |
                    Where-Object { $_ -like 'APP:m1.*' -and $_.Split( ':' )[2].Contains( 'R' ) } )
        Expect-Equal -Expected 0 -Actual $others.Count `
            -Because "no m1 application started: $( $others -join ' | ' )"

        Invoke-DirigentCli -World $world -Command 'KillApp m2.recorder' | Out-Null
        Wait-DirigentCondition -World $world -Because 'm2.recorder stops running' -Condition {
            param( $w )
            $line = Invoke-DirigentCli -World $w -Command 'GetAppState m2.recorder'
            $flags = $line.Split( ':' )[2]
            return ( -not $flags.Contains( 'R' ) )
        }
    }
    finally { Stop-DirigentWorld -World $world }
}

# ---- post-crash recovery, which only a real process can show -------------------------

Test-Case 'an agent that is killed adopts its applications when it comes back' {
    $world = Start-DirigentWorld -Scenario LoggingWorld
    try
    {
        Invoke-DirigentCli -World $world -Command 'StartApp m2.recorder' | Out-Null
        Wait-DirigentAppState -World $world -App 'm2.recorder' -Flags 'R'

        $before = ( Invoke-DirigentCli -World $world -Command 'GetAppState m2.recorder' ).Split( ':' )[3]

        # the status file is what the recovery reads; it must exist while the agent runs
        $statusFiles = @( Get-ChildItem -Path $world.Manifest.AgentStatusFolder -File -ErrorAction SilentlyContinue )
        Expect-True -Condition ( $statusFiles.Count -gt 0 ) -Because 'the agent wrote a status file'

        # kill the agent hosting m2 the hard way, leaving its application running
        $agent = @( $world.Processes ) | Where-Object { $_.Machine -eq 'm2' }
        $agent.Process.Kill()
        $agent.Process.WaitForExit( 10000 ) | Out-Null

        Wait-DirigentCondition -World $world -Because 'the master notices m2 is gone' -Condition {
            param( $w )
            $lines = Invoke-DirigentCli -World $w -Command 'GetAllClientsState' -List
            $connected = $lines | Where-Object { $_ -like 'CLIENT:m2:1:*' }
            return ( -not $connected )
        }

        # the application is still running: Dirigent does not take applications down with an agent
        $stillThere = @( Get-DirigentWorldProcesses -World $world )
        Expect-True -Condition ( $stillThere.Count -gt 0 ) -Because 'the application outlived its agent'

        # bring the agent back and let it adopt what it finds
        $exe = Get-DirigentTool -Project 'Dirigent.Agent.Console' -Exe 'Dirigent.Agent.exe'
        $restarted = Start-Process -FilePath $exe -WorkingDirectory $world.Root -WindowStyle Minimized -PassThru `
            -ArgumentList @(
                '--machineId', 'm2', '--mode', 'daemon', '--isMaster', '0',
                '--masterIp', '127.0.0.1', '--masterPort', $world.MasterPort,
                '--sharedConfigFile', $world.Manifest.SharedConfig,
                '--agentStatusFolder', $world.Manifest.AgentStatusFolder,
                '--downloadFolder', $world.Manifest.DownloadFolder,
                '--logFile', ( Join-Path $world.Manifest.LogFolder 'm2-restarted.log' ),
                '--rootForRelativePaths', $world.Root
            )

        $world.Processes += [pscustomobject] @{ Machine = 'm2'; Process = $restarted; IsMaster = $false }

        Wait-DirigentAppState -World $world -App 'm2.recorder' -Flags 'R' -TimeoutSec 40

        $after = ( Invoke-DirigentCli -World $world -Command 'GetAppState m2.recorder' ).Split( ':' )[3]
        Expect-Equal -Expected $before -Actual $after `
            -Because 'the adopted application is the same process, not a fresh one'
    }
    finally { Stop-DirigentWorld -World $world }
}

# ---- the file subsystem, end to end across processes ---------------------------------

Test-Case 'the declared file nodes can be listed' {
    $world = Start-DirigentWorld -Scenario LoggingWorld
    try
    {
        $result = Invoke-DirigentScript -World $world -Script 'BuiltIns/ListVfsNodes.cs' `
                    -Arguments '{"Filter":{"Id":"log"}}'

        $found = @( $result.Nodes | ForEach-Object { "$($_.MachineId).$($_.AppId)" } | Sort-Object )
        Expect-Equal -Expected 'm1.camera, m1.tracker, m2.recorder' -Actual ( $found -join ', ' ) `
            -Because 'every application exposes its logs'
    }
    finally { Stop-DirigentWorld -World $world }
}

Test-Case 'a file node on another machine resolves to files that exist' {
    $world = Start-DirigentWorld -Scenario LoggingWorld
    try
    {
        Invoke-DirigentCli -World $world -Command 'StartApp m2.recorder' | Out-Null
        Wait-DirigentAppState -World $world -App 'm2.recorder' -Flags 'R'

        Wait-DirigentCondition -World $world -Because 'the recorder has written its log' -Condition {
            param( $w )
            $logs = @( $w.Manifest.Apps ) | Where-Object { $_.IdTuple -eq 'm2.recorder' }
            return ( Test-Path ( Join-Path $logs.LogsDir 'app.log' ) )
        }

        $result = Invoke-DirigentScript -World $world -Script 'BuiltIns/ResolveVfsPath.cs' `
                    -Arguments '{"Node":{"Id":"log","MachineId":"m2","AppId":"recorder"},"IncludeContent":true}'

        $paths = @()
        function Add-Paths( $node )
        {
            if ( $node.Path ) { $script:collected += $node.Path }
            if ( $node.Children ) { foreach ( $child in $node.Children ) { Add-Paths $child } }
        }
        $script:collected = @()
        Add-Paths $result.VfsNode
        $paths = $script:collected

        Expect-True -Condition ( @( $paths | Where-Object { $_ -like '*app.log' } ).Count -gt 0 ) `
            -Because "the live log is among the resolved files: $( $paths -join ', ' )"
        Expect-True -Condition ( @( $paths | Where-Object { $_ -like '*ancient.log' } ).Count -eq 0 ) `
            -Because 'the nine-day-old file was filtered out'
        Expect-True -Condition ( @( $paths | Where-Object { -not ( Test-Path $_ ) } ).Count -eq 0 ) `
            -Because 'every resolved path exists'
    }
    finally { Stop-DirigentWorld -World $world }
}

Test-Case 'logs from both machines are collected into one archive' {
    $world = Start-DirigentWorld -Scenario LoggingWorld
    try
    {
        foreach ( $app in @( 'm1.camera', 'm1.tracker', 'm2.recorder' ) )
        {
            Invoke-DirigentCli -World $world -Command "StartApp $app" | Out-Null
        }
        foreach ( $app in @( 'm1.camera', 'm1.tracker', 'm2.recorder' ) )
        {
            Wait-DirigentAppState -World $world -App $app -Flags 'R'
        }

        Wait-DirigentCondition -World $world -Because 'every application has written its log' -Condition {
            param( $w )
            foreach ( $app in @( $w.Manifest.Apps ) )
            {
                if ( -not ( Test-Path ( Join-Path $app.LogsDir 'app.log' ) ) ) { return $false }
            }
            return $true
        }

        $result = Invoke-DirigentScript -World $world -Script 'BuiltIns/DownloadZipped.cs' `
                    -Arguments '{"Node":{"Id":"logs.all"}}'

        Expect-Equal -Expected 0 -Actual @( $result.Errors ).Count `
            -Because "the download reported no errors: $( @( $result.Errors ) -join ' | ' )"
        Expect-Equal -Expected 'm1, m2' -Actual ( ( @( $result.Machines ) | Sort-Object ) -join ', ' ) `
            -Because 'both machines contributed'

        $archive = @( $result.Files )[0]
        Expect-True -Condition ( Test-Path $archive ) -Because "the archive exists: $archive"

        # look inside: a folder per machine, a folder per application, and nothing stale
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip = [System.IO.Compression.ZipFile]::OpenRead( $archive )
        try
        {
            $entries = @( $zip.Entries | ForEach-Object { $_.FullName } )
        }
        finally { $zip.Dispose() }

        Expect-Match -Lines $entries -Pattern 'm1/*camera/app.log' -Because "the camera's log is in there"
        Expect-Match -Lines $entries -Pattern 'm2/*recorder/app.log' -Because "the recorder's log is in there"
        Expect-True -Condition ( @( $entries | Where-Object { $_ -like '*ancient.log' } ).Count -eq 0 ) `
            -Because "the nine-day-old files were left behind: $( $entries -join ', ' )"

        # the staging folder the machines uploaded their parts to is gone again
        $leftovers = @( Get-ChildItem -Path $world.Manifest.DownloadFolder -Directory )
        Expect-Equal -Expected 0 -Actual $leftovers.Count `
            -Because "no folder is left in the download folder: $( ( $leftovers | ForEach-Object { $_.Name } ) -join ', ' )"
    }
    finally { Stop-DirigentWorld -World $world }
}

# ---- the shipped command line client --------------------------------------------------

Test-Case 'Dirigent.CLI.exe answers and exits 0, as it always has' {
    $world = Start-DirigentWorld -Scenario LoggingWorld
    try
    {
        # a listing: the lines are printed and the END ends it
        $lines = Invoke-DirigentCliExe -World $world -Command 'GetAllAppsState'
        $code = $LASTEXITCODE
        Expect-Equal -Expected 0 -Actual $code -Because "the exe reported success: $( $lines -join ' | ' )"
        Expect-Match -Lines $lines -Pattern 'APP:m1.camera:*' -Because 'it printed what the master said'
        Expect-Match -Lines $lines -Pattern 'END' -Because 'including the terminator'

        # a simple command: one ACK
        $lines = Invoke-DirigentCliExe -World $world -Command 'StartApp m1.camera'
        $code = $LASTEXITCODE
        Expect-Equal -Expected 0 -Actual $code -Because "starting an app succeeded: $( $lines -join ' | ' )"
        Expect-Match -Lines $lines -Pattern 'ACK' -Because 'the master acknowledged it'

        # and a command nobody knows is a failure, with the reason printed
        $lines = Invoke-DirigentCliExe -World $world -Command 'NoSuchCommand'
        $code = $LASTEXITCODE
        Expect-Equal -Expected 4 -Actual $code -Because "an error is exit code 4: $( $lines -join ' | ' )"
        Expect-Match -Lines $lines -Pattern 'ERROR*' -Because 'and it says what was wrong'
    }
    finally { Stop-DirigentWorld -World $world }
}

Test-Case 'Dirigent.CLI.exe waits for a script to finish before it returns' {
    # What a plan step or a batch file needs: the exe must not report success at the ACK, which says
    # only that the master accepted the command. It waits for the END that WaitForScript sends when
    # the script is really over.
    $world = Start-DirigentWorld -Scenario WaitingWorld
    try
    {
        # a script waiting for a machine no agent serves - it cannot finish on its own. The relaxed
        # JSON goes in single quotes, doubled inside, which is the form that survives both parsers
        # (see docs/CLI.md).
        $instance = [Guid]::NewGuid().ToString()
        $hanging = "StartScript $instance BuiltIns/RunPlanWhenMachinesOnline.cs '{Plan:''never''}'" +
                   " ; WaitForScript $instance timeout=4"

        $lines = $null
        $elapsed = Measure-Command { $script:lines = Invoke-DirigentCliExe -World $world -Command $hanging }
        $code = $LASTEXITCODE

        Expect-True -Condition ( $elapsed.TotalSeconds -ge 3 ) `
            -Because ( "it waited for the script rather than returning at the ACK: " +
                       "$( [int]$elapsed.TotalSeconds ) s, answered $( $script:lines -join ' | ' )" )
        Expect-Equal -Expected 4 -Actual $code `
            -Because "the wait timed out, which is a failed command: $( $script:lines -join ' | ' )"
        Expect-Match -Lines $script:lines -Pattern 'ERROR*did not finish*' `
            -Because 'and the reason is the timeout'

        # and a script that does finish: success, once it is over
        $instance = [Guid]::NewGuid().ToString()
        $lines = Invoke-DirigentCliExe -World $world -Command `
            "StartScript $instance BuiltIns/ListVfsNodes.cs ; WaitForScript $instance timeout=30"
        $code = $LASTEXITCODE

        Expect-Equal -Expected 0 -Actual $code -Because "the script finished: $( $lines -join ' | ' )"
        Expect-Match -Lines $lines -Pattern 'END' -Because 'the wait ended with END'

        $state = Invoke-DirigentCli -World $world -Command "GetScriptState $instance"
        Expect-Match -Lines @( $state ) -Pattern '*Finished*' `
            -Because "it really had finished by the time the exe returned: $state"
    }
    finally { Stop-DirigentWorld -World $world }
}

# ---- the other remote-control surface -------------------------------------------------

Test-Case 'the web server answers the same commands' {
    $world = Start-DirigentWorld -Scenario LoggingWorld -WithHttp
    try
    {
        $url = "http://127.0.0.1:$($world.HttpPort)/api/cli"

        $response = Invoke-RestMethod -Uri $url -Method Post -Body 'GetAllAppsState' -TimeoutSec 20
        $text = ( $response | Out-String )

        Expect-True -Condition ( $text -like '*m1.camera*' ) `
            -Because "the applications came back over HTTP: $text"
    }
    finally { Stop-DirigentWorld -World $world }
}

# ---- and nothing left behind ---------------------------------------------------------

Test-Case 'a run leaves no processes and no folders behind' {
    $world = Start-DirigentWorld -Scenario LoggingWorld
    $root = $world.Root

    Invoke-DirigentCli -World $world -Command 'StartApp m1.camera' | Out-Null
    Wait-DirigentAppState -World $world -App 'm1.camera' -Flags 'R'

    Stop-DirigentWorld -World $world

    Expect-True -Condition ( -not ( Test-Path $root ) ) -Because "the world's folder was removed: $root"

    $strays = @( Get-CimInstance Win32_Process -Filter "Name='Dirigent.TestApp.exe'" -ErrorAction SilentlyContinue |
                    Where-Object { $_.CommandLine -and $_.CommandLine.Contains( $root ) } )
    Expect-Equal -Expected 0 -Actual $strays.Count -Because 'no application outlived the world'
}
