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
* built-in peripherals other than the Console Teletype paper tape reader/punch

The emulator listens on TCP port 8101 for a TCP connection from a terminal
emulator to be used as the Console Teletype keyboard and printer.

Peripherals other than the Console Teletype may be attached via TCP, with the
emulator acting as a client connecting to a device server.  See "Tape Server"
in this repository for an example.

#### Examples
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
