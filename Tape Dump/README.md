This utility displays the contents of a SEL810 paper tape image.  Supported paper tape image types include: absolute loader, object (MNEMBLER), BASIC program, and raw bytes.

Absolute loader tape format is:
* some number of null bytes for the tape leader.
* an FF byte marking the start of a file.
* a 2-byte value which is the load address for the file.
* a 2-byte value which is the negative two's complement of the number of program words N on the tape.
* N 16-bit words of program text, displayed as a hex dump.
* a 16-bit checkum.  The checksum passes if it equals the modulo-65536 sum of program text words.
* some number of null bytes for the tape trailer.

Object (MNEMBLER) tape format is:
* some number of null bytes for the tape leader.
* some number of loader blocks, where each block contains:
* a block start marker (8D 8A FF, or 8D 8A 00 FF).
* 36 24-bit words of loader directives, displayed in hex and octal.
* a 16-bit checksum.  The checksum passes if the modulo-65536 sum of the block words and the checksum word are zero.

BASIC program tape format is:
* some number of null bytes for the tape leader.
* an FF byte marking the start of a file.
* a 2-byte value which is the negative two's complement of the number of program words N on the tape.
* N 16-bit words of program text, displayed as a BASIC program listing.
* a 16-bit checksum.  The checksum passes if the modulo-65536 sum of the program words and the checksum word are zero.

Raw tape format is displayed as a hex dump of paper tape image bytes.
