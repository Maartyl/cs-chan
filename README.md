Chan System
===========

Library for sending messages between unrelated subsystems or parts of application possibly over an internet.

## About

Chans are generic, one-directional asynchronous channels between (possibly) multiple senders and receivers.

#### Ideas

- Subsystems pass around immutable messages.
- Messages are preferably small and of 'informative' character, not huge chunks of data.
    - Although, net-chans currently support messages up to 64 KiB in size when serialized.
- Chans can be composed into networks and trafic observed in connection points.

- System is not necessarily dependent on C# and it is possible to write parts of system that would seamlessly integrate in other languages as well.
    - Although, currently WCF is used for opening net-chans.
- All 'nodes' able to act as servers are equal.
    - Clients that cannot be servers can still do everything else, but host chans.
    
- WCF allows for authentication when opening chans.
    - Although it is not implemented in Chat test application.

#### Influences

- Clojure core.async channels
- Human interactions
    - you can say things to others
    - or listen
    - not really 'call methods' on them...
        - This allows for more cooperative, yet less dependent systems.
- To big part other inter-process communication, like TCP or especially unix pipes.
    - Chans are similar to pipes but support multiple receivers and arbitrary message types, not just chars.
    - `Chan<byte>` with only 1 receiver would be very similar. (only much slower. ^^)

- Systems for workload distribution
- Event distribution and propagation
- Reactive platforms
- ...

#### Motivation

- I worked on a project that was about passing data around, essentially.
    - I realized: this seems like something people do all the time.
    - The project used WCF and it wasn't very good: 
        - no push
        - weird stuff around
        - many things specific to this one problem that could be generic
        - ...
    - I thought about using TCP but it seemed like way too much work then.
    - So, I wanted to create simple system that allows sending messages between subsystems over network.
        - It wasn't that hard to extend it to local systems.
- I looked around: I never really saw such library anywhere and thought it would be cool.
- I needed something for a school project and this seemed both interesting and useful.

#### Outlook

Currently there is no notion of identity in the system and all communication is one-directional.
I would like to extend it with a Dispatch mechanism and ability to send messages to certain participants (especially replies).
The system could be fairly similar to Actor model (with no global system) but should work ~seamlessly over the internet just as well as locally.

Simple Dispatch idea can be seen in Chat example application. Proper one should be fully async and with the ability to reply (async return value).

Chans would then connect more local and more reactive parts built around Dispatch.

Good but terrible idea is for .Send to return `Task<Task<TMsg>>`, where first task completes upon properly sending/enqueuing/... and second after received with possible reply or Exception thrown in receiver. - Which has many complications like: multiple receivers, possibly passing Exceptions around on the internet, ...


## Get

cs-chan is a normal .NET library.

### Prerequisite

- .NET4.5 (or mono equivalent)

- For server:
    - ability to run WCF server
        - Mainly means Admin rights on Windows.
    - No closed NAT between server and client
        - Currently: server opens chan on some port to which client connects.
        - This should probably be changed in the future, ideally removing WCF altogether and having everything in TCP...

### Download
- For now: you can compile it from source. 
    - There is no final version, yet: I will add it then.



## API overview


## Example

### Chat Simple
- in Chat/Program.cs
- when run with --simple <arg>
    - arg: port - starts server
    - arg: host:port - starts client
- Only to show simple way to access chans and connect 2 computers.
- All messages from server are broadcasted to everyone connected, they send everything to server and it does nothing else.

### Chat
Details are in [/Chat](Chat) folder in this repository.

------------------------------

# TODO

- Getting Started
- Basic idea how works from perspective of usage (WCF...)
- Basic idea how works internally
- API overview
- Example
    - Extra simple, direclty in Program
        - Only 1 server, 1 client
        - Shows how works.
    - Chat app
        - Include 'user' documentation on how to use that
    - Also shows Dispatch idea
        - Async version with replies is an idea of future extension/lib


------------------------------
## School part
This has been created as a school project but with the intention to also use it for other projects.

### Used Technologies
    - Sockets
        - My TCP protocol
    - WCF
    - Linq
        - including Query syntax
    - Tasks
    - async methods
        - The main part, Chan System, is based around async and Tasks.
    - Reflection
        - in generating default SerDes
    - Serialization
    
    
    
    
    