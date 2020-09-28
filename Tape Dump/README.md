This utility displays a SEL810 BASIC program from a paper tape image.

The tape image format is:
* some number of null bytes for the tape leader.
* a 3-byte value which is the negative two's complement of the number of program words on the tape N.
* N 16-bit words of program text (in big-endian order, MSB first)
* a 16-bit checksum value.  If the modulo-65536 sum of program text words and checksum is 0, the checksum is good.

The format of a BASIC program is a sequence of lines.  Each line contains:
* 1 word: line number
* 1 word: length (includes line number and line length words)
* length-2 words: tokens representing BASIC keywords, variables, etc.
