# Mystiko Network Protocol

## Discovery

### Local area networks

Nodes publish

== Connection ==

Connection messages perform several functions:

1. Reciever receives sender's identity, which contains:
   A. Public key for future key exchange
   B. Proof-of-work for the sender's identity
      1. Nonce and hash prove node worked to produce the key
	  2. Provide an identity that can be used to approve or reject

2. Receiver replies with [encrypted with public key of sender], if approved
   A. Public key for future key exchange
   B. Proof-of-work for the receiver's identity
   C. Symmetric encryption key for future communications
   D. List of nodes seen in the past 30 days (IP's, Public Keys)

== Request Metadata ==

1. Sender:
   A. Latest metadata known

2. Receiver:
   A. Verifies the sender is legitiminate, which would be determined if
      1. Node has not requested the same in the past X days - prevents network leeching
	  2. Node has not supplied the same in the past X days - prevents network leeching
	  3. Node is participating on the network
         - or - 3B. Node is signing block chain requests
   B. If verified, sends an excerpt of the metadata base, which includes
      1. Content hashes + metadata for all files in the global database

== Cycle Update of Metadata ==

1. Sender:
   A. Broadcasts a ManifestCycleUpdate announcement
      1. Last content hash
      2. Next content hash, which is SHA512(lastContentHash + content + identity)
      3. Identity participation hash, which is SHA512(lastContentHash + identity + secretValue)
	     a. This allows the identity to prove it provided an accepted update in the future
      4. Nonce and hash prove identity worked to produce the update
