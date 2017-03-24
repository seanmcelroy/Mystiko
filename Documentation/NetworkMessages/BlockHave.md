# BlockHave

## Description

**BlockHave** is the start of an interaction in which an initiator node broadcasts
they have an encrypted piece of content (a block) that the respondant could choose
to recieve.  A successful conversation is as follows:

```
INITIATOR:  BlockHave    - I have data, here's its inexact ID
RESPONDANT: BlockWant    - I want that data
INITIATOR:  BlockTake    - Here's the data
RESPONDANT: BlockTook    - I got the data, here's my proof
INITIATOR:  BlockCommit  - Here's the true ID of the block (surprise!),
                           and a ticket you can countersign to publish we traded this block
RESPONDANT: BlockConfirm - Here's the countersigned ticket
EITHER:     BlockCredit  - Here's a countersigned ticket to add to the public ledger
```

The purpose of interchange is twofold:

1. Initiators do not reveal what block they have until the respondant confirms they
   have received a copy of it.  In other words, to understand what a neighbor
   possesses, you must possess it yourself.
2. Both the initiator and the respondant can optioanlly participate in signing an
   assertion that the trade took place.  If one or both sides choose not to make
   such an assertion, the block was still transmitted.
   
## Wire format

BlockHave message on-the-wire format is in little-endian format as follows

```
BYTES
[000-007] -  8 bytes - Message start (ASCII for MYSTIKO\r)
[008-008] -  1 byte  - Message type code: 03
[009-015] -  7 bytes - Number of QWORD's following this field until end of message
[016-047] - 32 bytes - First 32 bytes of the 64-byte SHA512 hash of the block
[048-055] -  8 bytes - Maximum size of the block
[056-063] -  8 bytes - Message end (HEX for 0C AB 00 5E FF FF FF FF - caboose)
```

*Maximum size of the block* is an inexact size of the block.  This permits a
respondant to determine whether it as capacity to receive the block based on its
rules for accepting new blocks.  To prevent pre-exchange identification of which
block this represents, the actual size of the block is this value or less.

```
Now()-ExpiryPeriod/RandBetween(2,10) and Now()
```

It is important this value have an inexact and random component to prevent
full-capture traffic analysis correlated with blockchain entires from determining
which nodes were communicating to exchange a block, in case they do not wish to
make their exchange public through a countersigned ticket in a subsequent
BlockCredit message.

## Example

```
4D 59 53 54 49 4B 4F 0A - Message start (ASCII for MYSTIKO\r)
03 05 00 00 00 00 00 00 - Message type code, then number of QWORD's following (5 = 0x05)
33 33 33 33 33 33 33 33 - First 32 bytes of the 64-byte SHA512 hash of the block
33 33 33 33 33 33 33 33
33 33 33 33 33 33 33 33
33 33 33 33 33 33 33 33
FF FF FF FF FF FF FF FF - Maximum size of the block
0C AB 00 5E FF FF FF FF - Message end (the 'caboose')
```

## Replies

Respondant nodes will respond with either no reply or:
* NodeWant

Respondant nodes may chose to delay their reponse with a NodeWant to confound
traffic analysis efforts from carrier advesaries.

## See also

