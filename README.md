# SEL810
A SEL810 Emulator and related tools, written in C#

**Emulator**  
The Emulator includes the SEL810 processor, memory, and the Console Teletype (including paper tape reader and punch.)
The emulator also provides a text-based command interface that enables and/or simplifies tasks normally requiring
use of the front panel indicators and switches.

The emulator includes a TCP server that will accept a connection on port 8101 from a terminal emulator to
be used as an alternative Console Teletype keyboard and printer.

The emulator can connect as a TCP client to device servers that emulate other peripherals.  See for example
the Tape Server.

**Tape Server**  
The Tape Server emulates a high-speed paper tape reader and punch.  It functions as a TCP server and
accepts connections from the SEL810 Emulator.  By default it listens on port 8102.

For convenience it allows tapes to be selected by making use of unused bits in the CEU instruction.

The Tape Server also serves as a model for how new device emulators can be written.

**Tape Dump**  
The Tape Dump utility can be used to dump the content of paper tape image files.  It recognizes absolute
loader tapes, relocatable object loader tapes, and BASIC program tapes.

**Make BASIC**  
The Make BASIC utility can be used to create new BASIC program paper tapes, or to modify existing ones.
