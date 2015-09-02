# Chan Chat

A fairly simple chat using chans for actual connecting and sending of messages.

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


### Gui class

Gui is independent of the rest of application and has no idea how is used.

Consists of panel which can change contents (implemented cases: help and chat) and 1 line at bottom used to issue commands.

Gui has methods that can change it (append message / system information / ...) and events (tab-completion-request, command, ...)

### Application run arguments
All arguments are interpreted as commands. I.e. the same as if writen to Gui right after start.

## How uses chans

{...}

### Server
Upon opening server, it creates ChanStore (each time: main reason being WCF) and creates broadcast channel, correctly pipeing all excepttions to connector.
If everything opened fine, it reregisters server related commands to 'already running' and actual stop.

Done mainly in `GuiChat.InitServer`.

### Client
Client is a special class, constantly registered in connector that is stateful and respons differently per state (connecting / connected / disconnected / ...). Methods on this class are not necessarily thread safe but generally good enough for how it is used.

`chans` holds on broadcast chan, that can be accessed through it. Other variables should be obvious from context.


## Connector (Reactive dispatch)
Idea, that could be extended and become part of chan library.

## System
{what connected to connector, how, order}







