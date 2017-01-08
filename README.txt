= Mystiko =

== Intro ==

Mystiko is Greek for 'secret'.  Mystiko is a set of utilities aimed at keeping
information secure and confidential, making information available
psuedonymously, and retrieiving information anonymously or pseudonymously.

Mystiko is not a real-time streaming anonymization proxy, streaming network, or
onion router.  It is therefore not similar to or a replacement for I2P or Tor.
The closest functional analogs would be similar to the combined functionality of
encrypted containers in TrueCrypt/VeraCrypt, file splitting and joining
functions of HJSplit, and at some point in the future, file lookup and
distribution functionality similar to BitTorrent or Freenet.

== Programs ==

=== MPM ===

mpm - Mystiko Package Manager

(Output from Mystiko.File.Console project)

This program encrypts files and splits them into separate encrypted parts with
a manifest file.  To recombine the file, all output artifacts, including the
manifest file are required.  Usage parameters are as follows:

  -d, --decrypt    The path to a Mystiko manifest file to decrypt a package

  -e, --encrypt    The path to file to encrypt and package

  -h, --hash       The path to hash for a manifest output, without actually
                   creating split encrypted files

  -f, --force      Overwrite files if required

  -o, --output     If used with the --decrypt operation, specifies the path for
                   the unpackaged file

  -p, --pause      Pauses at the end of the operation

  -v, --verbose    Write verbose output

  -y, --verify     Perform extra verification checks on internal operations

  --help           Display this help screen.

==== How it works ====

MPM splits a file into multiple chunks and performs a SHA-512 hash on each
part.  The first 32 bytes of each chunk's hash are XOR'ed together to create
an AES-256 symmetric key.  Each chunk is then encrypted with this key to create
an encrypted block.  Each block is then hashed with SHA-512, and the hash of
all blocks as well as the encryption key are XOR'ed together to create an
'unlock' key.

A manifest file is created that includes some basic metadata of the file (name,
create dates, etc), the order of the blocks for joining and decrypting, and the
unlock key.

Without the manifest file, the blocks have no context (how many blocks there
are or the order of the blocks).  Without all the blocks and the manifest file,
no blocks can be decrypted since the unlock key must be XOR'ed with the hashes
of all encrypted parts.

The size of encrypted blocks are random so as to obscure which block is the
final one.  This behavior can be overridden with the '--size' parameter.

==== Known issues / coming improvmeents ====

Issue: It is possible to discern when any block is associated with the same
manifest file by comparing the hash blocks within the manifest, which could be
incriminating depending on the content and how oppresive the governing regime
of the user may be.

Response: This will soon be resolved by a coming feature that will XOR the
hash blocks in the manifest file with the last 32 bytes of all other encrypted
pieces.  This means that it requires all pieces plus the manifest file to
verify the association of a block to a manifest file.


Issue: It is possible to discern, by default operation, the name of a file that
blocks represent.

Response: This will soon be resolved by removing the base file name from
generated blocks.