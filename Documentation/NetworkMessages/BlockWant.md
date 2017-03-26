# BlockWant

## Description

**BlockWant** has one of two uses for a single purpose: to indicate that the node sending
the message desires a block.  It can be dispatched in one of two cases:

1. A node has previously received a BlockHave message through the networks unattended
   announcement and content push system.  This is the response to a solicitation to obtain
   a copy.
2. A node independently is requesting content by a partial hash of a block.  This could
   be on the basis of a query made interactivley by a user, or a queue being processed from
   a 'download list', perhaps but not necessarily queued by a user. 

Both BlockHave and BlockWant messages do not specify precisely what block they are seeking
in their messages.  Files are split and encrypted into parts called blocks.  Each block has
a unique SHA512 hash of 64 bytes in length.  BlockWant only uses the first 32 bytes of the
64 byte hash to advertise it desires a block.  It may or may not receive its intended
block, and in the process, may receive other blocks with the same 32 byte value as the
first half of their hashes.  This inexactness has two design goals:

1. Users have repudiation - it is not possible to prove that a node was requesting a
   specific block because both the request is identified with a non-unique identifier
   for a block and because there is no guaranteed population list of blocks since blocks
   traded are not necessarily reported through countersigned tickets to the transfer
   blockchain via BlockCredit messages.  (See BlockHave for the description of the full
   conversation sequence)
2. Nodes accumulate more content than their controlling users can specifically request. This
   reduces the ability of nodes to leech specific content and ensures nodes cannot greylist
   the receipt of blocks since they will receive a copy of any block (participate in
   distribution) before they have the opportunity to ascertian exactly what the block is.

In addition, nodes can send BlockWant requests for reasons other than a user wishing to
download specific data.  Nodes naturally seek to accumulate date to participate in the
overall quality and service improvement of the network.  With no user interaction on a node,
a node will attempt to send BlockWant messages in this priority:

1. If a user has obtained a Resource Record that it has queued for download, then the
   node also has a list of Block SHA512 hashes, and will attempt to grab the blocks
   that comprise the file.
2. If a node sees unsoliciated BlockHave requests from neighboors, and it sees content
   that is at a location closer than other data already in its repository, it will
   express interest in the block by sending a BlockWant message.  When it receives the
   block, if it has insufficient space to keep blocks at a further location than the
   block it received, when it is no longer transmitting that block to another neighbor
   and has not sent a BlockHave message for it in at least 72 hours, it will delete
   the block.
3. If a node has a copy of the transfer blockchain ledger, a list showing what nodes
   traded messages, if any of the nodes in that list are currently neighbors, then the node
   will request blocks from neighbors for in ascending order of the blocks being closest
   (most relevant) to its location
4. If a node has a copy of the transfer blockchain ledger, if any nodes have not been
   recently traded, in ascending date of the last published block trade, that block
   will be requested in a BlockWant message.
5. Any BlockHave message from a neighboor that advertises a block a node does not already
   have will be responded to with a BlockWant.
      
Using this methodology, over time, nodes then will fill to capacity and then specialize
for its location

## Wire format

BlockWant message on-the-wire format is in little-endian format as follows

```
BYTES
[000-007] -  8 bytes - Message start (ASCII for MYSTIKO\r)
[008-008] -  1 byte  - Message type code: 04
[009-015] -  7 bytes - Number of QWORD's following this field until end of message
[016-047] - 32 bytes - First 32 bytes of the 64-byte SHA512 hash of the block
[048-079] - 32 bytes - NodeID making the request
[056-063] -  8 bytes - Message end (HEX for 0C AB 00 5E FF FF FF FF - caboose)
```

*NodeID* The ID of a node is the XOR of its Public X and Public Y keys.  This allows
nodes to quickly determine if a peer is a node they have seen elsewhere in their
routing table.  In addition, this is required for nodes that are not peers to a
node sending a BlockWant message to understand how to address the node in a reply
BlockTake message.

## Example

```
4D 59 53 54 49 4B 4F 0A - Message start (ASCII for MYSTIKO\r)
04 09 00 00 00 00 00 00 - Message type code, then number of QWORD's following (9 = 0x09)
33 33 33 33 33 33 33 33 - First 32 bytes of the 64-byte SHA512 hash of the block
33 33 33 33 33 33 33 33
33 33 33 33 33 33 33 33
33 33 33 33 33 33 33 33
88 88 88 88 88 88 88 88 - NodeID of this node sending the request for the block
88 88 88 88 88 88 88 88
88 88 88 88 88 88 88 88
88 88 88 88 88 88 88 88
0C AB 00 5E FF FF FF FF - Message end (the 'caboose')
```

## See also

