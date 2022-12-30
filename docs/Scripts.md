# Scripts

Script is a C# class dynamically compiled and started on given Dirigent node (agent, master, UI).

Scripts allows for running a user specific code utilizing the Dirigent API. This allows the user to implement whatever logic of controlling the applications, including complex multi-machine stuff.

The script is able to

* Start/kill apps/plans/scripts.
* Check the status of running apps/plans/scripts.
* Check what Dirigent Agents (i.e. machines) are on-line.
* Send any Dirigent command/message to all nodes.
* Start & await remote scripts.
* and more...

#### Script example
Please see the [DemoScript1.cs](../src/Dirigent.Common/Scripts/DemoScript1.cs) source file

#### Script Status

Script status is described by a status code and additional details (short text description and a byte array data).

| Status Code | Status Text       | Data                 | Description                           |
| ----------- | ----------------- | -------------------- | ------------------------------------- |
| Starting    | N/A               | N/A                  | Script is being instantiated.         |
| Running     | Set by the script | Set by the script    | Script is running.                    |
| Finished    | N/A               | N/A                  | Script successfully finished.         |
| Cancelling  | N/A               | N/A                  | Script is being cancelled.            |
| Cancelled   | N/A               | N/A                  | Script was cancelled.                 |
| Failed      | N/A               | Serialized exception | Exception was thrown from the script. |

The status text (and byte array data) can be set by the script at any time to provide more info on what the script is currently doing.

The status text is shown to the user on the Dirigent's UI.

The format of the byte array data is script specific.

#### Remote Execution

Scripts can be started from starting machine but instantiated remotely on different target machine. The starting node can wait for the script to finish its job and to return the results.

WARNING: The script need to be available on the target machine!

#### Arguments and results

A script can receive arbitrary arguments serialized in a byte array. The script needs to know how to deserialize and to interpret the data. The caller needs to provide arguments that are compatible with the script being called.

A script can return a result back to the caller as a byte array. The caller need to understand how to deserialize and interpret the results returned.

The serialization/deserialization is done via MessagePack using Contract-less Standard Resolver. It takes all public fields of given C# class with the exception of those explicitly marked as ignored.

#### Asynchronous nature

Scripts run asynchronously, i.e. they do not block other Dirigent operations.

As most parts of Dirigent run synchronously single threaded, the calls from the script to Dirigent's API get dispatched to Dirigent's main thread, causing the script to wait until the next tick.

This is why all the Dirigent API calls need to be awaited (like "await StartApp" or "await KillApp").

WARNING: The await in this case does not mean the script waits for the operation to finish. It waits just until the dirigent sends the network command. You need to check the app/plan state if you need to know if it started successfully or not.

### Singleton scripts

Singleton scripts are special kind of scripts preconfigured in SharedConfig.xml.

They are exposed to the user via Dirigent's UI.

This makes them possible to be started/stopped via the Dirigent UI or via the Dirigent's CLI by their unique id as configured in the SharedConfig.xml.

They run on master.

At most one single instance of each of the scripts can run at any given time. If an instance is already running, further start requests for the same instance are ignored until the existing instance terminates.

Singleton script's results are ignored.

