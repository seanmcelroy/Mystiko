# Mystiko Project Goals

## Anonymity

First and foremost, Mystiko aims to provide anonymity in several key ways:

### Repudiation of posession

Nodes do not know what they store.  Content is stored in encrypted splitfiles, called chunks.
The order of chunks for reassembly as well as the encryption key required to decrypt the
chunks back into their original file are stored in a manifest file.  This manifest file
is encrypted and stored separately from regular node exchanges.  This allows nodes to operate
on chunks by a unifying 'temporal file identifier', but they do not actually understand what
kind of data a temporal file identifier or any of the partial or complete chunk sets represent.

Only by marrying up a resource record, which requires inside knowledge calculated by network
participants over time as well as all the chunks of a file can a file be reassembled.
A node which has no resource records lacks the link between encrypted content and metadata
that describes it

### Repudiation of receipt

Nodes do not know what they receive from other nodes.  Nodes simply aim to identify properly
participating nodes and in a tit-for-tat fashion attempt to distribute their content in a
balanced fashion using a DHT to push data to nodes that have a higher degree of closeness
to a temporal file identifier, and attempt to pull data that is closer to its own 'location'.

For instance, a node with a identifier of 500 will attempt to pull all the chunks that
comprise files with a temporal file identifier that are near 500, such as 499 and 502.
Nodes operate only on hash derivations of encrypted content.

### Repudiation of distribution

Nodes do not know what they send to other nodes, if they do not contain Resource Records
for chunks they hold.  Resource Records are distributed via out-of-band means that are not
made available to the node-chunk-distribution mechanism.  Resource Record indexes are not
keyed by their temporal file identifier, so a reverse lookup to get a Resource Record
is not possible unless a node retrieves a full copy of the Resource Record index for the
whole network.

Temporal file identifiers are calculated with a component that changes over time -
progressive network entropy.  This means temporal file identifiers change over time for
all nodes in the network.  Access to the next progressive value required to generate
the new versions of temporal file identifiers is only available to node participants that
have a Proof of Stake (proof they have and can distribute content on request) and have
a Proof of Work (have been online and have distributed content in the past).  There are
no "blameless" observers - to gain access to the dynamic nature of Resource Records,
nodes must participate in the distribution of encrypted content.  Because it is
difficult to gain and continue to access Resource Records, there is repudiation
for distribution as loss of access means a node cannot identify the content it holds,
even if it did so in the past.

## Confidentiality

Mystiko is a distributed file system.  It is not browser-based, and does not depend on
a web browser securely displaying content provided by network participants, like
Freenet.  Unlike Tor, users do not navigate to pages where their devices may be
fingerprinted or locations reported through malicious Javascript beacons.  However,
drawing on inspiration from Freenet and Perfect Dark, Mystiko is a distributed encrypted
file system operating over a mixnet.

There is a concept of node identity and operator identity.  Nodes gain reputation by
Proof of Stake (posessing content) and Proof of Work (receiving and distributing content).
However, operator identity is optional and ultimately is represented only by a public key.
A goal of the project is to never disclose operator closeness or association with nodes
or content, unless an operator choses to explicitly associate themselves with content
they upload.

## Integrity

Distribution of chunks is a logged operation in a blockchain.  Node identities are
expensive to create, and must be mined.  Nodes that participate in a malicious fashion,
such as distributing superfulous or malformed chunks or such as leeching will have their
identities shunned by network participants.  The blockchain allows poisoning to be traced
and removed by nodes who have hold poisoned chunks.  The use of strong cryptographic hashes
allow multiple tiers of integrity validation.

## Availability

The network strives to make all information available to the whole network.  Unlike Freenet,
less popular content is not aged off the network: instead, nodes actively push content to
neighbors that have storage capacity and actively try to identify rare chunks and files and
reseed them into the network without user interaction.

## Performance

The network must be fast to:

1. Browse content by category or keyword
2. Retrieve content by identifier

## Resiliant 

Nodes must assume their neighbors are malicious threat actors that evesdrop, interrogate,
and maliciously seed the network as advanced persistent threats.  Further, the Mystiko
client must assume content can be destructive to anonymity.  Finally, nodes must assume
network operators aim to identify and limit or restrict their ability to communicate with
other nodes.  For these reasons:

1. Nodes build trust with other nodes over time in an anonymous, public blockchain ledger
2. Node operators can explicitly log their trust and distrust in the blockchain
3. Nodes who verify other nodes that have been distrusted themselves lose trust from the
   network (to prevent Sybil collusion attacks in trust gathering)
4. Nodes that leech or share content based on greylists (look busy with one another in a
   Sybil collusion while observing pass-thru traffic) will be distrusted or shunned.
5. Clients should attempt to warn or protect users from downloading executable content
6. To thwart malicious nodes that watch for node requests to discern queries that are not
   in line with a node's location, such as a user making a request for a specific file,
   nodes may make occasional, spurious requests for content that is not close to its
   location.  Unattended node behavior should appear similar to nodes behaving with an
   operator present and using their node for content retrieval 

Nodes may be restricted in how they can participate in the network with other nodes based
on accumulated reputation.  For instance:

1. New nodes may only be able to receive and send blocks, but not receive updates to the
   progressive network entropy, which is required to follow changes to temporal file
   identifiers over time
2. New nodes may not be able to retrieve Resource Records or query Resource Record indexes
3. Newer nodes may not be able to explicitly request chunks at all, whether user-generated
   retrieval requests or spurious observation-thwarting requests.
4. Neighbors may need to sign a node's newly-mined identity and may only be able to do
   so for so often.  Newer nodes may not yet have introduction priviledges.
5. Newer nodes may not be able to insert new files into the network until they have
   participated in a requisite amount of retrieval and distribution of chunks (if not
   whole files) as well

