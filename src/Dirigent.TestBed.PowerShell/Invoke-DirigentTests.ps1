<#
.SYNOPSIS
    Runs the tier-2 tests, or brings up one world and leaves it standing for you to poke at.

.EXAMPLE
    .\Invoke-DirigentTests.ps1
    Runs every tier-2 test.

.EXAMPLE
    .\Invoke-DirigentTests.ps1 -Filter download
    Runs the tests whose name contains "download".

.EXAMPLE
    .\Invoke-DirigentTests.ps1 -KeepAlive -WithGui
    Generates the world, starts the master as a tray GUI plus the other agents, and leaves it all
    running with $w bound to it. This replaces the run_m1_gui_master.bat / run_m2_con.bat pair.

.NOTES
    Windows PowerShell 5.1, nothing to install.
#>
[CmdletBinding()]
param(
    # bring up one world and stop, instead of running the tests
    [switch] $KeepAlive,

    # with -KeepAlive: run the master as the WinForms tray GUI so you can watch and click
    [switch] $WithGui,

    # with -KeepAlive: which world to build
    [string] $Scenario = 'LoggingWorld',

    # with -KeepAlive: also start the web server
    [switch] $WithHttp,

    # run only the tests whose name contains this
    [string] $Filter,

    # build the solution first
    [switch] $Build,

    # leave the agent console windows visible
    [switch] $Visible
)

$ErrorActionPreference = 'Stop'

$modulePath = Join-Path $PSScriptRoot 'Dirigent.Testing.psm1'
Import-Module $modulePath -Force -DisableNameChecking

if ( $Build )
{
    $solution = Join-Path ( Get-DirigentRepoRoot ) 'src\Dirigent.NetCore.sln'
    Write-Host "building $solution ..." -ForegroundColor Cyan
    & dotnet build $solution -v q --nologo
    if ( $LASTEXITCODE -ne 0 ) { throw "the build failed" }
}

# ---- a world to keep ------------------------------------------------------------------

if ( $KeepAlive )
{
    $world = Start-DirigentWorld -Scenario $Scenario -Gui:$WithGui -WithHttp:$WithHttp -Visible:$Visible

    # bind it where an interactive session will find it
    Set-Variable -Name w -Value $world -Scope Global

    Write-Host ""
    Write-Host ( Get-DirigentWorldSummary -World $world ) -ForegroundColor Cyan
    Write-Host ""
    Write-Host "The world is bound to `$w in this session." -ForegroundColor Yellow
    Write-Host "Remember to run  Stop-DirigentWorld -World `$w  when you are done." -ForegroundColor Yellow
    return
}

# ---- or the tests ---------------------------------------------------------------------

Reset-TestResults

$specs = Get-ChildItem -Path ( Join-Path $PSScriptRoot 'Tests' ) -Filter '*.Tests.ps1' -File

Write-Host ""
Write-Host "Dirigent tier-2 tests" -ForegroundColor Cyan

$watch = [Diagnostics.Stopwatch]::StartNew()

foreach ( $spec in $specs )
{
    Write-Host ""
    Write-Host $spec.BaseName -ForegroundColor Cyan

    # the filter lives in the module, so a spec file needs to know nothing about it
    Set-TestFilter -Filter $Filter

    & $spec.FullName -Module $modulePath
}

$results = @( Get-TestResults )
$failed  = @( $results | Where-Object { -not $_.Passed } )

Write-Host ""
Write-Host ( "{0} tests, {1} failed, {2:0.0}s" -f $results.Count, $failed.Count, $watch.Elapsed.TotalSeconds ) `
    -ForegroundColor ( & { if ( $failed.Count -gt 0 ) { 'Red' } else { 'Green' } } )

foreach ( $failure in $failed )
{
    Write-Host "  FAILED: $($failure.Name)" -ForegroundColor Red
    Write-Host "          $($failure.Error)" -ForegroundColor DarkRed
}

# leave nothing of ours running, whatever the tests did
$strays = @( Get-CimInstance Win32_Process -Filter "Name='Dirigent.TestApp.exe' OR Name='Dirigent.Agent.exe'" `
                -ErrorAction SilentlyContinue |
                Where-Object { $_.CommandLine -and $_.CommandLine -like '*DirigentTier2*' } )

if ( $strays.Count -gt 0 )
{
    Write-Host ""
    Write-Warning "$($strays.Count) process(es) from a tier-2 world are still running; killing them:"
    foreach ( $stray in $strays )
    {
        Write-Host "  $($stray.ProcessId) $($stray.Name)" -ForegroundColor DarkYellow
        try { Stop-Process -Id $stray.ProcessId -Force -ErrorAction Stop } catch { }
    }
}

if ( $failed.Count -gt 0 ) { exit 1 }
exit 0
