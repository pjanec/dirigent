## Architecture

Each computer is running an agent process. One of the agents runs a master server component. Agents connect to a single master.

The master's role is to execute plan/script logic and to tell agents what apps to start/kill.

Agent's role is to manage individual applications. Agent manages the processes running locally on the same machine where the agent is running. Agent takes care of local application launching, killing, restarting and status monitoring. 

Agents publish the status of local applications to master which in turn spreads it to all other agents. The status include whether the app is running, whether it is already initialized etc.

[dirigent internals diagrams](Architecture.drawio), open with [Diagrams.net](https://app.diagrams.net/)

[Direct link](https://app.diagrams.net/#Uhttps%3A%2F%2Fraw.githubusercontent.com%2Fpjanec%2Fdirigent%2F3.1%2Fdocs%2FArchitecture.drawio)
