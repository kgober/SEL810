# SEL810 Emulator

This is a simple emulator of a SEL810 system.  It is a work in progress, and currently **lacks** support for:
* Power Fail Safe interrupt
* special interrupt card
* Block Transfer Control (BTC) units
* Program Protect and Instruction Trap (Option 81-080B)
* Stall Alarm (Option 81-043B)
* Auto Start (Option 81-041B)
* Input/Output Parity (Option 81-210B)
* 60Hz Real-Time Clock (Option 81-031B)
* graphical front panel
* built-in peripherals other than the Console Teletype (including its paper tape reader/punch)

The emulator listens on TCP port 8101 for a TCP connection from a terminal
emulator that may used as an alternative Console Teletype keyboard and printer.

Peripherals other than the Console Teletype may be attached via TCP, with the
emulator acting as a client connecting to a device server.  See "Tape Server"
in this repository for an example.

### Command-Line Options
-g1 - enable JSON-based GUI protocol as used by https://github.com/emooreatx/SEL810GUI  
*statefile* - preload registers and memory from *statefile*

### Emulator Commands
a [*value*] - display A accumulator (or set if *value* given)  
b [*value*] - display B accumulator (or set if *value* given)  
c [*mode*] - display console output mode (or set if value given)  
d [*addr*] - dump 8 words at *addr* (press *Enter* to continue)  
e [*addr*] *op* [*arg*] - enter instruction at *addr*  
f - force ready (release I/O hold)  
g [*addr*] - start CPU (setting PC to *addr* if given)  
h - halt CPU  
ir [*value*] - display instruction register (or set if *value* given)  
i *filename* - read console paper tape input from *filename*  
k *input* - queue *input* as if had been typed at console keyboard  
l [*addr*] *filename* - load memory from *filename* at *addr* (default 0)  
mc - master clear  
n *unit* *hostname:port* - attach device *unit* via network  
o *filename* - write console paper tape output to *filename*  
pc [*value*] - display program counter (or set if *value* given)  
q [*statefile*] - exit emulator, optionally saving state to *statefile*  
r - display registers  
s - single-step CPU  (press *Enter* to continue)  
t [*value*] - display sense toggle switches (or set if *value* given)  
u [*addr*] - display instruction at *addr* (press *Enter* to continue)  
v - toggle verbose mode (shows OVF and IOH indicators)  
w *addr* *len* *filename* - write *len* words at *addr* to *filename*  
= [*addr*] *value* - write *value* to memory at *addr* (press *Enter* to continue)  
. [*addr* [*count*]] - set a read breakpoint at *addr*  
! [*addr* [*count*]] - set a write breakpoint at *addr*  
*reg*+ *value* - set a breakpoint on *reg* = *value*  
*reg*- *value* - clear a breakpoint on *reg* = *value*  
*reg*? - display breakpoints on *reg*  

*value* - a 16-bit value (prefix octal values with ' or o, hex with x)  
*addr* - a 15-bit address (prefix octal values with ' or o, hex with x)  
*mode*  
1 = printer  
2 = punch  
3 = both  
*input* - a sequence of characters.  permitted escapes:  
\\ - CR (^M) if given as the last character of *input*  
\\\\ - backslash (\\)  
\\a - BEL (^G)  
\\b - BS (^H)  
\\e - ESC (^\[)  
\\f - FF (^L)  
\\n - LF (^J)  
\\o*ddd* - a byte value given in octal  
\\r - CR (^M)  
\\t - TAB (^I)  
\\x*dd* - a byte value given in hexadecimal  
\\*ddd* - a byte value given in decimal  
\\*C* - any other escaped character *C* represents itself  

### Examples
Start the emulator and 'toggle in' the bootstrap at location 0:
> C:\\>**Emulator.exe**  
> 810A>**= 0 '130101**  
> 00000=b041/130101  00001:0000/000000  >**= '4000**  
> 00001=0800/004000  00002:0000/000000  >**= '170301**  
> 00002=f0c1/170301  00003:0000/000000  >**= '22**  
> ...  
> 00016=8fb9/107671  00017:0000/000000  >**= '7673**  
> 00017=0fbb/007673  00020:0000/000000  >  

(Continued) Load a paper tape and execute the bootstrap:
> 00017=0fbb/007673  00020:0000/000000  >**input loader.bin**  
> [+RDR]810A>**pc 0**  
> PC:0000/00000  IR:000000  HLT  
> 810A>**go**  
> 810A>[HALT]**go**  
> 810A>````````````````...  

Note the need to 'go' twice, similarly to how on the real hardware you must depress the Start switch twice.  This is because loading PC with a new value does not also automatically load the instruction register IR (which still contains a HLT instruction).  The first Start executes the HLT in the IR, then loads IR from the location pointed to by the PC.  The second Start will proceed normally.
