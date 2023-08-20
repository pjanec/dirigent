# Environment variables

Some apps need specific environment variables to be set.

There are different ways how to set an environment variable for an application started from Dirigent.

1. Dirigent agent process variables.
   * Inherited from agent's process
   * App-specific copy taken at time of starting the app from the Dirigent.
   * Can be changed dynamically using `SetVars` command,  applies to apps started after (not for already running ones)
2. Variables set for a plan in SharedConfig. [NOT IMPLEMENTED YET]
   * Seen by any app started from the plan.
4. Variables set for an app in SharedConfig.
   * Visible just for this app, no other apps see that (unless they define their own).
5. Variables set for an app in StartApp command.
   * Explicitly specified in the command for starting the app.
   * Top priority.

The latter in the list above takes the precedence over the former.
