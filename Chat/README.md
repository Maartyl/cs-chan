Chan Chat
=========

A fairly simple chat using chans for actual connecting and sending of messages.

How to use
----------

For chat to work, clients must all connect to the same server.

To start server, the command `:server.start` is used. Different clients can then connect to this server, including the one running in the same application(process) but not necessarily.

One can connect to server through the `:connect` command.

Server requires port to run on. Client then has to connect to that computer (ip address: obtainable though `:ip` command*) + has to specify the port used to start the server delimited by `':'`.

\(* The ip command provides correct ip only if both computers are on the same network.)

Example:

```
#Server
:server.start 4567
:ip #-> 2.2.2.2

#Client
:connect 2.2.2.2:4567
```

Use `:name <new name>` to change your name. Every client should do this to not confuse others with who wrote what.

Overview of code
----------------

### `Program.cs` `.Main`

Normally only calls `GuiChat.Start` and includes running commands from arguments but also contains complete, very simple example how to use net-chans.

### `GuiChat.cs`

#### `.Start`

Creates `Connector` and calls `Init` and `StartGui`.

#### `.Init`

Most application logic. Registers all commands that don't require Gui, also creates `ChatClient`.

#### `.InitServer`

Run for each server start from server-start handler defined in `Init`.

#### `.StartGui`

Starts WinForms main loop and in `.OnLoad` registers all Gui connector handlers and subscribes connector runs to Gui events.

#### `.testParsing`

Simple test included to show how parsing works.

#### `.longestCommonPrefix`

Function found on the Internet, rewritten to C# and slightly improved to allow skipping what is known to be the same.

### 'ChatClient.cs'

Class with mutable state, that represents client in different states (connected, disconnected, ...). Handless connecting itself (accessing correct chan through store).

### `Gui.cs`

Independent class capable of showing different content but has methods that correspond to needs from system. (write system notification / error / message; completion, scroll down...). Uses Windows Form internally.

### `Connector.cs` `:Dispatch`

See [Connector](#connector-reactive-dispatch)

### `Dispatch.cs`

See [Connector](#connector-reactive-dispatch)

### `Cmd.cs`

Contains command names as string constants and command parsing.

### `CmdArg.cs`

Struct representing string with source. Used in connector and in `Message`

### `Message.cs`

Adds enum with type of message to CmdArg.

### `Settings.cs`

Contains settings for the application. Some are static or constant.

### `Exts.cs`

Syntax sugar. Mainly for `PipeEx` so it can be called on exceptions and tasks and not just connector.

### \(`ActionTAwaiter.cs`\)

Attempt to write my own awaitable that turned out to be just less capable `TaskCompletionSource`. The reason being replies in connector.

Should not be counted to project size.

---

UI Overview
-----------

UI is based around commands that map to actions/handlers. Each action have an argument (string with source) and every command is a string. These commands can be written by hand or invoked programatically. All parts are independent and only assume* other parts exist. (* In case they don't, only thing that has to exist is error handler/action.)

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

Each `':'` is a static property on Settings `UserCommandStart`. `text` is `Cmd.Text`.

After invoking that in Connector, all `null` commands get translated to `Chat.NoCommand`. All `text` commands are either canceld in case it's just whitespace / ... or trim argument and are translated to `send` which is then handled by passing message to chans...

For examples see `GuiChat.testParsing`. This method serves no real purpose, but it is useful to see how command parsing works.

Commands in `Cmd` that start with `#` are 'internal' and cannot be typed by user.

### Gui class

Gui is independent of the rest of application and has no idea how is used.

Consists of panel which can change contents (implemented cases: help and chat) and 1 line at bottom used to issue commands.

Gui has methods that can change it (append message / system information / ...) and events (tab-completion-request, command, ...)

### Application run arguments

All arguments are interpreted as commands. I.e. the same as if writen to Gui right after start.

How uses chans
--------------

### Server

Upon opening server, it creates ChanStore (each time: main reason being WCF) and creates broadcast channel, correctly pipeing all excepttions to connector. If everything opened fine, it reregisters server related commands to 'already running' and actual stop.

Done mainly in `GuiChat.InitServer`. (and in `Cmd.StartServer` handler defined in `GuiChat.Init`\)

### Client

Client is a special class, constantly registered in connector that is stateful and respons differently per state (connecting / connected / disconnected / ...). Methods on this class are not necessarily thread safe but generally good enough for how it is used.

`chans` holds on broadcast chan, that can be accessed through it. Other variables should be obvious from context.

Connector (Reactive dispatch)
-----------------------------

Based on idea, that could be extended and become part of chan library. (or another, tied library)

Easiest way to explain the concept:

```
Dictionary<TCmd, Action<TArg>>;
.Run(TCmd, TArg):BoolTCmdFound;
```

For Connector, `TCmd` is `string` and `TArg` is `CmdArg` which is essentially a tuple of a string and 'source'.

Each Dispatch (base of Connector) can invoke `Default` in case cmd was not found: for connector, this equals to running error command with argument being what was not found.

On top of dispatch, Connector provides `PipeEx` which is a set of methods that either get an `Exception` or a `Task` and invoke `Cmd.NotifyError` instead of throwing them / losing them.

### Register

Registers handlers for commands, has options \(`Dispatch.RegisterOpts`\):

-	Add: Only add if not exists. Exception if handler present. (default)
-	Merge: If already present, replaced with delegate composed of both.
-	Replace: In case of `null`, removes completely.

Connector provides shortcuts: `Reregister` and `Coregister`.

### Run

Runs appropriate handler, has flag options \(`Dispatch.RunErrOpts`\):

-	InformsOnly: No flag set.
-	Defaults: If not found, invokes `Default` handler instead.
-	Throws: If not found, throws exception.

System
------

What is connected to connector. (+ order)

First invoked is `GuiChat.Start` which creates connector and passes it to `Init` which initializes everything that does not need Gui. (This includes `Cmd.AfterGuiLoaded` which is invoked after all gui related handlers are registered. In which `initExtra` is invoked.)`GuiChat.Start` then invokes `StartGui` which creates Gui (which internally creates a `Form`) and runs provided delegate in the form's OnLoad event. (Which at the end invokes `Cmd.AfterGuiLoaded`.)

`InitServer` is invoked on each `Cmd.ServerStart`.

Easiest way of understanding what is registered is to read those methods. Especially `GuiChat.Init`.

### Adding new user commands

Adding new user commands is easy because all that is needed is provide the handler to Connector and everything else will work automatically. (Of course, It might be smart to include the command in Cmd constants and provide help...)
