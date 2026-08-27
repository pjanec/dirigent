# Web API

Dirigent is running a simple web server on default port **8877**, making Dirigent's features
available also via the HTTP protocol. The port is set with `--httpPort`, master only.

Two things about that option are worth knowing:

* **`--httpPort 0` does not switch the web server off.** Zero means "not specified", so the
  default 8877 applies. Pass **`-1`** to run without a web server - which also matters when two
  Dirigent installations share a machine, as they would otherwise fight over the port.
* No URL reservation or elevation is needed. The server listens on its own socket rather than
  through the Windows HTTP stack.

### GET /api/appdefs

returns a list of all appdefs as JSON `[{'id':'m1.a'}, ...]`

### GET /api/appdefs/m1.a

returns a list of a specific appdefs as JSON `{'id':'m1.a'}`

### GET /api/appstates

returns a list of the state of all apps as JSON `[{'id':'m1.a', 'status':{'code':'Running', 'flags':'SIP'}}, ...]`

### GET /api/appstates/m1.a

returns a list of the state of given app as JSON `{'id':'m1.a', 'status':{'code':'Running', 'flags':'SIP'}}`

### GET /api/plandefs

returns a list of all plandefs as JSON `[{'name':'plan1', 'appDefs':[...]}, {'name':'plan2', 'appDefs':[...]}]`

### GET /api/plandefs/plan1

returns plandef of a single given plan as JSON `{'name':'plan1', 'appDefs':[...]}`

### GET /api/planstates

returns a list of the state of all plans as JSON `[{'name':'plan1', 'status':{'code':'InProgress'}}, {'name':'plan2', status={'code':'None'}}]`

### GET /api/planstates/plan1

returns a state of a single plan as JSON `{'name':'plan1', 'status':{'code':'InProgress'}}`

### POST /api/cli

Executes a cli command sent as POST data; for example `StartApp m1.a`
Returns same response as from the CLI command (see CLI command reference), for example `ACK`
