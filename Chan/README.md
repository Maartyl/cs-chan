Chan
====
# concept and implementation details
-----

## Chan idea
Chans have two sides `IChanReceiver` and `IChanSender` with fairly obvious roles.

What completion of `.Send` means can differ per type of chan, but basically accepted or rejected. In case of net-chans it can include exception "why couldn't send".

Completion of `.Receive` provieds with the message.

Most of the internals (and thus API) are based on the Task monad.

### .Close
All subsequent calls to `.Send` will return cancelled tasks and so will `.Receive` after emptying internal queues.

Close completes when chan is empty.

### .Receive(callback)
- Special version of `.Receive` exists that can set how the task returned from `.Send` completes.
- This is especially useful for chaining chans.
- It does not work perfectly with broadcast chans.
- It only works for local chans.

## Local Chans
Originally, there was a couple drafts of which 2 survived and I implemented one.
- ChanAsync
    - version, where `.Send` and `.Receive` are asynchronous (never block thread, unless too many)
    - Each task resulting from `.Send` completees upon delivery through `.Receive`
    - Each task resulting from `.Receive` completes upon matching `.Send`
    - If these were blocking operations, this would effectively synchronize the threads: both waiting until transaction.
        - One of drafts implemented this but I decided to go with Async version: uses more space but does not waste threads: much more precious resource.
- ChanQueued
    - Difference from ChanAsync being that `.Send` completes immediately after enqueuing not when delivered.
    - `.Receive` cannot specify if accepted or not and messages can get lost.
    - This seems completely inferior to ChanAsync but could be useful in some special situations.
        - Not implemented because of this: mostly not needed.
        - Net-chans work this way.
            - They are structurally able of completing upon delivery but each send-task would take realy long, so I decided to not implement it this way...
- rejected
    - ChanBlocking, other implementation variants of ChanAsync

### ChanAsync implementation

Each ChanAsync contains 2 concurrent,blocking queues for following 2 scenarios:
- overpressure (Send > Receive):
    - enqueue 'waiter' that holds on the message and a TaskCompletionSource that is completed upon delivery and returned from Send right away.
- underpressure (Receive > Send):
    - enqueue 'promise' that is essentially TaskCompletionSource, completed in Send.
- balanced:
    - This can be solved by small over/under pressure or alternating of both.

#### .Send
    if any promise:
        deliver
    else:
        enqueue waiter
#### .Receive
    if any waiter:
        deliver
    else:
        enqueue promise

## Net Chans
I originally didn't intend to create multiple versions. The original idea was for .Send to return a task mainly to inform about exceptions while sending. After I extended the idea to completing when received, idea arose to complete when receivwed for net-chans too. It would probably take really long before completing, though. That version is not implemented.

Implemented version:

The basic idea is a binary TCP protocol. More details: [NetChan](NetChan).

## Chan Distribution Type
Currently there are 2 (and I cannot think of any other reasonalbe options but there could be...)
- Broadcast - all receivers receive every message
    - works like event distribution
- FirlstOnly - works like workload distribution
    - It works fairly evenly: queue of requests
    - Only problem is: different queue at each 'node' so chaning chans can cause uneven distribution overall even though it's even at each 'fork'.
        - Probably good way of solving this: back propagation of how many requests made along the path... - For most part, unnecessarily complicated...

## Chan Factory

Simple concept, that (generally) initialized with a chan allows to get senders and receivers. In case they are needed to be different objects, they can. (broadcast: special chan for every receiver)

## Chan Store
While working with this library, most people will come in contact with this and IChan interfaces.
This class works as a place, where chans are created and are accessed through.

Basic idea is storing a number of Dictionaries:

Mainly `locals` where chan-names point to ~tuples of local `ChanFactory` and possibly a server counter part for net-chans (in which case, they are cross-wired).

Then there are `clients`, senders and receivers: These are clients connected to remote chans in some other `ChanStore`, potentially in another process, computer...

- overview of instance variables
    - `locals` - as mentioned; chans created in this ChanStore
    - `clients` - chans created in some other ChanStore only referenced from here
        - there are different caches: `clientReceivers` and `clientSenders`
        - each cache record contains 1 connection wrapped in a ChanFactory
    - `clientBindingsSender` and `clientBindingsReceiver`
        - these are just default Binding for unconnected/future clients for WCF
        - if not specified uses DefaultBinding that exists for the whole ChanStore
    - `freeChans` - idea that I can 'free' chans after I am done using them.
        - It turns out, it's not really necessary but it might be in the future.
        - It is used for no longer sending anything in case of broadcast...
        - this just a dict from each chan to an action, freeing it. - results in a bool, whether freed (also far from perfect...)
    - `netChanProviderHost` is just the service host associated with this ChanStore
    - `connectingServerTimeout` - after chan opened in server but no-one connected from outside for that long: cancel

The implementation details should be obvious from code and comments in code.

## Chan Utils

There are 'helper' functions in static class `Chan`.

### Pipe
Connects 2 chans (receiver and sender).

- Potentially observable. (optional argument: Action<TMsg>)
- Potentially different types. (optional argument: Func<TMsgInReceiver, TMsgInSender>)
- Potentially closes other chan if one closes. - Propagates close by default.
- Returns task completing when one of chans closes. (or both, if propagates)

### Listen
- Allows consuming a chan-receiver.
- Each message invokes provided action. If provided action is null, all messages are thrown away until someone subscribes to the event.
- Returns `ChanEvent` that exposes 
    - event, that fires for each message.
    - `.Close` method that closes underlying chan and returns:
    - `.AfterClosed():Task` which completes after there are no more messages...
        - It might complete sooner:
            - some event handler thrown an Exception: propagated here + stops listening
        - if `chan.Close()` threw Exception, also propagated

### Combine
Similar to creating 1 2-directional pipe in C from 2 1-directional pipes.
Just returns a chan that can do both, sending and receiving, and delegates those to the provided sender and receiver.

## SerDes
Interface with 2 methods: Serialize and Deserialize. It is used by net-chans to send messages over TCP.
A default SerDes is provided in `NetChanConfig<T>` if `T` is serializable.

Each call is expected to serialize/deserialize exactly one message.

- `T Deserialize(Stream s)`
- `void Serialize(Stream s, T obj)`

#### Implementation Warning
For reason of reusing buffers, Stream passed to Serialize might be too 'short'. (not able to hold the whole message)

In this case, it is expected for the whole call to be rollbacked if it did any mutation and throw `NotSupportedException`. This exception will be thrown by the underlying MemoryStream (provided from net-chan), so it's fine to just rethrow or ignore it.


It should not happen often, as it is not expected for each message to be twice the size of the previous one.



















































