# NodeDecline

## Description

**NodeDecline** is a message a respondant uses to reject an initiator's NodeHello
introduction.  Nodes may decline a connection for any reason, or no reason at all.

Node declination reasons must be careful not to leak information that can be used
to profile the state of the node or of the node's perspective of the network to
avoid certain types of dragnet surveys through bogus introduction attempts or
Sybil attacks.  For this reason, the NodeDecline reason does not provide any
information about a node's identity or location in the network.  For instance, there
is no reason code for 'I am at capacity for peers' or 'I am at capacity for storage'.

Nodes typically decline introductions for one of the following reasons:

```
01 - Unable:    The node is unable to establish communications with the initiator,
                for any reason, including version incompatibility, resource
                capacity, or even if the node knows its host is shutting down.
                This type of rejection indicates the initiator may try again
                at some point in the future.

02 - Unwilling: The node is able to establish communications with the initiator,
                but is unwilling to do so.  This could stem from reasons such as
                the respondant is shunning the initiator, perceives it to be
                untrustworthy (such as lying or providing corrupted or garbage
                content) or a leech of network resources.  This type of rejection
                indicates the initiator should not try contacting this respondant
                again with the same node identity, and that doing so with a new
                identity may not resolve the issue.

03 - Untrusted: The node is not able to parse or accept the identity of the
                initiator node.  For instance, the message is malformed, the
                keying material is invalid, or the date of the mined identity
                is in the future or too far in the past.  This type of rejection
                indicates the initiator should not try contacting this respondant
                again with the same node identity, and that doing so with a new
                identity or properly formatted issue may resolve the issue.

00 - Other:     The node refuses to clarify why it is declining the connection.
                This type of rejection indicates nothing about the likely outcome
                of future connection attempts.  The initiator may have sent a
                valid message that was parsed and trusted, but no qualification is
                implied by this decline reason.
```

Respondant nodes that decline a connection may provide a suggested remediation.
A node that attempts to remediate itself may or may not be accepted on future
connection attempts.  Remediation suggestions include:

```
01 - Update:    Update to a newer version of the protocol

02 - Share:     Log the exchange of unique chunks between more unique nodes
                in the blockchain to improve network reputation

03 - Rekey:     Mine a new node identity

04 - Another:   Try another node

05 - Scram:     Simply don't connect to this node again for a while

00 - Other:     The respondant has no remediation suggestion to make for the
                initiator to overcome the rejection
```

Initiators do not have to handle any remediation codes they receive.  Respondants
may provide misleading or unspecific remediation codes.

## Wire format

NodeDecline message on-the-wire format is in little-endian format as follows

```
BYTES
[000-007] -  8 bytes - Message start (ASCII for MYSTIKO\r)
[008-008] -  1 byte  - Message type code: 02
[009-015] -  7 bytes - Number of QWORD's following this field until end of message
[016-016] -  1 byte  - Decline reason code
[017-017] -  1 byte  - Remediation suggestion code
[018-023] -  6 bytes - Zero padding to end the QWORD
[024-031] -  8 bytes - Message end (HEX for 0C AB 00 5E FF FF FF FF - caboose)
```

## Example

```
4D 59 53 54 49 4B 4F 0A - Message start (ASCII for MYSTIKO\r)
02 01 00 00 00 00 00 00 - Message type code, then number of QWORD's following (1 = 0x01)
01 02 33 33 33 33 33 33 - Rejection reason, remediation suggestion, then 6 bytes of 0-padding to round out
0C AB 00 5E FF FF FF FF - Message end (the 'caboose')
```

## Notes

Once a NodeDecline message is sent, the connection is immediately terminated.

## See also

* NodeHello
* NodeAccept