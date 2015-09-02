# Net Chan

## Idea
Basic idea is to seamlessly integrate with local chans, but transfer messages over TCP.

Net-chans contain a 'membrane' chan that connect to the world through and communicate with internally.

## Protocol
The protocol is implemented in `NetChanBase`. It sends and receives byte chunks, errors; handles heartbeats (ping pong).
There is a Header class which represents 8 bytes of header. Some 'packets' can have 'data' after header, in which case header contains length of the data, etc.

### Header
The first byte is opcode (what type of 'packet' it is).
The rest are fields where meaning depends on the opcode.
All values are stored in Big-endiand.

Header details can be seen in [this table](https://docs.google.com/spreadsheets/d/194xfTzefP4BBAmkpHFahmH3JwOByksx5H1uvVMoljtk/pubhtml?gid=0&single=true).
<iframe src="https://docs.google.com/spreadsheets/d/194xfTzefP4BBAmkpHFahmH3JwOByksx5H1uvVMoljtk/pubhtml?gid=0&amp;single=true&amp;widget=true&amp;headers=false">
</iframe>

Error is designed so, that "all 0" is interpreted as Error 'packet' with no error message.

Flags (currently only one):
- 1 == HasNextFragment

Every net-chan contains async receive-loop that loads next header that is dispatched on opcode and calls appropriate handler.
`NetChanReceiverBase` overrides msg-handler.
Each handler produces next header, so it can start receiving as soon as possible.

## Sending

A buffer for sending is reused and filled through SerDes.
In case the SerDes requires bigger buffer, it fails and growable MemoryStream is used instead.
The buffer is then exchanged for this new bigger buffer.

Messages are read from world-membrane-chan through async loop, sending each message directly.

## Receiving

Msg-handler loads bytes per `header.length` and reconstructs messages through SerDes, which then sends to world-membrane-chan.
On successfull receive returns `Ack`, on fail `Err` + throws the exception locally.

## Config
Most importantly provides SerDes. Potentially default for serializable types.

Inludes internal fields for netIn and netOut streams. As it is built now, they are NetworkStream but can be changed to be something else for testing or if there is another layer... (like encryption / ...)

Otherwise can provide:
- initial sender and receiver buffer sizes.
- ping delay
- (for server)
    - whether receiver or sender should propagate close

# Net Chan Server
Allows for creation of `NetChanServer`s from WCF calls to `ChanStore`.
All connected to the local part of net-chan.
It also collects all exceptions from creating and using net-chan.

It stores all net-chan-*-servers in a set, removing them on closing. If propagates, closing everything in the set + underlying local chan.


# Net Chan Client Cache
Knows how to connect to other `ChanStore`s and caches the connections until closed.
It also collects all exceptions from creating and using net-chans.

Connecting is always run on a background thread to prevent dead locks. This implies this cannot be used in scenario where only 1 thread is available for use. (uses Task.Run)

Know how to change chan-uri to server-uri + local-chan-uri at server, sending that to the server in a WCF request.

Cashe has 2 levels: connecting and cashed: connecting need to be stored to not start cashing twice. All access to (potentially) cached client-factories is asynchronous.

If connecting to the channel at any point fails, the exception is propagated. (including exception in correct response from server)
(I found out I probably can throw exceptions in WCF handlers and they will get correctly propagated but This makes me calmer...)

Child classes (receiver and sender) handle the connection core; wrap resulting net-chan-client in a factory per type from WCF request and register 'forgetting' from cache when chan closes.


