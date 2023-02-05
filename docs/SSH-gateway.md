# SSH gateway

GUI usually connects to Master directly if all runs in same local network.

GUI can also connect to a Master running on different network via a SSH gateway.

The list of available gateways is read from GatewayConfig.xml. For each gateway the IP address, user name and password is required.

GUI establishes a SSH connection to the gateway computer where the SSH server service is running.

GUI sets up SSH port tunnel to reach the Master by connection to 127.0.0.1:new_port.

## Port forwarding

For each machine there is defined a list of ports to be forwarded. This allows tools like VNC to connect to the target machine behind the gateway.

For each service defined for a machine one port tunnel is set up.

When a tool is started in the context of a machine, the following set of network service-related variables is available to be expanded as part of tool's command line:

| Variable | Description                                                  |
| -------- | ------------------------------------------------------------ |
| SVC_IP   | IP address of the machine where the tool can connect. If SSH tunnel is opened, this is always local host 127.0.0.1 |
| SVC_PORT | Port for the network service the app is going to use. This port is forwarded to the target machine is SSH tunnel is active. |

## Machine info caching

When GUI connects to the gateway for the first time, it needs to knows just the IP/port of the Dirigent Master. Once connected, it receives from the Master the list of all machines and their services. The port forwarding is then recalculated according to the up-to-date info. Retrieved machines & services are stored in a cache for the next time.

## Accessing files behind SSH proxy

GUI can edit files from any agent computer even if they are not directly accessible using windows file sharing.

### Download - Edit - Upload

The file to edit is first downloaded from its home machine via sftp to a temporary file on the computer where the GUI is running. Editing tool is launched, opening the temporary file. If the file gets modified during the life span of the editing tool (i.e. saved by the tool), the content of the temporary file is uploaded back to the home machine.

The file modification detection only works until the editing tool terminates. This does not play well with single instance editors that are already running before user wants to edit the file. This feature requires the editor to be started in separate instance for each of the files to edit.

### Symbolic links on the gateway

On connect, GUI creates a set of symbolic links on the gateway computer (Windows only). For each file share on each machine one link is created. The link target is an UNC path leading to file share of the agent machines. Via this link the GUI can access file on machines behind the gateway, providing the gateway user has proper access rights to the file shares.

Links are created by running "mklink /D" command. This only works if the creation of symbolic links is enabled on the gateway machine for the account we use for SSH connect!

The local folder where the symlinks were created is returned to the GUI. SSH paths leading to desired file on desired machine are constructed as path to gateway's local file but containing the machine-specific symlink.

```
sftp://user@gw_ip:port/path/to/gw-link-to-target-machine/path/on/target/machine/file.txt
```

