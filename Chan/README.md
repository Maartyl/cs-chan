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

## Chan Factory

Simple concept, that (generally) initialized with a chan allows to get senders and receivers. In case they are needed to be different objects, they can. (broadcast: special chan for every receiver)

## Chan Store
While working with this library, most people will come in contact with this and IChan interfaces.
This class works as a place, where chans are created and are accessed through.

Basic idea is storing a number of Dictionaries. 

Mainly `locals` where names point to ~tuples of local `ChanFactory` and possibly a server counter part for net-chans (in which case, they are cross-wired).

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



## Chan Utils

### Event

### Pipe


## SerDes




















































