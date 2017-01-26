# Mystiko

## Intro

Mystiko is Greek for 'secret'.  Mystiko is a set of utilities aimed at keeping
information secure and confidential, making information available
psuedonymously, and retrieiving information anonymously or pseudonymously.

Mystiko is not a real-time streaming anonymization proxy, streaming network, or
onion router.  It is therefore not similar to or a replacement for I2P or Tor.
The closest functional analogs would be similar to the combined functionality of
encrypted containers in TrueCrypt/VeraCrypt, file splitting and joining
functions of HJSplit, and at some point in the future, file lookup and
distribution functionality similar to BitTorrent or Freenet.

## Programs

### MPM

mpm - Mystiko Package Manager

(Output from Mystiko.File.Console project)

This program encrypts files and splits them into separate encrypted parts with
a manifest file.  To recombine the file, all output artifacts, including the
manifest file are required.  Usage parameters are as follows:

Switch                 | Function
------                 | --------
`-d`, `--decrypt`      | OPERATION: Decrypts a Mystiko split file set
`-e, --encrypt`        | OPERATION: Encrypts a file into split files and a manifest
`-h, --hash`           | OPERATION: Prepares a manifest, but does not create split files
`-c, --createFromHash` | OPERATION: Creates split files from a prepared manifest
`-s, --source`         | The source file to encrypt or decrypt
`-m, --manifest`       | The manifest file to use to decrypt or createFromHash
`-f, --force`          | Overwrite files if required
`-o, --output`         | If used with the --decrypt operation, specifies the path for the unpackaged file
`-p, --pause`          | Pauses at the end of the operation
`-v, --verbose`        | Write verbose output
`-y, --verify`         | Perform extra verification checks on internal operations
`--help`               | Display this help screen.

#### How it works

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

#### Example usage

To encrypt a file into split parts and a manifest:

`mpm.exe -e -s "C:\Downloads\secret.file" -f -p -v -y`


To decrypt a file from its split parts and manifest:

`mpm.exe -d -m "C:\Downloads\secret.file.mystiko" -f -p -v -y`


To decrypt a file from its split parts and manifest to a specific path:

`mpm.exe -d -m "C:\Downloads\secret.file.mystiko" -o "C:\Downloads\secret.file.rebuilt" -f -p -v -y`


To create a special 'local' manifest file of a file without actually creating the encrypted split files:

`mpm.exe -h -s "C:\Downloads\secret.file" -m "C:\Downloads\secret.file.mystiko2" -f -p -v -y`


To create a split parts from a pre-calculated 'local' manifest:

`mpm.exe -c -s "C:\Downloads\secret.file" -m "C:\Downloads\secret.file.mystiko2" -p -v -y`


#### Split file (chunking) process

1. Assess the size of the file
   * Split files in chunks of random sizes between 1 MB and 10 MB, unless the file is large, in which case the chunk size will be randomly between 	 
	 10^(\lfloor log_{10}fileSizeBytes \rfloor - 2) 
	 and 
	 10^(\lfloor log_{10}fileSizeBytes \rfloor - 1)

2. Perform SHA-512 hashes of each chunk

3. Generate a symmetric encryption key from the RNG cryptographic provider

4. XOR together the first 32 bytes of each chunk's hash with the symmetric encryption key.  This forms the 'unlock key'

5. Create the split files chunks and encrypt each chunk with the encryption key using AES-256

6. Create the manifest file, which consists of the SHA-512 hashes of all chunks, which provides the order of the files,
   along with the unlock key.  This ensures to recover the encryption key, one must have all the chunks of the file and
   the manifest to recover the key.  Encryption key recovery is an XOR of all chunk portions

7. To ensure that one has all chunks to recover the encryption key, before the manifest is committed to disk, the hash
   of each chunk in the manifest is XOR'ed with the last 32 bytes of each actual encrypted chunk other than the one that
   hash represents.

   For example, a file is split and encrypted into chunks C1, C2, and C3.
   The manifest contains the hash for C1 XOR'ed with the last 32 bytes of C2 and C3,
   the hash for C2 XOR'ed with the last 32 bytes of C1 and C3, and
   the hash for C3 XOR'ed with the last 32 bytes of C1 and C2.

   This prevents a manifest owner from discerning the key from chunk hashes and the unlock key in the manifest alone - 
   the tails of split chunk files are required as well.

The decryption process works in reverse: the manifest file contains the order of hashes to reconstruct the file.
These hashes are XOR'ed together with the unlock key in the manifest to recover the decryption key.  Chunks are
unencrypted and reconstituted into the resulting file, by default with the .decrypted suffix applied to the filename.


#### Known issues / coming improvements

**Issue:** It is possible to discern, by default operation, the name of a file that
blocks represent.

**Response:** This will soon be resolved by removing the base file name from
generated blocks.
