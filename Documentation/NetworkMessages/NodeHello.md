# NodeHello

## Description

**NodeHello** is a message an initiator uses to attempt to establish connectivity with
another node.  It contains basic information that identifies the node and supplies
parameters that a respondant can use to:

1. Decide whether the client has a Proof of Work showing it has mined its identity
2. Validate whether the client is shunned on the network
3. Understand the distance between itself and the initiator and optionally:
   a. Continue building a connection, potentially discarding another connection that
      supplies little value or is a much further distance away, or
   b. Inform the initiator it will not continue building a connection

## Wire format

NodeHello message on-the-wire format is in little-endian format as follows

```
BYTES
[000-007] -  8 bytes - Message start (ASCII for MYSTIKO\r)
[008-008] -  1 byte  - Message type code: 01
[009-015] -  7 bytes - Number of QWORD's following this field until end of message
[016-023] -  8 bytes - Date epoch
[024-055] - 32 bytes - Public Key X
[056-087] - 32 bytes - Public Key Y
[088-095] -  8 bytes - Nonce
[096-097] -  2 bytes - Version
[098-103] -  6 bytes - Zero padding to end the QWORD
[104-111] -  8 bytes - Message end (HEX for 0C AB 00 5E FF FF FF FF - caboose)
```

*Date epoch* is the number of seconds since the Epoch that the identity was
created.  This can be used by nodes to limit peers to only accept identities that
have either shown presence in public transfer blockchains or are created after a
certain date, to prevent identity mining and stockpiling.

*Nonce* is used as a proof of work to show the identity was mined.  Identities
are mined using SHA512(dateEpoch + publicKeyX + publicKeyY + nonce) to achieve
a hash value with a target difficulty of starting zeros.

## Example

```
4D 59 53 54 49 4B 4F 0A - Message start (ASCII for MYSTIKO\r)
01 0B 00 00 00 00 00 00 - Message type code, then number of QWORD's following (11 = 0x0B)
33 33 33 33 33 33 33 33 - Epoch
FF FF FF FF FF FF FF FF - Public Key X
FF FF FF FF FF FF FF FF
FF FF FF FF FF FF FF FF
FF FF FF FF FF FF FF FF
CC CC CC CC CC CC CC CC - Public Key Y
CC CC CC CC CC CC CC CC
CC CC CC CC CC CC CC CC
CC CC CC CC CC CC CC CC
55 55 55 55 55 55 55 55 - Nonce
01 00 00 00 00 00 00 00 - Version, then 6 bytes of 0-padding to round out
0C AB 00 5E FF FF FF FF - Message end (the 'caboose')
```

## Notes

The ID of a node is the XOR of its Public X and Public Y keys.  This allows nodes
to quickly determine if a peer is a node they have seen elsewhere in their routing
table.

## Replies

Respondant nodes will respond with one of two possible network messages:
* NodeAccept
* NodeDecline

## See also

