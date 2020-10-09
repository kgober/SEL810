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


using System;
using System.IO;

namespace Emulator
{
    class Program
    {
        static SEL810 CPU = new SEL810();
        static String AUTO_CMD = String.Empty;

        static void Main(String[] args)
        {
            Int32 ap = 0;
            while (ap < args.Length)
            {
                String arg = args[ap++];
                LoadState(arg);
            }

            Console.Out.Write("810A>");
            String cmd;
            while ((cmd = Console.In.ReadLine()) != null)
            {
                String arg = String.Empty;
                Int32 p = cmd.IndexOf(' ');
                Int16 word;
                if (p != -1)
                {
                    arg = cmd.Substring(p + 1);
                    cmd = cmd.Substring(0, p);
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
                    Console.Out.WriteLine("g[o] - start CPU");
                    Console.Out.WriteLine("h[alt] - halt CPU");
                    Console.Out.WriteLine("ir [val] - display or set Instruction Register");
                    Console.Out.WriteLine("i[nput] filename - read paper tape input from 'filename'");
                    Console.Out.WriteLine("l[oad] [addr] filename - load memory from 'filename' at 'addr' (default 0)");
                    Console.Out.WriteLine("mc - master clear (clears all registers)");
                    Console.Out.WriteLine("o[utput] filename - write paper tape output to 'filename'");
                    Console.Out.WriteLine("pc [val] - display or set Program Counter");
                    Console.Out.WriteLine("q[uit] [filename] - exit emulator, optionally saving state to 'filename'");
                    Console.Out.WriteLine("r[egisters] - display registers");
                    Console.Out.WriteLine("s[tep] - single step CPU (Enter to continue)");
                    Console.Out.WriteLine("t[oggle] [val] - display or set sense switches");
                    Console.Out.WriteLine("u[nassemble] [addr] - display instruction at 'addr' (Enter to continue)");
                    Console.Out.WriteLine("w[rite] addr len filename - write 'len' words at 'addr' to 'filename'");
                    Console.Out.WriteLine("= [addr] [val] - write 'val' to 'addr' (Enter to continue)");
                    Console.Out.WriteLine(". [addr [count]] - set a read breakpoint at 'addr'");
                    Console.Out.WriteLine("! [addr [count]] - set a write breakpoint at 'addr'");
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
                            case 1: Console.Out.WriteLine("1=printer"); break;
                            case 2: Console.Out.WriteLine("2=punch"); break;
                            case 3: Console.Out.WriteLine("3=both (printer and punch)"); break;
                            default: Console.Out.WriteLine("? unknown ({0:D0})", CPU.ConsoleMode); break;
                        }
                    }
                    else if ((arg == "1") || (arg == "printer"))
                    {
                        CPU.ConsoleMode = 1;
                    }
                    else if ((arg == "2") || (arg == "punch"))
                    {
                        CPU.ConsoleMode = 2;
                    }
                    else if ((arg == "3") || (arg == "both"))
                    {
                        CPU.ConsoleMode = 3;
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
                else if (cmd[0] == 'g') // go
                {
                    CPU.Start();
                }
                else if (cmd[0] == 'h') // halt
                {
                    CPU.Stop();
                }
                else if (cmd == "ir")
                {
                    if (arg.Length == 0)
                    {
                        Console.Out.WriteLine("IR:{0:X4}/{1}  {2}", CPU.IR, Octal(CPU.IR, 6), Op(CPU.PC, CPU.IR));
                    }
                    else if (!ParseWord(arg, out word))
                    {
                        Console.Out.WriteLine("Unrecognized: {0}", arg);
                    }
                    else
                    {
                        CPU.IR = word;
                        Console.Out.WriteLine("IR={0:X4}/{1}  {2}", CPU.IR, Octal(CPU.IR, 6), Op(CPU.PC, CPU.IR));
                    }
                }
                else if (cmd[0] == 'i') // input
                {
                    if ((arg.Length != 0) && (!File.Exists(arg))) Console.Out.WriteLine("File not found: {0}", arg);
                    else CPU.SetReader(arg);
                }
                else if (cmd[0] == 'l') // load
                {
                    while ((arg.Length != 0) && (arg[0] == ' ')) arg = arg.Substring(1);
                    if ((p = arg.IndexOf(' ')) == -1)
                    {
                        word = 0;
                    }
                    else
                    {
                        if (!ParseWord(arg.Substring(0, p), out word)) word = 0;
                        else arg = arg.Substring(p + 1);
                    }
                    if (!File.Exists(arg)) Console.Out.WriteLine("File not found: {0}", arg);
                    else CPU.Load(word, arg);
                }
                else if (cmd == "mc")
                {
                    CPU.PC = CPU.IR = CPU.A = CPU.B = CPU.T = 0;
                    Console.Out.WriteLine("A={0:X4}/{1}  B={2:X4}/{3}  T={4:X4}/{5}", CPU.A, Octal(CPU.A, 6), CPU.B, Octal(CPU.B, 6), CPU.T, Octal(CPU.T, 6));
                    Console.Out.WriteLine("PC={0:X4}/{1}  IR={2:X4}/{3}  {4}", CPU.PC, Octal(CPU.PC, 5), CPU.IR, Octal(CPU.IR, 6), Op(CPU.PC, CPU.IR));
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
                    CPU.Stop();
                    if (arg.Length != 0) SaveState(arg);
                    CPU.Exit();
                    break;
                }
                else if (cmd[0] == 'r') // registers
                {
                    Console.Out.WriteLine("A:{0:X4}/{1}  B:{2:X4}/{3}  T:{4:X4}/{5}", CPU.A, Octal(CPU.A, 6), CPU.B, Octal(CPU.B, 6), CPU.T, Octal(CPU.T, 6));
                    Console.Out.WriteLine("PC:{0:X4}/{1}  IR:{2:X4}/{3}  {4}", CPU.PC, Octal(CPU.PC, 5), CPU.IR, Octal(CPU.IR, 6), Op(CPU.PC, CPU.IR));
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
            }
        }

        // TODO: include OVF, CF, X, XP, PPR, VBR, maybe breakpoints?
        static public void LoadState(String fileName)
        {
            Byte[] buf = File.ReadAllBytes(fileName);
            Int32 p = 0, q = 0;
            CPU.PC = BitConverter.ToInt16(buf, p);
            CPU.IR = BitConverter.ToInt16(buf, p += 2);
            CPU.A = BitConverter.ToInt16(buf, p += 2);
            CPU.B = BitConverter.ToInt16(buf, p += 2);
            CPU.T = BitConverter.ToInt16(buf, p += 2);
            CPU.SR = BitConverter.ToInt16(buf, p += 2);
            while ((p += 2) < buf.Length)
            {
                CPU[q++] = BitConverter.ToInt16(buf, p);
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
            Int16 addr, len;
            while ((arg.Length != 0) && (arg[0] == ' ')) arg = arg.Substring(1);
            if ((p = arg.IndexOf(' ')) == -1)
            {
                Console.Out.WriteLine("Must specify address, word count, and filename");
                return;
            }
            if (!ParseWord(arg.Substring(0, p), out addr))
            {
                Console.Out.WriteLine("Unrecognized address: {0}", arg.Substring(0, p));
                return;
            }
            if (addr < 0)
            {
                Console.Out.WriteLine("Invalid address: {0}", arg.Substring(0, p));
                return;
            }
            arg = arg.Substring(p + 1);
            while ((arg.Length != 0) && (arg[0] == ' ')) arg = arg.Substring(1);
            if ((p = arg.IndexOf(' ')) == -1)
            {
                Console.Out.WriteLine("Must specify address, word count, and filename");
                return;
            }
            if (!ParseWord(arg.Substring(0, p), out len))
            {
                Console.Out.WriteLine("Unrecognized word count: {0}", arg.Substring(0, p));
                return;
            }
            if (len < 0)
            {
                Console.Out.WriteLine("Invalid word count: {0}", arg.Substring(0, p));
                return;
            }
            arg = arg.Substring(p + 1);
            while ((arg.Length != 0) && (arg[0] == ' ')) arg = arg.Substring(1);
            if (arg.Length == 0)
            {
                Console.Out.WriteLine("Must specify address, word count, and filename");
                return;
            }
            FileStream f = new FileStream(arg, FileMode.Create, FileAccess.Write);
            for (Int32 i = 0; i < len; i++)
            {
                Int16 word = CPU[(addr + i) % 32768];
                f.WriteByte((Byte)((word >> 8) & 0xff));
                f.WriteByte((Byte)(word & 0xff));
            }
            f.Close();
        }

        static public void ReadBP(String arg)
        {
            Int32 p;
            Int16 addr, ct;
            if (arg.Length == 0)
            {
                Int16 n = 0;
                for (Int32 i = 0; i < SEL810.CORE_SIZE; i++)
                {
                    if ((ct = CPU.GetBPR((Int16)(i))) == 0) continue;
                    Console.Out.WriteLine("Read BP: {0:x4}/{1} {2}", i, Octal((Int16)(i), 5), (ct < 0) ? "*" : ct.ToString());
                    n++;
                }
                if (n == 0) Console.Out.WriteLine("Read BP: none set");
                return;
            }
            while ((arg.Length != 0) && (arg[0] == ' ')) arg = arg.Substring(1);
            if ((p = arg.IndexOf(' ')) == -1)
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
            CPU.SetBPR(addr, ct);
        }

        static public void WriteBP(String arg)
        {
            Int32 p;
            Int16 addr, ct;
            if (arg.Length == 0)
            {
                Int16 n = 0;
                for (Int32 i = 0; i < SEL810.CORE_SIZE; i++)
                {
                    if ((ct = CPU.GetBPW((Int16)(i))) == 0) continue;
                    Console.Out.WriteLine("Write BP: {0:x4}/{1} {2}", i, Octal((Int16)(i), 5), (ct < 0) ? "*" : ct.ToString());
                    n++;
                }
                if (n == 0) Console.Out.WriteLine("Write BP: none set");
                return;
            }
            while ((arg.Length != 0) && (arg[0] == ' ')) arg = arg.Substring(1);
            if ((p = arg.IndexOf(' ')) == -1)
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

        static Int16 DUMP_ARG = -1;
        static public void Dump(String arg)
        {
            Int16 p;
            if (arg.Length == 0) p = (AUTO_CMD == "dump") ? DUMP_ARG : CPU.PC;
            else if (!ParseWord(arg, out p)) Console.Out.WriteLine("Unrecognized: {0}", arg);
            if (p != -1)
            {
                Console.Out.Write("{0}  ", Octal(p, 5));
                for (Int32 i = 0; i < 8; i++)
                {
                    Console.Out.Write(" {0}", Octal(CPU[(p + i) % 32768], 6));
                }
                Console.Out.Write("  >");
                AUTO_CMD = "dump";
                DUMP_ARG = (Int16)((p + 8) % 32768);
            }
            else
            {
                Console.Out.Write("810A>");
            }
        }

        static Int16 WRITE_ADDR = -1;
        static public void Write(String arg)
        {
            Int32 p;
            Int16 word;
            while ((arg.Length != 0) && (arg[0] == ' ')) arg = arg.Substring(1);
            if ((p = arg.IndexOf(' ')) == -1)
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
                    return;
                }
                if (word < 0)
                {
                    Console.Out.WriteLine("Invalid: {0}", arg.Substring(0, p));
                    Console.Out.Write("810A>");
                    return;
                }
                arg = arg.Substring(p + 1);
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
                return;
            }
            CPU[p] = word;
            AUTO_CMD = "=";
            WRITE_ADDR = (Int16)((p + 1) % 32768);
            Console.Out.Write("{0}={1:x4}/{2}  {3}:{4:x4}/{5}  >", Octal((Int16)(p), 5), word, Octal(word, 6), Octal(WRITE_ADDR, 5), CPU[WRITE_ADDR], Octal(CPU[WRITE_ADDR], 6));
        }

        static Int16 DISASM_ARG = -1;
        static public void Disassemble(String arg)
        {
            Int16 p;
            if (arg.Length == 0) p = (AUTO_CMD == "unassemble") ? DISASM_ARG : CPU.PC;
            else if (!ParseWord(arg, out p)) Console.Out.WriteLine("Unrecognized: {0}", arg);
            if (p != -1)
            {
                Console.Out.Write("{0}  {1}  {2}  >", Octal(p, 5), Octal(CPU[p], 6), Op(ref p, CPU[p], 20));
                AUTO_CMD = "unassemble";
                DISASM_ARG = (Int16)(p % 32768);
            }
            else
            {
                Console.Out.Write("810A>");
            }
        }

        static Int32 STEP_ARG = -1;
        static public void Step(String arg)
        {
            Int32 p;
            if (arg.Length == 0) p = (AUTO_CMD == "step") ? STEP_ARG : 1;
            else if (!Int32.TryParse(arg, out p)) Console.Out.WriteLine("Unrecognized: {0}", arg);

            for (Int32 i = 0; (i < p) && (CPU.IsHalted); i++)
            {
                CPU.Step();
                Console.Out.Write("A:{0:X4}/{1}  B:{2:X4}/{3}  T:{4:X4}/{5}  ", CPU.A, Octal(CPU.A, 6), CPU.B, Octal(CPU.B, 6), CPU.T, Octal(CPU.T, 6));
                Console.Out.Write("PC:{0:X4}/{1}  {2}  {3}  >", CPU.PC, Octal(CPU.PC, 5), Octal(CPU.IR, 6), Op(CPU.PC, CPU.IR, 16));
            }
            if (p != -1)
            {
                AUTO_CMD = "step";
                STEP_ARG = p;
            }
            else
            {
                Console.Out.Write("810A>");
            }
        }

        static public Boolean ParseWord(String s, out Int16 result)
        {
            result = -1;
            if (s == null) return false;
            Int32 p = 0;
            while ((p < s.Length) && (s[p] == ' ')) p++; // skip leading spaces
            if (p == s.Length) return false;
            Int16 radix = 10;
            Int16 sign = 1;
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
            else if (s[p] == '-')
            {
                sign = -1;
                p++;
                if (p == s.Length) return false;
                if (s[p] == 'o')
                {
                    radix = 8;
                    p++;
                }
                else if (s[p] == 'x')
                {
                    radix = 16;
                    p++;
                }
            }
            if (p == s.Length) return false;
            Int32 n;
            if ((n = RadixValue(s[p++], radix)) == -1) return false;
            result = (Int16)(n * sign);
            while ((p < s.Length) && ((n = RadixValue(s[p], radix)) != -1))
            {
                if ((((result * radix) + n) > 65535) && (sign == 1)) break;
                if ((((result * radix) - n) < -32768) && (sign == -1)) break;
                n = ((sign * result * radix) + n) * sign;
                result = (Int16)(n & 0xffff);
                p++;
            }
            return (p == s.Length);
        }

        static public Int16 RadixValue(Char c, Int32 radix)
        {
            if (c < '0') return -1;
            if ((c <= '9') && ((c - '0') < radix)) return (Int16)(c - '0');
            if (c < 'A') return -1;
            if (c >= '`') c -= ' ';
            if ((c <= 'Z') && ((c - 'A' + 10) < radix)) return (Int16)(c - 'A' + 10);
            return -1;
        }

        static public String Octal(Int16 value)
        {
            return Octal(value, 0, '0');
        }

        static public String Octal(Int16 value, Int32 minWidth)
        {
            return Octal(value, minWidth, (minWidth < 0) ? ' ' : '0');
        }

        static public String Octal(Int16 value, Int32 minWidth, Char padChar)
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

        static public String Op(Int16 addr, Int16 word, Int32 width)
        {
            Int16 tmp = addr;
            return Op(ref tmp, word, width);
        }

        static public String Op(ref Int16 addr, Int16 word, Int32 width)
        {
            String s = Op(ref addr, word);
            if (s.Length >= width) return s;
            return String.Concat(s, new String(' ', width - s.Length));
        }

        static public String Op(Int16 addr, Int16 word)
        {
            Int16 tmp = addr;
            return Op(ref tmp, word);
        }

        static public String Op(ref Int16 addr, Int16 word)
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
                    case 36: return String.Format("STX{0} '{1}", I, Octal(CPU[addr++], 6)); // should show M flag somehow
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
                Int16 unit = (Int16)(word & 0x3f);
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
                }
            }
            else if (op == 15)
            {
                Int32 aug = (word >> 6) & 7;
                Int16 unit = (Int16)(word & 0x3f);
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
                Int16 ea = (Int16)(word & 511);
                if ((word & 0x200) != 0) ea |= (Int16)(addr & 0x7e00); // M flag
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
                }
            }
            return String.Format("DATA '{0}", Octal(word, 6));
        }
    }
}
