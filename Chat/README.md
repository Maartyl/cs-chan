# Chan Chat

A fairly simple chat using chans for actual connecting and sending of messages.

## How to use

For chat to work, clients must all connect to the same server.

To start server, the `:server.start` is used. To this server then can different clients connect, including the one running in the same application(process) but not necessarily.

One can connect to server through the `:connect` command.

Server requires port to run on. Client then has to connect to that computer (ip address: obtainable thourh `:ip` command*) + has to specify the port used to start the server delimited by ':'.

(* The ip command provides correct ip only if both computers are on the same network.)

Example: 
```
#Server
:server.start 4567
:ip #-> 2.2.2.2

#Client
:connect 2.2.2.2:4567
```


## UI Overview
{from (command) usage perspective}

UI is based around commands that map to actions/handlers. Eache action have an argument (string with source) and every command is a string.
These commands can be written by hand or invoked programatically. All parts are independent and only assume* other parts exist. (* In case they don't, only thing that has to exist is error handler/action.)

Most commands can be understood from `help` that can be found in `GuiChat.StartGui` or is shown on application startup.

Their definitions are mostly either in `GuiChat.Init` and `GuiChat.StartGui` if they change gui.


### Command parsing
Each 'line' (fully textual command) is parsed to 2 strings in the following fashion:
```
:<cmd> <arg>  -> cmd, arg
missing cmd: null
missing arg: null
:<cmd><end>   -> cmd, null
:<end> -> null, null
everything else -> text, line
```

Each ':' is a static property on Settings `UserCommandStart`. `text` is `Cmd.Text`.

After invoking that in Connector, all `null` commands get translated to `Chat.NoCommand`.
All `text` commands are either canceld in case it's just whitespace / ... or trim argument and are translated to `send` which is then handled by passing message to chans...

For examples see `GuiChat.testParsing`. This method serves little purpose (and should be a proper test), but it is useful to see how command parsing works.

Commands in `Cmd` that start with ` #` are 'internal' and cannot be typed by user.
### Gui class

Gui is independent of the rest of application and has no idea how is used.

Consists of panel which can change contents (implemented cases: help and chat) and 1 line at bottom used to issue commands.

Gui has methods that can change it (append message / system information / ...) and events (tab-completion-request, command, ...)

### Application run arguments
All arguments are interpreted as commands. I.e. the same as if writen to Gui right after start.

## How uses chans

### Server
Upon opening server, it creates ChanStore (each time: main reason being WCF) and creates broadcast channel, correctly pipeing all excepttions to connector.
If everything opened fine, it reregisters server related commands to 'already running' and actual stop.

Done mainly in `GuiChat.InitServer`. (and in `Cmd.StartServer` handler defined in `GuiChat.Init`)

### Client
Client is a special class, constantly registered in connector that is stateful and respons differently per state (connecting / connected / disconnected / ...). Methods on this class are not necessarily thread safe but generally good enough for how it is used.

`chans` holds on broadcast chan, that can be accessed through it. Other variables should be obvious from context.


## Connector (Reactive dispatch)
Based on idea, that could be extended and become part of chan library. (or nother, tied library)

Easiest way to explain the concept:
```
Dictionary<TCmd, Action<TArg>>;
.Run(TCmd, TArg):BoolTCmdFound;
```
For Connector, `TCmd` is `string` and `TArg` is `CmdArg` which is essentially a tuple of a string and 'source'.

Each Dispatch (base of  Connector) can invoke `Default` in case cmd was not found: for connector, this equals to running error command with argument being what was not found.

On top of dispatch, Conector provides `PipeEx` whish is a set of methods that either get an `Exception` or a `Task` and invoke `Cmd.NotifyError` instead of throwing them / losing them.

### Register
Registers handlers for commands, has options (`Dispatch.RegisterOpts`):
- Add: Only add if not exists. Exception if handler present. (default)
- Merge: If already present, replaced with delegate composed of both.
- Replace: In case of `null`, removes completely.

Connector provides shortcuts: `Reregister` and `Coregister`.
### Run
Runs appropriate handler, has flag options (`Dispatch.RunErrOpts`):
- InformsOnly: No flag set.
- Defaults: If not found, invokes `Default` handler instead.
- Throws: If not found, throws exception.

## System
What is connected to connector. (+ order)

First invoked is `GuiChat.Start` which creates connector and passes it to `Init` which initializes everything that does not need Gui.
(This includes `Cmd.AfterGuiLoaded` which is invoked after all gui related handlers are registered. In which `initExtra` is invoked.)
`GuiChat.Start` then invokes `StartGui` which creates Gui (which internally creates a `Form`) and runs provided delegate in the form's OnLoad event. (Which at the end invokes `Cmd.AfterGuiLoaded`.)

`InitServer` is invoked on each `Cmd.ServerStart`.

Easiest way of understanding what is registered is to read those methods. Especially `GuiChat.Init`.

### Adding new user commands
Adding new user commands is easy because all that is needed is provide the handler to Connector and everything else will work automatically. (Of course, It might be smart to include the command in Cmd constatnts and provide help...)







