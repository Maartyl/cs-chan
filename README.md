Chan System
===========

Library for sending messages between unrelated subsystems or parts of application possibly over a network.

Senders and receivers per chan can be accessed from other computers to transfer messages simply by calling `.Send(T msg)` and `Task<T> .Receive()`. Messages are serialized and sent over TCP connection. Not all chans have to be network accessible, in which case they are much faster and messages don't need to be serializable. Application components don't need to know how are messages actually transported, which adds scalability and independence between components.

About
-----

Chans are generic, one-directional asynchronous channels between (possibly) multiple senders and receivers.

### Ideas

-	Subsystems pass around immutable messages.
-	Messages are preferably small and of 'informative' character, not huge chunks of data.

	-	Although, net-chans currently support messages up to 64 KiB in size when serialized.

-	Chans can be composed into networks and trafic observed in connection points.

-	System is not necessarily dependent on C# and it is possible to write parts of system that would seamlessly integrate in other languages as well.

	-	Although, currently WCF is used for opening net-chans.

-	All 'nodes' able to act as servers are equal.

	-	Clients that cannot be servers can still do everything else, but host chans.

-	WCF allows for authentication when opening chans.

	-	Although it is not implemented in Chat test application.

### Influences

-	Clojure core.async channels
-	Human interactions

	-	you can say things to others
	-	or listen
	-	not really 'call methods' on them...
	-	This allows for more cooperative, yet less dependent systems.

-	To big part other inter-process communication, like TCP or especially unix pipes.

	-	Chans are similar to pipes but support multiple receivers and arbitrary message types, not just chars.
	-	`Chan<byte>` would be very similar. (only much slower. ^^)

-	Systems for workload distribution

-	Event distribution and propagation

-	Reactive platforms

-	...

### Motivation

(subjective)

-	I worked on a project that was about passing data around, essentially.
	-	I realized: this seems like something people do all the time.
	-	The project used WCF and it wasn't very well suited for the task:
		-	no push (that was the main problem)
		-	specifying a lot of details not necessary for something simple
		-	many things specific to this one problem that could be generic (statically typed method signatures) - ...
-	I thought about using TCP but it seemed like way too much work then.
-	So, I wanted to create simple system that allows sending messages between subsystems over network.
	-	It wasn't that hard to extend it to local systems.
-	I looked around: I never really saw such library anywhere and thought it would be cool.
-	I needed something for a school project and this seemed both interesting and useful.

### Outlook

(subjective)

Currently there is no notion of identity in the system and all communication is one-directional. I would like to extend it with a Dispatch mechanism and ability to send messages to certain participants (especially replies). The system could be fairly similar to Actor model (with no global system) but should work ~seamlessly over the internet just as well as locally.

Simple Dispatch idea can be seen in Chat example application. Proper one should be fully async and with the ability to reply (async return value).

Chans would then connect more local and more reactive parts built around Dispatch.

Good but terrible idea is for .Send to return `Task<Task<TMsg>>`, where first task completes upon properly sending/enqueuing/... and second after received with possible reply or Exception thrown in receiver. - Which has many complications like: multiple receivers, possibly passing Exceptions around on the internet, ...

Currently does not work through NAT as WCF request opens a TCP on some port and sends that to client to connect to it. This is obviously not good but I realized it too late: I will change it once I move opening channels to TCP as well (once I know how/if to solve authentication).

### Chan System Overview Diagram

[Diagram](Chan/chanArchitecture.png) showing structure behind main part of `ChanStore` API. (Does not include exception handling and details for clarity.)

![Diagram](Chan/chanArchitecture.png)

### Implementation Details

See [Chan](Chan).

Get
---

cs-chan is a normal .NET library.

### Prerequisite

-	.NET4.5 (or mono equivalent)
-	For server:

	-	ability to run WCF server

		-	Mainly means Admin rights on Windows.

	-	No closed NAT between server and client

	-	Currently: server opens chan on some port to which client connects.

	-	This should probably be changed in the future, ideally removing WCF altogether and having everything in TCP...

### Download

-	For now: you can compile it from source.
	-	There is no final version, yet: It will be added then.

Getting Started
---------------

-	Create chans on `ChanStore`

	-	`.Create{Local,Net}Chan<TMsg>(cfg)`
	-	where cfg is configuration; simplest: `NetChanConfig.MakeDefault<TMsg>()`
	-	If `TMsg` isn't serializable, you have to provide custom ISerDes.

-	access the chans through `.Get{Sender,Receiver}{,Async}(uri)`

	-	where uri is: `chan:[//<authority*>]/<chan name>`
	-	* : If accessing 'outside' end of a net-chan.

-	Receivers then receive what counterparts on the same chan sent.

	-	net-chans are wired so that local and remote ends are connected.
	-	local: no authority specified
	-	remote: authority specified (`localhost` if local chan)

-	Idea behind accessing through URI:

	-	Normal code shouldn't even necessarily know what Uri it is using to access chans.

-	Net-chans are only accessible after calling `.StartServer(port)`.

	-	Get URIs then have to specify this port in authority.

Examples
--------

### Chat Simple

-	in Chat/Program.cs
-	when run with --simple <arg>

	-	arg: port - starts server
	-	arg: host:port - starts client

-	Only to show simple way to access chans and connect 2 computers.

-	All messages from server are broadcasted to everyone connected, they send everything to server and it does nothing much else.

### Chat

Details are in [/Chat](Chat) folder in this repository. It also includes very simple, limited and synchronous version of Dispatch mentioned in [Outlook](#outlook) section.

---

School part
-----------

This has been created as a school project but with the intention to also use it for other projects.

### Used Technologies

-	Sockets
	-	My TCP protocol
-	WCF
-	Linq
	-	including Query syntax
-	Tasks
-	async methods
	-	The main part, Chan System, is based around async and Tasks.
-	Reflection
	-	in generating default SerDes
-	Serialization
