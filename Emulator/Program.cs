// Program.cs
// Copyright © 2020 Kenneth Gober
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.


// To Do:
// * Auto Start: see p.64 of http://vtda.org/docs/computing/SEL/SEL_810ASchool_1971.pdf
// * Compact state file (omit trailing nulls)
// * include more state in state file
// * SEL810A vs SEL810B differences
// * use stdout only for console printer output
// * use ':' to display memory or registers


using System;
using System.IO;

namespace Emulator
{
    class Program
    {
        static public Boolean VERBOSE = false;

        static Char[] WHITESPACE = new Char[] { ' ', '\t' };
        static String AUTO_CMD = String.Empty;
        static SEL810 CPU = new SEL810();

        static void Main(String[] args)
        {
            Int32 ap = 0;
            while (ap < args.Length)
            {
                String arg = args[ap++];
                if ((arg == null) || (arg.Length == 0))
                {
                    continue;
                }
                else if (arg[0] == '-')
                {
                    if (arg.Length == 1)
                    {
                        // - by itself, ignore this for now
                    }
                    else if ((arg[1] == 'g') || (arg[1] == 'G'))
                    {
                        arg = arg.Substring(2);
                        if (arg.Length == 0) arg = args[ap++];
                        CPU.SetGUIProtocol(Int32.Parse(arg));
                    }
                    else
                    {
                        // unrecognized option, ignore for now
                    }
                }
                else
                {
                    LoadState(arg);
                }
            }

            Console.Out.Write("810A>");
            String cmd;
            while ((cmd = Console.In.ReadLine()) != null)
            {
                UInt16 word;
                String arg = String.Empty;
                Int32 p = cmd.IndexOfAny(WHITESPACE);
                if (p != -1)
                {
                    arg = cmd.Substring(p + 1);
                    cmd = cmd.Substring(0, p);
                    while (arg.IndexOfAny(WHITESPACE) == 0) arg = arg.Substring(1);
                }
                if (cmd.Length == 0) cmd = AUTO_CMD;
                if (cmd.Length == 0)
                {
                    // do nothing
                }
                else if (cmd == "?")
                {
                    Console.Out.WriteLine("Commands:");
                    Console.Out.WriteLine("? - display this text");
                    Console.Out.WriteLine("a [val] - display or set A accumulator");
                    Console.Out.WriteLine("b [val] - display or set B accumulator");
                    Console.Out.WriteLine("c[onsole] [mode] - display or set console mode");
                    Console.Out.WriteLine("d[ump] [addr] - dump 8 words at 'addr' (Enter to continue)");
                    Console.Out.WriteLine("e[nter] [addr] op [arg] - enter instruction at 'addr' (Enter to continue)");
                    Console.Out.WriteLine("f[orce] - force ready (release I/O hold)");
                    Console.Out.WriteLine("g[o] [addr] - start CPU (at addr if specified)");
                    Console.Out.WriteLine("h[alt] - halt CPU");
                    Console.Out.WriteLine("ir [val] - display or set Instruction Register");
                    Console.Out.WriteLine("i[nput] filename - read paper tape input from 'filename'");
                    Console.Out.WriteLine("k[eys] input - queue input as if it had been typed at console keyboard");
                    Console.Out.WriteLine("l[oad] [addr] filename - load memory from 'filename' at 'addr' (default 0)");
                    Console.Out.WriteLine("mc - master clear (clears all registers)");
                    Console.Out.WriteLine("n[etwork] unit hostname:port - attach 'unit' via network");
                    Console.Out.WriteLine("o[utput] filename - write paper tape output to 'filename'");
                    Console.Out.WriteLine("pc [val] - display or set Program Counter");
                    Console.Out.WriteLine("q[uit] [filename] - exit emulator, optionally saving state to 'filename'");
                    Console.Out.WriteLine("r[egisters] - display registers");
                    Console.Out.WriteLine("s[tep] - single step CPU (Enter to continue)");
                    Console.Out.WriteLine("t[oggle] [val] - display or set sense switches");
                    Console.Out.WriteLine("u[nassemble] [addr] - display instruction at 'addr' (Enter to continue)");
                    Console.Out.WriteLine("v[erbose] - toggle verbose mode (shows OVF and IOH indicators)");
                    Console.Out.WriteLine("w[rite] addr len filename - write 'len' words at 'addr' to 'filename'");
                    Console.Out.WriteLine("= [addr] val - write 'val' to 'addr' (Enter to continue)");
                    Console.Out.WriteLine(". [addr [count]] - set a read breakpoint at 'addr'");
                    Console.Out.WriteLine("! [addr [count]] - set a write breakpoint at 'addr'");
                    Console.Out.WriteLine("<reg>+ val - set a breakpoint on <reg> = 'val'");
                    Console.Out.WriteLine("<reg>- val - clear a breakpoint on <reg> = 'val'");
                    Console.Out.WriteLine("<reg>? - display breakpoints on <reg>");
                }
                else if (cmd.EndsWith("+"))
                {
                    cmd = cmd.Substring(0, cmd.Length - 1);
                    String reg = cmd.ToUpper();
                    switch (reg)
                    {
                        case "A": p = 0; break;
                        case "B": p = 1; break;
                        case "IR": p = 2; break;
                        case "PC": p = 3; break;
                        default: p = -1; break;
                    }
                    if (p == -1)
                    {
                        Console.Out.WriteLine("Unrecognized register: {0}", cmd);
                    }
                    else if (!ParseWord(arg, out word))
                    {
                        Console.Out.WriteLine("Unrecognized value: {0}", arg);
                    }
                    else if ((p == 3) && (word >= 0x8000))
                    {
                        Console.Out.WriteLine("Invalid PC value: {0}", arg);
                    }
                    else
                    {
                        CPU.SetBPReg(p, word);
                    }
                }
                else if (cmd.EndsWith("-"))
                {
                    cmd = cmd.Substring(0, cmd.Length - 1);
                    String reg = cmd.ToUpper();
                    switch (reg)
                    {
                        case "A": p = 0; break;
                        case "B": p = 1; break;
                        case "IR": p = 2; break;
                        case "PC": p = 3; break;
                        default: p = -1; break;
                    }
                    if (p == -1)
                    {
                        Console.Out.WriteLine("Unrecognized register: {0}", cmd);
                    }
                    else if (!ParseWord(arg, out word))
                    {
                        Console.Out.WriteLine("Unrecognized value: {0}", arg);
                    }
                    else if ((p == 3) && (word >= 0x8000))
                    {
                        Console.Out.WriteLine("Invalid PC value: {0}", arg);
                    }
                    else
                    {
                        CPU.ClearBPReg(p, word);
                    }
                }
                else if (cmd.EndsWith("?"))
                {
                    cmd = cmd.Substring(0, cmd.Length - 1);
                    String reg = cmd.ToUpper();
                    switch (reg)
                    {
                        case "A": p = 0; break;
                        case "B": p = 1; break;
                        case "IR": p = 2; break;
                        case "PC": p = 3; break;
                        default: p = -1; break;
                    }
                    if (p == -1)
                    {
                        Console.Out.WriteLine("Unrecognized register: {0}", cmd);
                    }
                    else
                    {
                        Int32 lim = (p == 3) ? 32768 : 65536;
                        Int32 wid = (p == 3) ? 5 : 6;
                        Int32 ct = 0;
                        for (Int32 i = 0; i < lim; i++)
                        {
                            if (!CPU.GetBPReg(p, (UInt16)(i))) continue;
                            Console.Out.WriteLine("{0} BP: {1:x4}/{2}", reg, i, Octal(i, wid));
                            ct++;
                        }
                        if (ct == 0) Console.Out.WriteLine("{0} BP: none set", reg);
                    }
                }
                else if (cmd == "a")
                {
                    if (arg.Length == 0)
                    {
                        Console.Out.WriteLine("A:{0:X4}/{1}", CPU.A, Octal(CPU.A, 6));
                    }
                    else if (!ParseWord(arg, out word))
                    {
                        Console.Out.WriteLine("Unrecognized: {0}", arg);
                    }
                    else
                    {
                        CPU.A = word;
                        Console.Out.WriteLine("A={0:X4}/{1}", CPU.A, Octal(CPU.A, 6));
                    }
                }
                else if (cmd == "b")
                {
                    if (arg.Length == 0)
                    {
                        Console.Out.WriteLine("B:{0:X4}/{1}", CPU.B, Octal(CPU.B, 6));
                    }
                    else if (!ParseWord(arg, out word))
                    {
                        Console.Out.WriteLine("Unrecognized: {0}", arg);
                    }
                    else
                    {
                        CPU.B = word;
                        Console.Out.WriteLine("B={0:X4}/{1}", CPU.B, Octal(CPU.B, 6));
                    }
                }
                else if (cmd[0] == 'c') // console
                {
                    if (arg.Length == 0)
                    {
                        Console.Out.Write("Console Mode: ");
                        switch (CPU.ConsoleMode)
                        {
                            case Teletype.Mode.Printer: Console.Out.WriteLine("1=printer"); break;
                            case Teletype.Mode.Punch: Console.Out.WriteLine("2=punch"); break;
                            case Teletype.Mode.Both: Console.Out.WriteLine("3=both (printer and punch)"); break;
                            default: Console.Out.WriteLine("? unknown ({0:D0})", CPU.ConsoleMode); break;
                        }
                    }
                    else if ((arg == "1") || (arg == "printer"))
                    {
                        CPU.ConsoleMode = Teletype.Mode.Printer;
                    }
                    else if ((arg == "2") || (arg == "punch"))
                    {
                        CPU.ConsoleMode = Teletype.Mode.Punch;
                    }
                    else if ((arg == "3") || (arg == "both"))
                    {
                        CPU.ConsoleMode = Teletype.Mode.Both;
                    }
                    else
                    {
                        Console.Out.WriteLine("Unrecognized: {0}", arg);
                    }
                }
                else if (cmd[0] == 'd') // dump
                {
                    Dump(arg);
                    continue;
                }
                else if (cmd[0] == 'e') // enter
                {
                    Enter(arg);
                    continue;
                }
                else if (cmd[0] == 'f') // force
                {
                    CPU.ReleaseIOHold();
                }
                else if (cmd[0] == 'g') // go
                {
                    if (arg.Length == 0)
                    {
                        CPU.Run();
                    }
                    else if (!ParseWord(arg, out word))
                    {
                        Console.Out.WriteLine("Unrecognized: {0}", arg);
                    }
                    else
                    {
                        CPU.PC = word;
                        CPU.Run();
                    }
                }
                else if (cmd[0] == 'h') // halt
                {
                    CPU.Halt();
                }
                else if (cmd == "ir")
                {
                    if (arg.Length == 0)
                    {
                        Console.Out.WriteLine("IR:{0:X4}/{1}  {2}", CPU.IR, Octal(CPU.IR, 6), Decode(CPU.PC, CPU.IR));
                    }
                    else if (!ParseWord(arg, out word))
                    {
                        Console.Out.WriteLine("Unrecognized: {0}", arg);
                    }
                    else
                    {
                        CPU.IR = word;
                        Console.Out.WriteLine("IR={0:X4}/{1}  {2}", CPU.IR, Octal(CPU.IR, 6), Decode(CPU.PC, CPU.IR));
                    }
                }
                else if (cmd[0] == 'i') // input
                {
                    if ((arg.Length != 0) && (!File.Exists(arg))) Console.Out.WriteLine("File not found: {0}", arg);
                    else CPU.SetReader(arg);
                }
                else if (cmd[0] == 'k') // keys
                {
                    Keys(arg);
                }
                else if (cmd[0] == 'l') // load
                {
                    if ((p = arg.IndexOfAny(WHITESPACE)) == -1) // TODO: handle no addr, with a filename with whitespace in it
                    {
                        word = 0;
                    }
                    else
                    {
                        if (!ParseWord(arg.Substring(0, p), out word))
                        {
                            word = 0;
                        }
                        else
                        {
                            arg = arg.Substring(p + 1);
                            while (arg.IndexOfAny(WHITESPACE) == 0) arg = arg.Substring(1);
                        }
                    }
                    if (!File.Exists(arg)) Console.Out.WriteLine("File not found: {0}", arg);
                    else CPU.Load(word, arg);
                }
                else if (cmd == "mc")
                {
                    CPU.MasterClear();
                    Console.Out.WriteLine("A={0:X4}/{1}  B={2:X4}/{3}  T={4:X4}/{5}", CPU.A, Octal(CPU.A, 6), CPU.B, Octal(CPU.B, 6), CPU.T, Octal(CPU.T, 6));
                    Console.Out.WriteLine("PC={0:X4}/{1}  IR={2:X4}/{3}  {4}", CPU.PC, Octal(CPU.PC, 5), CPU.IR, Octal(CPU.IR, 6), Decode(CPU.PC, CPU.IR));
                }
                else if (cmd[0] == 'n') // network
                {
                    if ((p = arg.IndexOfAny(WHITESPACE)) == -1) p = arg.Length;
                    if (!ParseWord(arg.Substring(0, p), out word))
                    {
                        Console.Out.WriteLine("Unrecognized unit number: {0}", arg.Substring(0, p));
                    }
                    else if ((word < 2) || (word > 63))
                    {
                        Console.Out.WriteLine("Invalid unit number: {0:D0}", word);
                    }
                    else
                    {
                        arg = arg.Substring(p);
                        while (arg.IndexOfAny(WHITESPACE) == 0) arg = arg.Substring(1);
                        CPU.AttachDevice(word, arg);
                    }
                }
                else if (cmd[0] == 'o') // output
                {
                    CPU.SetPunch(arg);
                }
                else if (cmd == "pc")
                {
                    if (arg.Length == 0)
                    {
                        Console.Out.WriteLine("PC:{0:X4}/{1}", CPU.PC, Octal(CPU.PC, 5));
                    }
                    else if (!ParseWord(arg, out word))
                    {
                        Console.Out.WriteLine("Unrecognized: {0}", arg);
                    }
                    else
                    {
                        CPU.PC = word;
                        Console.Out.WriteLine("PC={0:X4}/{1}", CPU.PC, Octal(CPU.PC, 5));
                    }
                }
                else if (cmd[0] == 'q') // quit
                {
                    CPU.Halt();
                    if (arg.Length != 0) SaveState(arg);
                    CPU.Exit();
                    break;
                }
                else if (cmd[0] == 'r') // registers
                {
                    word = CPU.PC;
                    if ((CPU[word] != CPU.IR) && (CPU[word - 1] == CPU.IR)) word--;
                    Console.Out.WriteLine("A:{0:X4}/{1}  B:{2:X4}/{3}  T:{4:X4}/{5}", CPU.A, Octal(CPU.A, 6), CPU.B, Octal(CPU.B, 6), CPU.T, Octal(CPU.T, 6));
                    Console.Out.WriteLine("PC:{0:X4}/{1}  IR:{2:X4}/{3}  {4}", CPU.PC, Octal(CPU.PC, 5), CPU.IR, Octal(CPU.IR, 6), Decode(word, CPU.IR));
                }
                else if (cmd[0] == 's') // step
                {
                    Step(arg);
                    continue;
                }
                else if (cmd[0] == 't') // toggle
                {
                    if (arg.Length == 0)
                    {
                        Console.Out.WriteLine("SR:{0:X4}/{1}", CPU.SR, Octal(CPU.SR, 6));
                    }
                    else if (!ParseWord(arg, out word))
                    {
                        Console.Out.WriteLine("Unrecognized: {0}", arg);
                    }
                    else
                    {
                        CPU.SR = word;
                        Console.Out.WriteLine("SR={0:X4}/{1}", CPU.SR, Octal(CPU.SR, 6));
                    }
                }
                else if (cmd[0] == 'u') // unassemble
                {
                    Disassemble(arg);
                    continue;
                }
                else if (cmd[0] == 'v') // verbose
                {
                    VERBOSE = !VERBOSE;
                }
                else if (cmd[0] == 'w') // write
                {
                    Save(arg);
                }
                else if (cmd[0] == '=') // write word
                {
                    Write(arg);
                    continue;
                }
                else if (cmd[0] == '.') // read breakpoint
                {
                    ReadBP(arg);
                }
                else if (cmd[0] == '!') // write breakpoint
                {
                    WriteBP(arg);
                }
                Console.Out.Write("810A>");
                AUTO_CMD = String.Empty;
            }
        }

        // TODO: include OVF, CF, X, XP, PPR, VBR, maybe breakpoints?
        static public void LoadState(String fileName)
        {
            Byte[] buf = File.ReadAllBytes(fileName);
            Int32 p = 0, q = 0;
            CPU.PC = BitConverter.ToUInt16(buf, p);
            CPU.IR = BitConverter.ToUInt16(buf, p += 2);
            CPU.A = BitConverter.ToUInt16(buf, p += 2);
            CPU.B = BitConverter.ToUInt16(buf, p += 2);
            CPU.T = BitConverter.ToUInt16(buf, p += 2);
            CPU.SR = BitConverter.ToUInt16(buf, p += 2);
            while ((p += 2) < buf.Length)
            {
                CPU[q++] = BitConverter.ToUInt16(buf, p);
            }
        }

        static public void SaveState(String fileName)
        {
            FileStream f = new FileStream(fileName, FileMode.Create, FileAccess.Write);
            Byte[] PC = BitConverter.GetBytes(CPU.PC);
            Byte[] IR = BitConverter.GetBytes(CPU.IR);
            Byte[] A = BitConverter.GetBytes(CPU.A);
            Byte[] B = BitConverter.GetBytes(CPU.B);
            Byte[] T = BitConverter.GetBytes(CPU.T);
            Byte[] SR = BitConverter.GetBytes(CPU.SR);
            f.Write(PC, 0, 2);
            f.Write(IR, 0, 2);
            f.Write(A, 0, 2);
            f.Write(B, 0, 2);
            f.Write(T, 0, 2);
            f.Write(SR, 0, 2);
            for (Int32 i = 0; i < SEL810.CORE_SIZE; i++)
            {
                Byte[] buf = BitConverter.GetBytes(CPU[i]);
                f.Write(buf, 0, 2);
            }
            f.Close();
        }

        static public void Save(String arg)
        {
            Int32 p;
            UInt16 addr, len;
            if ((p = arg.IndexOfAny(WHITESPACE)) == -1)
            {
                Console.Out.WriteLine("Must specify address, word count, and filename");
                return;
            }
            if (!ParseWord(arg.Substring(0, p), out addr))
            {
                Console.Out.WriteLine("Unrecognized address: {0}", arg.Substring(0, p));
                return;
            }
            if (addr >= 0x8000)
            {
                Console.Out.WriteLine("Invalid address: {0}", arg.Substring(0, p));
                return;
            }
            arg = arg.Substring(p + 1);
            while (arg.IndexOfAny(WHITESPACE) == 0) arg = arg.Substring(1);
            if ((p = arg.IndexOfAny(WHITESPACE)) == -1)
            {
                Console.Out.WriteLine("Must specify address, word count, and filename");
                return;
            }
            if (!ParseWord(arg.Substring(0, p), out len))
            {
                Console.Out.WriteLine("Unrecognized word count: {0}", arg.Substring(0, p));
                return;
            }
            if ((addr + len) > 0x8000)
            {
                Console.Out.WriteLine("Invalid word count: {0}", arg.Substring(0, p));
                return;
            }
            arg = arg.Substring(p + 1);
            while (arg.IndexOfAny(WHITESPACE) == 0) arg = arg.Substring(1);
            if (arg.Length == 0)
            {
                Console.Out.WriteLine("Must specify address, word count, and filename");
                return;
            }
            FileStream f = new FileStream(arg, FileMode.Create, FileAccess.Write);
            for (Int32 i = 0; i < len; i++)
            {
                UInt16 word = CPU[addr + i];
                f.WriteByte((Byte)((word >> 8) & 0xff));
                f.WriteByte((Byte)(word & 0xff));
            }
            f.Close();
        }

        static public void Keys(String arg)
        {
            Int32 s = 0;
            Int32 p = 0;
            Int32 n = 0;
            while (p < arg.Length)
            {
                Char c = arg[p++];
                switch (s)
                {
                    case 0: // normal keys
                        if (c == '\\')
                        {
                            s = 1;
                        }
                        else
                        {
                            CPU.TTY_KeyIn(c);
                        }
                        break;

                    case 1: // escape sequence
                        if (c == 'a')
                        {
                            s = 0;
                            CPU.TTY_KeyIn('\a');
                        }
                        else if (c == 'b')
                        {
                            s = 0;
                            CPU.TTY_KeyIn('\b');
                        }
                        else if (c == 'e')
                        {
                            s = 0;
                            CPU.TTY_KeyIn((Char)(27));
                        }
                        else if (c == 'f')
                        {
                            s = 0;
                            CPU.TTY_KeyIn('\f');
                        }
                        else if (c == 'n')
                        {
                            s = 0;
                            CPU.TTY_KeyIn('\n');
                        }
                        else if (c == 'o')
                        {
                            n = 0;
                            s = 3;
                        }
                        else if (c == 'r')
                        {
                            s = 0;
                            CPU.TTY_KeyIn('\r');
                        }
                        else if (c == 't')
                        {
                            s = 0;
                            CPU.TTY_KeyIn('\t');
                        }
                        else if (c == 'x')
                        {
                            n = 0;
                            s = 4;
                        }
                        else if ((c >= '0') && (c <= '9'))
                        {
                            n = RadixValue(c, 10);
                            s = 2;
                        }
                        else
                        {
                            s = 0;
                            CPU.TTY_KeyIn(c);
                        }
                        break;

                    case 2: // decimal literal
                        if ((c >= '0') && (c <= '9') && ((n * 10) < 255))
                        {
                            n = (n * 10) + RadixValue(c, 10);
                        }
                        else
                        {
                            s = 0;
                            CPU.TTY_KeyIn((Char)(n));
                            CPU.TTY_KeyIn(c);
                        }
                        break;

                    case 3: // octal literal
                        if ((c >= '0') && (c <= '7') && ((n * 8) < 255))
                        {
                            n = (n * 8) + RadixValue(c, 8);
                        }
                        else
                        {
                            s = 0;
                            CPU.TTY_KeyIn((Char)(n));
                            CPU.TTY_KeyIn(c);
                        }
                        break;

                    case 4: // hex literal
                        if ((c >= '0') && (c <= '9') && ((n * 16) < 255))
                        {
                            n = (n * 16) + RadixValue(c, 16);
                        }
                        else if ((c >= 'A') && (c <= 'F') && ((n * 16) < 255))
                        {
                            n = (n * 16) + RadixValue(c, 16);
                        }
                        else if ((c >= 'a') && (c <= 'f') && ((n * 16) < 255))
                        {
                            n = (n * 16) + RadixValue(c, 16);
                        }
                        else
                        {
                            s = 0;
                            CPU.TTY_KeyIn((Char)(n));
                            CPU.TTY_KeyIn(c);
                        }
                        break;
                }
            }

            if (s == 1)
            {
                CPU.TTY_KeyIn('\r');
            }
            else if (s > 1)
            {
                CPU.TTY_KeyIn((Char)(n));
            }
        }

        static public void ReadBP(String arg)
        {
            Int32 p;
            UInt16 addr;
            Int16 ct;
            if (arg.Length == 0)
            {
                Int32 n = 0;
                for (UInt16 i = 0; i < SEL810.CORE_SIZE; i++)
                {
                    if ((ct = CPU.GetBPR(i)) == 0) continue;
                    Console.Out.WriteLine("Read BP: {0:x4}/{1} {2}", i, Octal(i, 5), (ct < 0) ? "*" : ct.ToString());
                    n++;
                }
                if (n == 0) Console.Out.WriteLine("Read BP: none set");
                return;
            }
            if ((p = arg.IndexOfAny(WHITESPACE)) == -1)
            {
                ct = -1;
                if (!ParseWord(arg, out addr))
                {
                    Console.Out.WriteLine("Unrecognized addr: {0}", arg);
                    return;
                }
            }
            else
            {
                if (!ParseWord(arg.Substring(0, p), out addr))
                {
                    Console.Out.WriteLine("Unrecognized addr: {0}", arg.Substring(0, p));
                    return;
                }
                arg = arg.Substring(p + 1);
                while (arg.IndexOfAny(WHITESPACE) == 0) arg = arg.Substring(1);
                if (arg.Length == 0)
                {
                    ct = -1;
                }
                else if (!ParseWord(arg, out ct))
                {
                    Console.Out.WriteLine("Unrecognized word count: {0}", arg.Substring(0, p));
                    return;
                }
            }
            CPU.SetBPR(addr, ct);
        }

        static public void WriteBP(String arg)
        {
            Int32 p;
            UInt16 addr;
            Int16 ct;
            if (arg.Length == 0)
            {
                Int32 n = 0;
                for (UInt16 i = 0; i < SEL810.CORE_SIZE; i++)
                {
                    if ((ct = CPU.GetBPW(i)) == 0) continue;
                    Console.Out.WriteLine("Write BP: {0:x4}/{1} {2}", i, Octal(i, 5), (ct < 0) ? "*" : ct.ToString());
                    n++;
                }
                if (n == 0) Console.Out.WriteLine("Write BP: none set");
                return;
            }
            if ((p = arg.IndexOfAny(WHITESPACE)) == -1)
            {
                ct = -1;
                if (!ParseWord(arg, out addr))
                {
                    Console.Out.WriteLine("Unrecognized addr: {0}", arg);
                    return;
                }
            }
            else
            {
                if (!ParseWord(arg.Substring(0, p), out addr))
                {
                    Console.Out.WriteLine("Unrecognized addr: {0}", arg.Substring(0, p));
                    return;
                }
                arg = arg.Substring(p + 1);
                while ((arg.Length != 0) && (arg[0] == ' ')) arg = arg.Substring(1);
                if (arg.Length == 0)
                {
                    ct = -1;
                }
                else if (!ParseWord(arg, out ct))
                {
                    Console.Out.WriteLine("Unrecognized word count: {0}", arg.Substring(0, p));
                    return;
                }
            }
            CPU.SetBPW(addr, ct);
        }

        static UInt16 DUMP_ADDR = 0;
        static public void Dump(String arg)
        {
            UInt16 p;
            if (arg.Length == 0) p = (AUTO_CMD == "dump") ? DUMP_ADDR : CPU.PC;
            else if (!ParseWord(arg, out p))
            {
                Console.Out.WriteLine("Unrecognized: {0}", arg);
                Console.Out.Write("810A>");
                AUTO_CMD = String.Empty;
                return;
            }
            Console.Out.Write("{0}  ", Octal(p, 5));
            for (Int32 i = 0; i < 8; i++)
            {
                Console.Out.Write(" {0}", Octal(CPU[(p + i) % 32768], 6));
            }
            Console.Out.Write("  >");
            AUTO_CMD = "dump";
            DUMP_ADDR = (UInt16)((p + 8) % 32768);
        }

        static UInt16 ENTER_ADDR = 0;
        static public void Enter(String arg)
        {
            Int32 p;
            UInt16 addr;
            if (arg.Length == 0)
            {
                Console.Out.WriteLine("Must specify instruction");
                Console.Out.Write("810A>");
                AUTO_CMD = String.Empty;
                return;
            }
            if ((p = arg.IndexOfAny(WHITESPACE)) == -1)
            {
                if (ParseWord(arg, out addr))
                {
                    Console.Out.WriteLine("Must specify instruction");
                    Console.Out.Write("810A>");
                    AUTO_CMD = String.Empty;
                    return;
                }
                addr = (AUTO_CMD == "enter") ? ENTER_ADDR : CPU.PC;
                ENTER_ADDR = addr;
                if (!Assemble(ref ENTER_ADDR, arg))
                {
                    Console.Out.Write("810A>");
                    AUTO_CMD = String.Empty;
                    return;
                }
                Console.Out.Write("{0}  {1}  {2}  >", Octal(addr, 5), Octal(CPU[addr], 6), Decode(ref addr, CPU[addr], 20));
                AUTO_CMD = "enter";
            }
            else
            {
                if (ParseWord(arg.Substring(0, p), out addr))
                {
                    if (addr >= 0x8000)
                    {
                        Console.Out.WriteLine("Invalid address: {0}", arg.Substring(0, p));
                        Console.Out.Write("810A>");
                        AUTO_CMD = String.Empty;
                        return;
                    }
                    arg = arg.Substring(p + 1);
                    while (arg.IndexOfAny(WHITESPACE) == 0) arg = arg.Substring(1);
                }
                else
                {
                    addr = (AUTO_CMD == "enter") ? ENTER_ADDR : CPU.PC;
                }
                ENTER_ADDR = addr;
                if (!Assemble(ref ENTER_ADDR, arg))
                {
                    Console.Out.Write("810A>");
                    AUTO_CMD = String.Empty;
                    return;
                }
                Console.Out.Write("{0}  {1}  {2}  >", Octal(addr, 5), Octal(CPU[addr], 6), Decode(ref addr, CPU[addr], 20));
                AUTO_CMD = "enter";
            }
        }

        static UInt16 WRITE_ADDR = 0;
        static public void Write(String arg)
        {
            Int32 p;
            UInt16 word;
            if ((p = arg.IndexOfAny(WHITESPACE)) == -1)
            {
                p = WRITE_ADDR;
                if (p == -1) p = 0;
            }
            else
            {
                if (!ParseWord(arg.Substring(0, p), out word))
                {
                    Console.Out.WriteLine("Unrecognized: {0}", arg.Substring(0, p));
                    Console.Out.Write("810A>");
                    AUTO_CMD = String.Empty;
                    return;
                }
                if (word >= 0x8000)
                {
                    Console.Out.WriteLine("Invalid: {0}", arg.Substring(0, p));
                    Console.Out.Write("810A>");
                    AUTO_CMD = String.Empty;
                    return;
                }
                arg = arg.Substring(p + 1);
                while (arg.IndexOfAny(WHITESPACE) == 0) arg = arg.Substring(1);
                p = word;
            }
            if (arg.Length == 0)
            {
                word = CPU[p];
            }
            else if (!ParseWord(arg, out word))
            {
                Console.Out.WriteLine("Unrecognized: {0}", arg);
                Console.Out.Write("810A>");
                AUTO_CMD = String.Empty;
                return;
            }
            CPU[p] = word;
            AUTO_CMD = "=";
            WRITE_ADDR = (UInt16)((p + 1) % 32768);
            Console.Out.Write("{0}={1:x4}/{2}  {3}:{4:x4}/{5}  >", Octal(p, 5), word, Octal(word, 6), Octal(WRITE_ADDR, 5), CPU[WRITE_ADDR], Octal(CPU[WRITE_ADDR], 6));
        }

        static UInt16 DISASM_ADDR = 0;
        static public void Disassemble(String arg)
        {
            UInt16 p;
            if (arg.Length == 0) p = (AUTO_CMD == "unassemble") ? DISASM_ADDR : CPU.PC;
            else if (!ParseWord(arg, out p))
            {
                Console.Out.WriteLine("Unrecognized: {0}", arg);
                Console.Out.Write("810A>");
                AUTO_CMD = String.Empty;
                return;
            }
            Console.Out.Write("{0}  {1}  {2}  >", Octal(p, 5), Octal(CPU[p], 6), Decode(ref p, CPU[p], 20));
            AUTO_CMD = "unassemble";
            DISASM_ADDR = (UInt16)(p % 32768);
        }

        static Int32 STEP_ADDR = -1;
        static public void Step(String arg)
        {
            Int32 p;
            if (arg.Length == 0) p = (AUTO_CMD == "step") ? STEP_ADDR : 1;
            else if (!Int32.TryParse(arg, out p)) Console.Out.WriteLine("Unrecognized: {0}", arg);

            for (Int32 i = 0; (i < p) && (CPU.IsHalted); i++)
            {
                CPU.Step();
                Console.Out.Write("A:{0:X4}/{1}  B:{2:X4}/{3}  T:{4:X4}/{5}  ", CPU.A, Octal(CPU.A, 6), CPU.B, Octal(CPU.B, 6), CPU.T, Octal(CPU.T, 6));
                Console.Out.Write("PC:{0}  {1}  {2}  >", Octal(CPU.PC, 5), Octal(CPU.IR, 6), Decode(CPU.PC, CPU.IR, 16));
            }
            if (p != -1)
            {
                AUTO_CMD = "step";
                STEP_ADDR = p;
            }
            else
            {
                Console.Out.Write("810A>");
                AUTO_CMD = String.Empty;
            }
        }

        static public Boolean ParseWord(String s, out Int16 result)
        {
            Int32 n;
            Boolean retval = ParseWord(s, out n, -32768, 65535);
            result = (Int16)n;
            return retval;
        }

        static public Boolean ParseWord(String s, out UInt16 result)
        {
            Int32 n;
            Boolean retval = ParseWord(s, out n, 0, 65535);
            result = (UInt16)n;
            return retval;
        }

        static public Boolean ParseWord(String s, out Int32 result, Int32 min, Int32 max)
        {
            result = -1;
            if (s == null) return false;
            Int32 p = 0;
            while ((p < s.Length) && (s[p] == ' ')) p++; // skip leading spaces
            if (p == s.Length) return false;
            UInt16 radix = 10;
            Int16 sign = 1;
            if (s[p] == '-')
            {
                sign = -1;
                p++;
                if (p == s.Length) return false;
            }
            if ((s[p] == '\'') || (s[p] == 'o'))
            {
                radix = 8;
                p++;
            }
            else if (s[p] == 'x')
            {
                radix = 16;
                p++;
            }
            if (p == s.Length) return false;
            Int32 n;
            if ((n = RadixValue(s[p++], radix)) == -1) return false;
            result = n * sign;
            while ((p < s.Length) && ((n = RadixValue(s[p], radix)) != -1))
            {
                result = result * radix + n * sign;
                if ((result < min) || (result > max)) break;
                p++;
            }
            return (p == s.Length);
        }

        static public Int32 RadixValue(Char c, Int32 radix)
        {
            if (c < '0') return -1;
            if ((c <= '9') && ((c - '0') < radix)) return c - '0';
            if (c < 'A') return -1;
            if (c >= '`') c -= ' ';
            if ((c <= 'Z') && ((c - 'A' + 10) < radix)) return c - 'A' + 10;
            return -1;
        }

        static public String Octal(Int32 value)
        {
            return Octal(value, 0, '0');
        }

        static public String Octal(Int32 value, Int32 minWidth)
        {
            return Octal(value, minWidth, (minWidth < 0) ? ' ' : '0');
        }

        static public String Octal(Int32 value, Int32 minWidth, Char padChar)
        {
            Boolean f = false;
            if (minWidth < 0)
            {
                minWidth = -minWidth;
                f = true;
            }
            String num = Convert.ToString(value, 8);
            Int32 len = num.Length;
            if (len >= minWidth) return num;
            String pad = new String(padChar, minWidth - len);
            if (f) return String.Concat(num, pad);
            return String.Concat(pad, num);
        }

        static public String Decode(UInt16 addr, UInt16 word, Int32 width)
        {
            UInt16 tmp = addr;
            return Decode(ref tmp, word, width);
        }

        static public String Decode(ref UInt16 addr, UInt16 word, Int32 width)
        {
            String s = Decode(ref addr, word);
            if (s.Length >= width) return s;
            return String.Concat(s, new String(' ', width - s.Length));
        }

        static public String Decode(UInt16 addr, UInt16 word)
        {
            UInt16 tmp = addr;
            return Decode(ref tmp, word);
        }

        static public String Decode(ref UInt16 addr, UInt16 word)
        {
            // o ooo xim aaa aaa aaa - memory reference instruction
            // o ooo xis sss aaa aaa - augmented instruction
            Int32 op = (word >> 12) & 15;
            String X = ((word & 0x800) != 0) ? ",X" : null;
            Char I = ((word & 0x400) != 0) ? '*' : ' ';
            if (op == 0)
            {
                Int32 aug = (word & 0x3f) | ((word >> 4) & 0xc0);
                Int32 sc = (word >> 6) & 15;
                addr++;
                switch (aug)
                {
                    case 0: return "HLT";
                    case 1: return "RNA";
                    case 2: return "NEG";
                    case 3: return "CLA";
                    case 4: return "TBA";
                    case 5: return "TAB";
                    case 6: return "IAB";
                    case 7: return "CSB";
                    case 8: return String.Format("RSA  {0:D2}", sc);
                    case 9: return String.Format("LSA  {0:D2}", sc);
                    case 10: return String.Format("FRA  {0:D2}", sc);
                    case 11: return String.Format("FLL  {0:D2}", sc);
                    case 12: return String.Format("FRL  {0:D2}", sc);
                    case 13: return String.Format("RSL  {0:D2}", sc);
                    case 14: return String.Format("LSL  {0:D2}", sc);
                    case 15: return String.Format("FLA  {0:D2}", sc);
                    case 16: return "ASC";
                    case 17: return "SAS";
                    case 18: return "SAZ";
                    case 19: return "SAN";
                    case 20: return "SAP";
                    case 21: return "SOF";
                    case 22: return "IBS";
                    case 23: return "ABA";
                    case 24: return "OBA";
                    case 25: return "LCS";
                    case 26: return "SNO";
                    case 27: return "NOP";
                    case 28: return "CNS";
                    case 29: return "TOI";
                    case 30: return String.Format ("LOB  '{0}", Octal(CPU[addr++],6));
                    case 31: return "OVS";
                    case 32: return "TBP";
                    case 33: return "TPB";
                    case 34: return "TBV";
                    case 35: return "TVB";
                    case 36+64:
                    case 36: return String.Format("STX{0} '{1}", I, Octal(CPU[addr++], 6)); // should show M flag somehow
                    case 37+64:
                    case 37: return String.Format("LIX{0} '{1}", I, Octal(CPU[addr++], 6)); // should show M flag somehow
                    case 38: return "XPX";
                    case 39: return "XPB";
                    case 40: return "SXB";
                    case 41: return String.Format("IXS  {0:D2}", sc);
                    case 42: return "TAX";
                    case 43: return "TXA";
                    default: return String.Format("ZZZ{0} '{1}", I, Octal(word, 6));
                }
            }
            else if (op == 11)
            {
                Int32 aug = (word >> 6) & 7;
                UInt16 unit = (UInt16)(word & 0x3f);
                addr++;
                switch (aug)
                {
                    case 0: return String.Format("CEU{0} '{1} '{2}", I, Octal(unit), Octal(CPU[addr++], 6));
                    case 1: return String.Format("CEU{0} '{1},W '{2}", I, Octal(unit), Octal(CPU[addr++], 6)); // TODO: MAP mode
                    case 2: return String.Format("TEU{0} '{1} '{2}", I, Octal(unit), Octal(CPU[addr++], 6));
                    case 4: return String.Format("SNS  {0:D2}", unit & 15);
                    case 6: switch (unit)
                        {
                            case 0: return String.Format("PIE{0} '{1}", I, Octal(CPU[addr++], 6));
                            case 1: return String.Format("PID{0} '{1}", I, Octal(CPU[addr++], 6));
                            default: return String.Format("DATA  '{0}", Octal(word, 6, '0'));
                        }
                    default: return String.Format("DATA  '{0}", Octal(word, 6, '0'));
                }
            }
            else if (op == 15)
            {
                Int32 aug = (word >> 6) & 7;
                UInt16 unit = (UInt16)(word & 0x3f);
                addr++;
                switch (aug)
                {
                    case 0: return String.Format("AOP  '{0}", Octal(unit));
                    case 1: return String.Format("AOP  '{0},W", Octal(unit));
                    case 2: return String.Format("AIP  '{0}{1}", Octal(unit), (X == null) ? null : ",R");
                    case 3: return String.Format("AIP  '{0},W{1}", Octal(unit), (X == null) ? null : ",R");
                    case 4: return String.Format("MOP{0} '{1} '{2}", I, Octal(unit), Octal(CPU[addr++], 6));
                    case 5: return String.Format("MOP{0} '{1},W '{2}", I, Octal(unit), Octal(CPU[addr++], 6)); // TODO: MAP mode
                    case 6: return String.Format("MIP{0} '{1} '{2}", I, Octal(unit), Octal(CPU[addr++],6));
                    case 7: return String.Format("MIP{0} '{1},W '{2}", I, Octal(unit), Octal(CPU[addr++], 6)); // TODO: MAP mode
                    default: return String.Format("DATA  '{0}", Octal(word, 6, '0'));
                }
            }
            else
            {
                UInt16 ea = (UInt16)(word & 511);
                if ((word & 0x200) != 0) ea |= (UInt16)(addr & 0x7e00); // M flag
                addr++;
                switch (op)
                {
                    case 1: return String.Format("LAA{0} '{1}{2}", I, Octal(ea, 5, '0'), X);
                    case 2: return String.Format("LBA{0} '{1}{2}", I, Octal(ea, 5, '0'), X);
                    case 3: return String.Format("STA{0} '{1}{2}", I, Octal(ea, 5, '0'), X);
                    case 4: return String.Format("STB{0} '{1}{2}", I, Octal(ea, 5, '0'), X);
                    case 5: return String.Format("AMA{0} '{1}{2}", I, Octal(ea, 5, '0'), X);
                    case 6: return String.Format("SMA{0} '{1}{2}", I, Octal(ea, 5, '0'), X);
                    case 7: return String.Format("MPY{0} '{1}{2}", I, Octal(ea, 5, '0'), X);
                    case 8: return String.Format("DIV{0} '{1}{2}", I, Octal(ea, 5, '0'), X);
                    case 9: return String.Format("BRU{0} '{1}{2}", I, Octal(ea, 5, '0'), X);
                    case 10: return String.Format("SPB{0} '{1}{2}", I, Octal(ea, 5, '0'), X);
                    case 12: return String.Format("IMS{0} '{1}{2}", I, Octal(ea, 5, '0'), X);
                    case 13: return String.Format("CMA{0} '{1}{2}", I, Octal(ea, 5, '0'), X);
                    case 14: return String.Format("AMB{0} '{1}{2}", I, Octal(ea, 5, '0'), X);
                    default: return String.Format("DATA '{0}", Octal(word, 6));
                }
            }
        }

        static private Boolean Assemble(ref UInt16 addr, String line)
        {
            while ((line.Length != 0) && (line[0] == ' ')) line = line.Substring(1);
            String name = line;
            line = String.Empty;
            Int32 p = name.IndexOf(' ');
            if (p == -1) p = name.IndexOf('\t');
            if (p != -1)
            {
                line = name.Substring(p + 1);
                name = name.Substring(0, p);
            }
            name = name.ToUpper();
            while ((line.Length != 0) && (line[0] == ' ')) line = line.Substring(1);
            Boolean X = false;
            Boolean I = false;
            Boolean M = false;
            Boolean W = false;
            Boolean R = false;
            if (name.EndsWith("*"))
            {
                name = name.Substring(0, name.Length - 1);
                I = true;
            }
            p = 0;
            while ((p < Ops.Length) && (Ops[p].Name != name)) p++;
            if (p == Ops.Length)
            {
                Console.Out.WriteLine("Unrecognized mnemonic: {0}", name);
                return false;
            }
            Op op = Ops[p];
            UInt16 arg = 0;
            switch (op.Format)
            {
                case 0: // 000000ddddcccccc (Augmented 00)
                    if (op.Arg != 0)
                    {
                        if (line.Length == 0)
                        {
                            Console.Out.WriteLine("Missing operand");
                            return false;
                        }
                        if (!ParseWord(line, out arg))
                        {
                            Console.Out.WriteLine("Unrecognized operand: {0}", line);
                            return false;
                        }
                        if (arg > 15)
                        {
                            Console.Out.WriteLine("Invalid operand: {0}", line);
                            return false;
                        }
                    }
                    arg <<= 6;
                    arg |= op.Code;
                    CPU[addr++] = arg;
                    return true;
                case 1: // 00000IM000cccccc XIaaaaaaaaaaaaaa (STX, LIX)
                    if (line.Length == 0)
                    {
                        Console.Out.WriteLine("Missing operand");
                        return false;
                    }
                    if (!ParseWord(line, out arg))
                    {
                        Console.Out.WriteLine("Unrecognized operand: {0}", line);
                        return false;
                    }
                    if (I) op.Code |= 0x0400;
                    CPU[addr++] = op.Code;
                    CPU[addr++] = arg;
                    return true;
                case 2: // XIaaaaaaaaaaaaaa (DAC)
                    if (line.Length == 0)
                    {
                        Console.Out.WriteLine("Missing operand");
                        return false;
                    }
                    if ((line.EndsWith(",1")) || (line.EndsWith(",X", StringComparison.OrdinalIgnoreCase)))
                    {
                        line = line.Substring(0, line.Length - 2);
                        X = true;
                    }
                    if (!ParseWord(line, out arg))
                    {
                        Console.Out.WriteLine("Unrecognized operand: {0}", line);
                        return false;
                    }
                    if (arg >= 16384)
                    {
                        Console.Out.WriteLine("Invalid operand: {0}", line);
                        return false;
                    }
                    if (X) arg |= 0x8000;
                    if (I) arg |= 0x4000;
                    CPU[addr++] = arg;
                    return true;
                case 3: // 10110IMccWuuuuuu dddddddddddddddd (CEU, TEU)
                    if (line.Length == 0)
                    {
                        Console.Out.WriteLine("Missing operand");
                        return false;
                    }
                    if (op.Arg != 0)
                    {
                        p = line.IndexOf(' ');
                        if (p == -1) p = line.IndexOf('\t');
                        if (p == -1)
                        {
                            Console.Out.WriteLine("Missing operand");
                            return false;
                        }
                        String unit = line.Substring(0, p);
                        if ((unit.EndsWith(",1")) || (unit.EndsWith(",W", StringComparison.OrdinalIgnoreCase)))
                        {
                            unit = unit.Substring(0, unit.Length - 2);
                            W = true;
                        }
                        if (!ParseWord(unit, out arg))
                        {
                            Console.Out.WriteLine("Unrecognized unit: {0}", unit);
                            return false;
                        }
                        if (arg > 63)
                        {
                            Console.Out.WriteLine("Invalid unit: {0}", unit);
                            return false;
                        }
                        line = line.Substring(p + 1);
                    }
                    while ((line.Length != 0) && (line[0] == ' ')) line = line.Substring(1);
                    if (line.Length == 0)
                    {
                        Console.Out.WriteLine("Missing operand");
                        return false;
                    }
                    p = (0x160 | ((I) ? 8 : 0) | (op.Code & 3)) << 1; // TODO: M flag
                    if (W) p |= 1;
                    p = (p << 6) + arg;
                    op.Code = (UInt16)(p);
                    if (!ParseWord(line, out arg))
                    {
                        Console.Out.WriteLine("Unrecognized operand: {0}", line);
                        return false;
                    }
                    CPU[addr++] = op.Code;
                    CPU[addr++] = arg;
                    return true;
                case 4: // 10110IMccWuuuuuu (SNS)
                    if (op.Arg != 0)
                    {
                        if (line.Length == 0)
                        {
                            Console.Out.WriteLine("Missing operand");
                            return false;
                        }
                        if (!ParseWord(line, out arg))
                        {
                            Console.Out.WriteLine("Unrecognized operand: {0}", line);
                            return false;
                        }
                        if (arg > 63)
                        {
                            Console.Out.WriteLine("Invalid operand: {0}", line);
                            return false;
                        }
                    }
                    p = (0x160 | ((I) ? 8 : 0) | (op.Code & 3)) << 7; // TODO: M flag
                    p += arg;
                    CPU[addr++] = (UInt16)(p);
                    return true;
                case 5: // 10110IM11Wcccccc dddddddddddddddd (PIE, PID)
                    if (line.Length == 0)
                    {
                        Console.Out.WriteLine("Missing operand");
                        return false;
                    }
                    if (!ParseWord(line, out arg))
                    {
                        Console.Out.WriteLine("Unrecognized operand: {0}", line);
                        return false;
                    }
                    p = (0x163 | ((I) ? 8 : 0)) << 7; // TODO: M flag
                    p |= op.Code & 63;
                    CPU[addr++] = (UInt16)(p);
                    CPU[addr++] = arg;
                    return true;
                case 6: // ccccXIMaaaaaaaaa (LAA, LBA, STA, STB, AMA, SMA, MPY, DIV, BRU, SPB, IMS, CMA, AMB)
                    if (line.Length == 0)
                    {
                        Console.Out.WriteLine("Missing operand");
                        return false;
                    }
                    if ((line.EndsWith(",1")) || (line.EndsWith(",X", StringComparison.OrdinalIgnoreCase)))
                    {
                        line = line.Substring(0, line.Length - 2);
                        X = true;
                    }
                    if (!ParseWord(line, out arg))
                    {
                        Console.Out.WriteLine("Unrecognized operand: {0}", line);
                        return false;
                    }
                    if (arg >= 0x8000)
                    {
                        Console.Out.WriteLine("Invalid operand: {0}", line);
                        return false;
                    }
                    if (arg >= 512)
                    {
                        if ((arg & 0x7e00) != (addr & 0x7e00))
                        {
                            Console.Out.WriteLine("Invalid operand MAP: {0}", line);
                            return false;
                        }
                        arg &= 511;
                        M = true;
                    }
                    p = (op.Code & 15) << 3;
                    if (X) p |= 4;
                    if (I) p |= 2;
                    if (M) p |= 1;
                    p <<= 9;
                    p += arg;
                    CPU[addr++] = (UInt16)(p);
                    return true;
                case 7: // 11110IMccWuuuuuu dddddddddddddddd (MOP, MIP)
                    if (line.Length == 0)
                    {
                        Console.Out.WriteLine("Missing operand");
                        return false;
                    }
                    if (op.Arg != 0)
                    {
                        p = line.IndexOf(' ');
                        if (p == -1) p = line.IndexOf('\t');
                        if (p == -1)
                        {
                            Console.Out.WriteLine("Missing operand");
                            return false;
                        }
                        String unit = line.Substring(0, p);
                        if ((unit.EndsWith(",1")) || (unit.EndsWith(",W", StringComparison.OrdinalIgnoreCase)))
                        {
                            unit = unit.Substring(0, unit.Length - 2);
                            W = true;
                        }
                        if (!ParseWord(unit, out arg))
                        {
                            Console.Out.WriteLine("Unrecognized unit: {0}", unit);
                            return false;
                        }
                        if (arg > 63)
                        {
                            Console.Out.WriteLine("Invalid unit: {0}", unit);
                            return false;
                        }
                        line = line.Substring(p + 1);
                    }
                    while ((line.Length != 0) && (line[0] == ' ')) line = line.Substring(1);
                    if (line.Length == 0)
                    {
                        Console.Out.WriteLine("Missing operand");
                        return false;
                    }
                    p = (0x1e0 | ((I) ? 8 : 0) | (op.Code & 3)) << 1; // TODO: M flag
                    if (W) p |= 1;
                    p = (p << 6) + arg;
                    op.Code = (UInt16)(p);
                    if (!ParseWord(line, out arg))
                    {
                        Console.Out.WriteLine("Unrecognized operand: {0}", line);
                        return false;
                    }
                    CPU[addr++] = op.Code;
                    CPU[addr++] = arg;
                    return true;
                case 8: // 1111RIMccWuuuuuu (AOP, AIP)
                    if (op.Arg != 0)
                    {
                        if (line.Length == 0)
                        {
                            Console.Out.WriteLine("Missing operand");
                            return false;
                        }
                        if ((line.EndsWith(",1")) || (line.EndsWith(",R", StringComparison.OrdinalIgnoreCase)))
                        {
                            line = line.Substring(0, line.Length - 2);
                            R = true;
                        }
                        if ((line.EndsWith(",1")) || (line.EndsWith(",W", StringComparison.OrdinalIgnoreCase)))
                        {
                            line = line.Substring(0, line.Length - 2);
                            W = true;
                        }
                        if (!ParseWord(line, out arg))
                        {
                            Console.Out.WriteLine("Unrecognized unit: {0}", line);
                            return false;
                        }
                        if (arg > 63)
                        {
                            Console.Out.WriteLine("Invalid unit: {0}", line);
                            return false;
                        }
                    }
                    p = (0x1e0 | ((R) ? 16 : 0) | ((I) ? 8 : 0) | (op.Code & 3)) << 1; // TODO: M flag
                    if (W) p |= 1;
                    p = (p << 6) + arg;
                    CPU[addr++] = (UInt16)(p);
                    return true;
                case 9: // 0000000000cccccc 0aaaaaaaaaaaaaaa (LOB)
                    if (op.Arg != 0)
                    {
                        if (line.Length == 0)
                        {
                            Console.Out.WriteLine("Missing address");
                            return false;
                        }
                        if (!ParseWord(line, out arg))
                        {
                            Console.Out.WriteLine("Unrecognized address: {0}", line);
                            return false;
                        }
                        if (arg >= 0x8000)
                        {
                            Console.Out.WriteLine("Invalid address: {0}", line);
                            return false;
                        }
                    }
                    CPU[addr++] = op.Code;
                    CPU[addr++] = arg;
                    return true;
                case 10: // dddddddddddddddd (EAC, DATA)
                    if (line.Length == 0)
                    {
                        Console.Out.WriteLine("Missing operand");
                        return false;
                    }
                    if (!ParseWord(line, out arg))
                    {
                        Console.Out.WriteLine("Unrecognized operand: {0}", line);
                        return false;
                    }
                    if ((op.Arg != 0) && (arg >= 0x8000))
                    {
                        Console.Out.WriteLine("Invalid address: {0}", line);
                        return false;
                    }
                    CPU[addr++] = arg;
                    return true;
                default:
                    return false;
            }
        }

        struct Op
        {
            public String Name;
            public UInt16 Code;
            public Byte Format;
            public Byte Arg;

            public Op(String name, Byte format, UInt16 code, Byte arg)
            {
                Name = name;
                Format = format;
                Code = code;
                Arg = arg;
            }
        }

        // fmt 0 = 000000ddddcccccc                  (Arg=0: dddd=0, Arg=1: dddd=arg)
        // fmt 1 = 00000IM000cccccc XIaaaaaaaaaaaaaa
        // fmt 2 = XIaaaaaaaaaaaaaa
        // fmt 3 = 10110IMccWuuuuuu dddddddddddddddd (Arg=0: uuuuuu=0, Arg=1: uuuuuu=arg)
        // fmt 4 = 10110IMccWuuuuuu                  (Arg=0: uuuuuu=0, Arg=1: uuuuuu=arg)
        // fmt 5 = 10110IM11Wcccccc dddddddddddddddd
        // fmt 6 = ccccXIMaaaaaaaaa
        // fmt 7 = 11110IMccWuuuuuu dddddddddddddddd (Arg=0: uuuuuu=0, Arg=1: uuuuuu=arg)
        // fmt 8 = 1111RIMccWuuuuuu                  (Arg=0: uuuuuu=0, Arg=1: uuuuuu=arg)
        // fmt 9 = 0000000000cccccc 0aaaaaaaaaaaaaaa (Arg=0: aa...a=0, Arg=1: aa...a=arg)
        // fmt 10= dddddddddddddddd                  (Arg=0: 0<=arg<=65535, Arg=1: 0<=arg<=32767)
        static Op[] Ops = {
            new Op("DAC", 2, 0, 0),
            new Op("EAC", 10, 0, 1),
            new Op("DATA", 10, 0, 0),
            new Op("**", 0, 0, 0),
            new Op("HLT", 0, 0, 0),
            new Op("RNA", 0, 1, 0),
            new Op("NEG", 0, 2, 0),
            new Op("CLA", 0, 3, 0),
            new Op("TBA", 0, 4, 0),
            new Op("TAB", 0, 5, 0),
            new Op("IAB", 0, 6, 0),
            new Op("CSB", 0, 7, 0),
            new Op("RSA", 0, 8, 1),
            new Op("LSA", 0, 9, 1),
            new Op("FRA", 0, 10, 1),
            new Op("FLL", 0, 11, 1),
            new Op("FRL", 0, 12, 1),
            new Op("RSL", 0, 13, 1),
            new Op("LSL", 0, 14, 1),
            new Op("FLA", 0, 15, 1),
            new Op("ASC", 0, 16, 0),
            new Op("SAS", 0, 17, 0),
            new Op("SAZ", 0, 18, 0),
            new Op("SAN", 0, 19, 0),
            new Op("SAP", 0, 20, 0),
            new Op("SOF", 0, 21, 0),
            new Op("IBS", 0, 22, 0),
            new Op("ABA", 0, 23, 0),
            new Op("OBA", 0, 24, 0),
            new Op("LCS", 0, 25, 0),
            new Op("SNO", 0, 26, 0),
            new Op("NOP", 0, 27, 0),
            new Op("CNS", 0, 28, 0),
            new Op("TOI", 0, 29, 0),
            new Op("LOB", 9, 30, 1),
            new Op("OVS", 0, 31, 0),
            new Op("TBP", 0, 32, 0),
            new Op("TPB", 0, 33, 0),
            new Op("TBV", 0, 34, 0),
            new Op("TVB", 0, 35, 0),
            new Op("STX", 1, 36, 1),
            new Op("LIX", 1, 37, 1),
            new Op("XPX", 0, 38, 0),
            new Op("XPB", 0, 39, 0),
            new Op("SXB", 0, 40, 0),
            new Op("IXS", 0, 41, 1),
            new Op("TAX", 0, 42, 0),
            new Op("TXA", 0, 43, 0),
            new Op("CEU", 3, 0, 1),
            new Op("TEU", 3, 1, 1),
            new Op("SNS", 4, 2, 1),
            new Op("PIE", 5, 0, 0),
            new Op("PID", 5, 1, 0),
            new Op("LAA", 6, 1, 0),
            new Op("LBA", 6, 2, 0),
            new Op("STA", 6, 3, 0),
            new Op("STB", 6, 4, 0),
            new Op("AMA", 6, 5, 0),
            new Op("SMA", 6, 6, 0),
            new Op("MPY", 6, 7, 0),
            new Op("DIV", 6, 8, 0),
            new Op("BRU", 6, 9, 0),
            new Op("SPB", 6, 10, 0),
            new Op("IMS", 6, 12, 0),
            new Op("CMA", 6, 13, 0),
            new Op("AMB", 6, 14, 0),
            new Op("AOP", 8, 0, 1),
            new Op("AIP", 8, 1, 1),
            new Op("MOP", 7, 2, 1),
            new Op("MIP", 7, 3, 1),
        };
    }
}
