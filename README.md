todo

Chan
====

Library for sending messages between unrelated subsystems or parts of application possibly over internet.

## About

Chans are generic, one-directional asynchronous channels between multiple senders and receivers.

#### Ideas

- Subsystems pass around immutable messages.
- Messages are preferably small and of 'informative' character, not huge chunks of data.
    - Although, net-chans currently support messages up to 64 KiB in size when serialized.
- Chans can be composed into networks and trafic observed in connection points.

- System is not necessarilly dependent on C# and it is possible to write parts of system that would seamlessly integrate in other languages as well.
    - Although, currently WCF is used for opening net-chans.
- All 'nodes' able to act as servers are equal.
    - Clients that cannot be servers can still do everything else, but host chans.
    
- WCF allows for authentication when opening chans.

#### Influences

- Clojure core.async channels
- Human interactions
    - you can say things to others
    - or listen
    - not really 'call methods' on them...
        - This allows for more cooperative, yet less dependent systems.
- To big part other inter-process communication, like TCP or especially unix pipes.
    - Chans are similar to pipes but allow multiple senders and receivers and arbitrary message types, not just chars.
    - Chan<byte> with only 1 sender and receiver would be very similar. (only much slower. ^^)

- Systems for workload distribution
- Event distribution and propagation
- Reactive platforms
- ...


#### Outlook

Currently there is no notion of identity in the system and all communication is one-directional.
I would like to extend it with a Dispatch mechanism and ability to send messages to certain participants (especially replies).
The system should be fairly similar to Actor model (with no global system) but should work ~seamlessly over the internet just as well as locally.

Simple Dispatch idea can be seen in Chat example application. Proper one should be fully async and with the ability to reply (async return value).

Chans would then connect more local and more reactive parts built around Dispatch.

Good but terrible idea is for .Send to return Task<Task<TMsg>>, where first task completes upon properly sending/enqueuing/... and second after received with possible reply or Exception thrown in receiver. - Which has many complications like: multiple receivers, possibly passing Exceptions around on the internet, ...


## Get

### Prerequisite

- .NET4.5 (or mono equivalent)

- For server:
    - ability to run WCF server
        - Mainly means Admin rights on Windows.
    - No closed NAT between server and client
        - Currently: server opens chan on some port to which client connects.
        - This should probably be changed in the future, ideally removing WCF altogether and having everything in TCP...

### Download


## API overview


## Example

### Chat Simple
- in Chat/Program.cs
- when run with --simple <arg>
    - arg: port - starts server
    - arg: host:port - starts client
- Only to show simple way to access chans and connect 2 computers.
- All messages are broadcasted to everyone connected and it does nothing else.


------------------------------

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
    
    
    
    
    