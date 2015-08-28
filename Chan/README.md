Chan
====
# concept and implementation details
-----

## Chan idea

Chans have two sides `IChanReceiver` and `IChanSender` with fairly obvious roles.

What completion of `.Send` means can differ per type of chan, but basically accepted or rejected. In case of net-chans it can include exception why couldn't send.

Completion of `.Receive` provieds with the message.

### .Close
After calling this all `.Send` will return cancelled tasks and so will `.Receive` after emptying internal queues.

Close completes when chan is empty.

### `.Receive(callback)`
Special version of `.Receive` exists that can set how the task returned from `.Send` completes.
This is especially useful for chaining chans.
It does not work perfectly with broadcast chans.
It only works for local chans.

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

#### `.Send`
    if any promise:
        deliver
    else:
        enqueu waiter
#### `.Receive`
    if any waiter:
        deliver
    else:
        enqueue promise
        























































