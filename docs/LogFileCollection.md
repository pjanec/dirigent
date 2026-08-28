# Collecting Log Files from Multiple Machines

This guide explains how to configure Dirigent to easily collect log files from different applications running on multiple machines/stations.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Configuration Approaches](#configuration-approaches)
3. [Step-by-Step Examples](#step-by-step-examples)
4. [Advanced Patterns](#advanced-patterns)
5. [Complete Examples](#complete-examples)

## Prerequisites

### 1. Configure Machine Shares

Before you can access files from remote machines, you must configure file shares for each machine. Shares enable Dirigent to convert local file paths to UNC paths for network access.

**Example Machine Configuration:**

```xml
<Machine Name="station1" IP="192.168.1.100">
    <!-- File shares for UNC path conversion -->
    <!-- Name: Share name used in UNC paths (\\IP\Name\path) -->
    <!-- Path: Local path on the machine (must be absolute) -->
    <Share Name="C" Path="C:\"/>
    <Share Name="D" Path="D:\"/>
    <Share Name="Logs" Path="C:\Logs"/>  <!-- Optional: dedicated share for logs -->
</Machine>

<Machine Name="station2" IP="192.168.1.101">
    <Share Name="C" Path="C:\"/>
    <Share Name="E" Path="E:\"/>
    <Share Name="AppLogs" Path="E:\Applications\Logs"/>
</Machine>
```

**Important Notes:**
- Share `Path` must be an absolute path
- Share `Name` is used in UNC paths: `\\{MachineIP}\{ShareName}\{relative_path}`
- Windows file shares must be accessible without additional credentials (or credentials must be cached)
- Dirigent uses the first matching share (longest path match)

## Configuration Approaches

There are three main approaches to configure log file collection:

1. **App-Scoped**: Define log files within each `<App>` section
2. **Machine-Scoped**: Define log files within each `<Machine>` section
3. **Global-Scoped**: Define log files at the top level, then reference them

Each approach has its use cases:

- **App-Scoped**: Best when each application has its own log files
- **Machine-Scoped**: Best when collecting all logs from a machine regardless of app
- **Global-Scoped**: Best for centralized log file definitions

## Step-by-Step Examples

### Approach 1: App-Scoped Log Files

Define log files within each application, then collect them using a FilePackage.

#### Step 1: Define Log Files in Each App

```xml
<Plan Name="Production">
    <App AppIdTuple="station1.webapp">
        <File Id="webapp_log" Path="C:\Logs\webapp.log"/>
        <File Id="webapp_error_log" Path="C:\Logs\webapp_errors.log"/>
        <Folder Id="webapp_log_folder" Path="C:\Logs\webapp" Mask="*.log"/>
    </App>
    
    <App AppIdTuple="station1.database">
        <File Id="db_log" Path="C:\Logs\database.log"/>
        <Folder Id="db_log_folder" Path="C:\Logs\database" Mask="*.log"/>
    </App>
    
    <App AppIdTuple="station2.webapp">
        <File Id="webapp_log" Path="E:\Applications\Logs\webapp.log"/>
        <Folder Id="webapp_log_folder" Path="E:\Applications\Logs\webapp" Mask="*.log"/>
    </App>
</Plan>
```

#### Step 2: Create a FilePackage to Collect All Logs

```xml
<!-- Global FilePackage to collect logs from all stations -->
<FilePackage Id="all_production_logs">
    <!-- Collect webapp logs from all machines -->
    <FileRef Id="webapp_log" MachineId="" AppId=""/>
    <FileRef Id="webapp_error_log" MachineId="" AppId=""/>
    
    <!-- Collect database logs from all machines -->
    <FileRef Id="db_log" MachineId="" AppId=""/>
    
    <!-- Collect log folders from all machines -->
    <FileRef Id="webapp_log_folder" MachineId="" AppId=""/>
    <FileRef Id="db_log_folder" MachineId="" AppId=""/>
</FilePackage>
```

#### Step 3: Collect Logs from Specific Stations Only

```xml
<FilePackage Id="station1_logs_only">
    <!-- Only collect from station1 -->
    <FileRef Id="webapp_log" MachineId="station1" AppId=""/>
    <FileRef Id="webapp_error_log" MachineId="station1" AppId=""/>
    <FileRef Id="db_log" MachineId="station1" AppId=""/>
</FilePackage>
```

### Approach 2: Machine-Scoped Log Files

Define log files at the machine level, useful for collecting all logs from a machine.

#### Step 1: Define Log Files in Machine Sections

```xml
<Machine Name="station1" IP="192.168.1.100">
    <Share Name="C" Path="C:\"/>
    <Share Name="Logs" Path="C:\Logs"/>
    
    <!-- Collect newest log files (up to 10) -->
    <File Id="newest_logs" Path="C:\Logs" Mask="*.log" Filter="Newest" MaxFiles="10">
        <Script Title="Download zipped" Name="BuiltIns/DownloadZipped.cs"/>
    </File>
    
    <!-- Collect all logs from a specific folder -->
    <Folder Id="all_logs" Path="C:\Logs" Mask="**/*.log">
        <Tool Title="Open in Explorer" Name="WinExplorer"/>
    </Folder>
    
    <!-- Collect recent logs (max 5, max 1 hour old) -->
    <File Id="recent_logs" Path="C:\Logs" Mask="*.log" Filter="Newest" MaxFiles="5" MaxSeconds="3600">
        <Script Title="Download zipped" Name="BuiltIns/DownloadZipped.cs"/>
    </File>
</Machine>

<Machine Name="station2" IP="192.168.1.101">
    <Share Name="E" Path="E:\"/>
    <Share Name="AppLogs" Path="E:\Applications\Logs"/>
    
    <File Id="newest_logs" Path="E:\Applications\Logs" Mask="*.log" Filter="Newest" MaxFiles="10"/>
    <Folder Id="all_logs" Path="E:\Applications\Logs" Mask="**/*.log"/>
</Machine>
```

#### Step 2: Create a FilePackage to Collect from Multiple Machines

```xml
<FilePackage Id="all_station_logs">
    <!-- Collect newest logs from all stations -->
    <FileRef Id="newest_logs" MachineId="station1" AppId=""/>
    <FileRef Id="newest_logs" MachineId="station2" AppId=""/>
    
    <!-- Or use empty MachineId to match all machines -->
    <FileRef Id="newest_logs" MachineId="" AppId=""/>
</FilePackage>
```

### Approach 3: Global Log File Definitions

Define log files globally, then reference them selectively.

#### Step 1: Define Global Log Files

```xml
<!-- Global log file definitions -->
<File Id="webapp_log_station1" MachineId="station1" Path="C:\Logs\webapp.log"/>
<File Id="webapp_log_station2" MachineId="station2" Path="E:\Applications\Logs\webapp.log"/>
<File Id="db_log_station1" MachineId="station1" Path="C:\Logs\database.log"/>
<File Id="db_log_station2" MachineId="station2" Path="E:\Applications\Logs\database.log"/>
```

#### Step 2: Create Selective FilePackages

```xml
<!-- Collect only webapp logs from all stations -->
<FilePackage Id="webapp_logs_all_stations">
    <FileRef Id="webapp_log_station1" MachineId="station1" AppId=""/>
    <FileRef Id="webapp_log_station2" MachineId="station2" AppId=""/>
</FilePackage>

<!-- Collect only database logs -->
<FilePackage Id="db_logs_all_stations">
    <FileRef Id="db_log_station1" MachineId="station1" AppId=""/>
    <FileRef Id="db_log_station2" MachineId="station2" AppId=""/>
</FilePackage>
```

## Advanced Patterns

### Using Wildcards in FileRef

You can use wildcard patterns to match multiple files by ID:

```xml
<FilePackage Id="all_newest_logs">
    <!-- Match all files with IDs starting with "newest" -->
    <FileRef Id="newest*" MachineId="" AppId=""/>
    
    <!-- Match all log files from station1 -->
    <FileRef Id="*_log" MachineId="station1" AppId=""/>
</FilePackage>
```

### Using Empty Values for Matching

Empty `MachineId` or `AppId` in `FileRef` matches any value:

```xml
<FilePackage Id="flexible_collection">
    <!-- Match "app_log" from any machine, any app -->
    <FileRef Id="app_log" MachineId="" AppId=""/>
    
    <!-- Match "app_log" from station1, any app -->
    <FileRef Id="app_log" MachineId="station1" AppId=""/>
    
    <!-- Match "app_log" from any machine, but only "webapp" app -->
    <FileRef Id="app_log" MachineId="" AppId="webapp"/>
</FilePackage>
```

### Using Folders with Glob Patterns

Collect multiple files using folder definitions with glob-style masks:

```xml
<FilePackage Id="comprehensive_logs">
    <!-- Collect all .log files recursively from station1 -->
    <Folder Id="station1_all_logs" MachineId="station1" Path="C:\Logs" Mask="**/*.log"/>
    
    <!-- Collect specific log types -->
    <Folder Id="error_logs" MachineId="station1" Path="C:\Logs" Mask="**/*error*.log"/>
    <Folder Id="info_logs" MachineId="station1" Path="C:\Logs" Mask="**/*info*.log"/>
    
    <!-- Collect logs from multiple subdirectories -->
    <Folder Id="app_logs" MachineId="station1" Path="C:\Logs" Mask="**/app*/**/*.{log,txt}"/>
</FilePackage>
```

### Using Filters for Recent Files

Use the `Filter="Newest"` attribute to collect only the most recent files:

```xml
<Machine Name="station1" IP="192.168.1.100">
    <Share Name="C" Path="C:\"/>
    
    <!-- Single newest log file -->
    <File Id="latest_log" Path="C:\Logs" Mask="*.log" Filter="Newest"/>
    
    <!-- Top 5 newest log files -->
    <File Id="top5_logs" Path="C:\Logs" Mask="*.log" Filter="Newest" MaxFiles="5"/>
    
    <!-- Newest logs from last hour (max 10 files) -->
    <File Id="recent_logs" Path="C:\Logs" Mask="*.log" Filter="Newest" MaxFiles="10" MaxSeconds="3600"/>
</Machine>
```

### Organizing with Virtual Folders

Use `VFolder` to organize collected logs into a hierarchical structure:

```xml
<FilePackage Id="organized_logs">
    <VFolder Id="station1_logs">
        <FileRef Id="webapp_log" MachineId="station1" AppId=""/>
        <FileRef Id="db_log" MachineId="station1" AppId=""/>
    </VFolder>
    
    <VFolder Id="station2_logs">
        <FileRef Id="webapp_log" MachineId="station2" AppId=""/>
        <FileRef Id="db_log" MachineId="station2" AppId=""/>
    </VFolder>
    
    <VFolder Id="error_logs">
        <FileRef Id="webapp_error_log" MachineId="" AppId=""/>
    </VFolder>
</FilePackage>
```

## Complete Examples

### Example 1: Collecting Logs from a Subset of Apps on Specific Stations

**Scenario**: Collect logs from `webapp` and `database` apps running on `station1` and `station2`, but not from other apps.

```xml
<Shared>
    <!-- Machine definitions with shares -->
    <Machine Name="station1" IP="192.168.1.100">
        <Share Name="C" Path="C:\"/>
        <Share Name="Logs" Path="C:\Logs"/>
    </Machine>
    
    <Machine Name="station2" IP="192.168.1.101">
        <Share Name="E" Path="E:\"/>
        <Share Name="AppLogs" Path="E:\Applications\Logs"/>
    </Machine>
    
    <!-- App definitions with log files -->
    <Plan Name="Production">
        <App AppIdTuple="station1.webapp">
            <File Id="webapp_log" Path="C:\Logs\webapp.log"/>
            <File Id="webapp_error_log" Path="C:\Logs\webapp_errors.log"/>
        </App>
        
        <App AppIdTuple="station1.database">
            <File Id="db_log" Path="C:\Logs\database.log"/>
        </App>
        
        <App AppIdTuple="station1.other">
            <!-- This app's logs are NOT collected -->
            <File Id="other_log" Path="C:\Logs\other.log"/>
        </App>
        
        <App AppIdTuple="station2.webapp">
            <File Id="webapp_log" Path="E:\Applications\Logs\webapp.log"/>
            <File Id="webapp_error_log" Path="E:\Applications\Logs\webapp_errors.log"/>
        </App>
        
        <App AppIdTuple="station2.database">
            <File Id="db_log" Path="E:\Applications\Logs\database.log"/>
        </App>
    </Plan>
    
    <!-- FilePackage to collect only webapp and database logs from station1 and station2 -->
    <FilePackage Id="selected_app_logs">
        <!-- Webapp logs from both stations -->
        <FileRef Id="webapp_log" MachineId="station1" AppId="webapp"/>
        <FileRef Id="webapp_log" MachineId="station2" AppId="webapp"/>
        <FileRef Id="webapp_error_log" MachineId="station1" AppId="webapp"/>
        <FileRef Id="webapp_error_log" MachineId="station2" AppId="webapp"/>
        
        <!-- Database logs from both stations -->
        <FileRef Id="db_log" MachineId="station1" AppId="database"/>
        <FileRef Id="db_log" MachineId="station2" AppId="database"/>
    </FilePackage>
</Shared>
```

### Example 2: Collecting All Logs from Specific Stations

**Scenario**: Collect all log files from `station1` and `station2`, regardless of which app created them.

```xml
<Shared>
    <Machine Name="station1" IP="192.168.1.100">
        <Share Name="C" Path="C:\"/>
        <Share Name="Logs" Path="C:\Logs"/>
        
        <!-- Collect all logs from this machine -->
        <Folder Id="all_machine_logs" Path="C:\Logs" Mask="**/*.log">
            <Tool Title="Open in Explorer" Name="WinExplorer"/>
        </Folder>
        
        <!-- Or collect newest logs only -->
        <File Id="newest_logs" Path="C:\Logs" Mask="*.log" Filter="Newest" MaxFiles="20">
            <Script Title="Download zipped" Name="BuiltIns/DownloadZipped.cs"/>
        </File>
    </Machine>
    
    <Machine Name="station2" IP="192.168.1.101">
        <Share Name="E" Path="E:\"/>
        <Share Name="AppLogs" Path="E:\Applications\Logs"/>
        
        <Folder Id="all_machine_logs" Path="E:\Applications\Logs" Mask="**/*.log"/>
        <File Id="newest_logs" Path="E:\Applications\Logs" Mask="*.log" Filter="Newest" MaxFiles="20">
            <Script Title="Download zipped" Name="BuiltIns/DownloadZipped.cs"/>
        </File>
    </Machine>
    
    <!-- Collect all logs from both stations -->
    <FilePackage Id="all_station_logs">
        <FileRef Id="all_machine_logs" MachineId="station1" AppId=""/>
        <FileRef Id="all_machine_logs" MachineId="station2" AppId=""/>
    </FilePackage>
    
    <!-- Or collect only newest logs -->
    <FilePackage Id="newest_station_logs">
        <FileRef Id="newest_logs" MachineId="station1" AppId=""/>
        <FileRef Id="newest_logs" MachineId="station2" AppId=""/>
    </FilePackage>
</Shared>
```

### Example 3: Collecting Logs with Time-Based Filtering

**Scenario**: Collect only recent log files (from the last hour) from specific apps on multiple stations.

```xml
<Shared>
    <Machine Name="station1" IP="192.168.1.100">
        <Share Name="C" Path="C:\"/>
        <Share Name="Logs" Path="C:\Logs"/>
        
        <!-- Recent webapp logs (last hour, max 10 files) -->
        <File Id="recent_webapp_logs" Path="C:\Logs\webapp" Mask="*.log" 
              Filter="Newest" MaxFiles="10" MaxSeconds="3600"/>
        
        <!-- Recent database logs -->
        <File Id="recent_db_logs" Path="C:\Logs\database" Mask="*.log" 
              Filter="Newest" MaxFiles="10" MaxSeconds="3600"/>
    </Machine>
    
    <Machine Name="station2" IP="192.168.1.101">
        <Share Name="E" Path="E:\"/>
        <Share Name="AppLogs" Path="E:\Applications\Logs"/>
        
        <File Id="recent_webapp_logs" Path="E:\Applications\Logs\webapp" Mask="*.log" 
              Filter="Newest" MaxFiles="10" MaxSeconds="3600"/>
        
        <File Id="recent_db_logs" Path="E:\Applications\Logs\database" Mask="*.log" 
              Filter="Newest" MaxFiles="10" MaxSeconds="3600"/>
    </Machine>
    
    <!-- Collect recent logs from both stations -->
    <FilePackage Id="recent_logs_collection">
        <VFolder Id="station1_recent">
            <FileRef Id="recent_webapp_logs" MachineId="station1" AppId=""/>
            <FileRef Id="recent_db_logs" MachineId="station1" AppId=""/>
        </VFolder>
        
        <VFolder Id="station2_recent">
            <FileRef Id="recent_webapp_logs" MachineId="station2" AppId=""/>
            <FileRef Id="recent_db_logs" MachineId="station2" AppId=""/>
        </VFolder>
    </FilePackage>
</Shared>
```

### Example 4: Using Wildcards for Flexible Collection

**Scenario**: Collect all logs matching a pattern from multiple stations.

```xml
<Shared>
    <Machine Name="station1" IP="192.168.1.100">
        <Share Name="C" Path="C:\"/>
        <Share Name="Logs" Path="C:\Logs"/>
        
        <!-- Define logs with consistent naming pattern -->
        <File Id="app1_log" Path="C:\Logs\app1.log"/>
        <File Id="app2_log" Path="C:\Logs\app2.log"/>
        <File Id="app3_log" Path="C:\Logs\app3.log"/>
    </Machine>
    
    <Machine Name="station2" IP="192.168.1.101">
        <Share Name="E" Path="E:\"/>
        <Share Name="AppLogs" Path="E:\Applications\Logs"/>
        
        <File Id="app1_log" Path="E:\Applications\Logs\app1.log"/>
        <File Id="app2_log" Path="E:\Applications\Logs\app2.log"/>
    </Machine>
    
    <!-- Collect all app logs using wildcard pattern -->
    <FilePackage Id="all_app_logs">
        <!-- This will match all files with IDs matching "app*_log" pattern -->
        <FileRef Id="app*_log" MachineId="" AppId=""/>
    </FilePackage>
    
    <!-- Or collect from specific stations only -->
    <FilePackage Id="station1_app_logs">
        <FileRef Id="app*_log" MachineId="station1" AppId=""/>
    </FilePackage>
</Shared>
```

## Tips and Best Practices

1. **Consistent Naming**: Use consistent ID naming patterns (e.g., `{appname}_log`) to make wildcard matching easier.

2. **Use Filters**: Use `Filter="Newest"` with `MaxFiles` and `MaxSeconds` to avoid collecting too many old log files.

3. **Organize with VFolders**: Use virtual folders to organize collected logs by station, app, or log type.

4. **Test Share Configuration**: Ensure file shares are properly configured and accessible before defining log file paths.

5. **Use Relative Paths with App Context**: When defining app-scoped files, you can use relative paths that will be resolved relative to the app's startup directory or use variables like `%APP_STARTUPDIR%`.

6. **Combine Approaches**: You can mix app-scoped, machine-scoped, and global definitions as needed for your use case.

## Troubleshooting

### Files Not Found

- Verify that file shares are correctly configured for each machine
- Check that the paths exist on the target machines
- Ensure UNC paths are accessible (test with `\\{IP}\{ShareName}\{path}`)

### Empty FilePackage

- Check that `FileRef` IDs match the actual file IDs
- Verify that `MachineId` and `AppId` match correctly (empty values match any)
- Ensure files are defined before they are referenced

### UNC Path Issues

- Verify file shares are accessible without additional credentials
- Check Windows file sharing permissions
- Ensure the share `Path` matches the beginning of your file paths

## See Also

- [Files.md](Files.md) - General file and package documentation
- [SharedConfig.md](SharedConfig.md) - Shared configuration reference
- [Apps.md](Apps.md) - Application configuration

