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
<iframe src="https://docs.google.com/spreadsheets/d/194xfTzefP4BBAmkpHFahmH3JwOByksx5H1uvVMoljtk/pubhtml?gid=0&amp;single=true&amp;widget=true&amp;headers=false"></iframe>

Error is designed so, that all 0 is interpreted as Error 'packet' with no error message.

Flags (currently only one):
- 1 == HasNextFragment

## Sending

## Receiving

## Config
