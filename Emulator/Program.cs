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
                    Console.Out.WriteLine("pc [val] - display or set Program Counter");
                    Console.Out.WriteLine("d[ump] [addr] - dump 8 words at 'addr' (Enter to continue)");
                    Console.Out.WriteLine("g[o] - start CPU");
                    Console.Out.WriteLine("h[alt] - halt CPU");
                    Console.Out.WriteLine("l[oad] [addr] filename - load memory from filename at 'addr' (default 0)");
                    Console.Out.WriteLine("q[uit] - exit emulator");
                    Console.Out.WriteLine("r[egisters] - display registers");
                    Console.Out.WriteLine("s[tep] - single step CPU (Enter to continue)");
                    Console.Out.WriteLine("t[oggle] [val] - display or set sense switches");
                    Console.Out.WriteLine("u[nassemble] [addr] - display instruction at 'addr' (Enter to continue)");
                    Console.Out.WriteLine(". [count] addr - set a read breakpoint at 'addr'");
                    Console.Out.WriteLine("! [count] addr - set a write breakpoint at 'addr'");
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
                        Console.Out.WriteLine("A:{0:X4}/{1}", CPU.A, Octal(CPU.A, 6));
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
                        Console.Out.WriteLine("B:{0:X4}/{1}", CPU.B, Octal(CPU.B, 6));
                    }
                }
                else if (cmd == "pc")
                {
                    if (arg.Length == 0)
                    {
                        Console.Out.WriteLine("PC:{0:X4}/{1}  IR:{2}  {3}", CPU.PC, Octal(CPU.PC, 5), Octal(CPU.IR, 6), Op(CPU.PC, CPU.IR));
                    }
                    else if (!ParseWord(arg, out word))
                    {
                        Console.Out.WriteLine("Unrecognized: {0}", arg);
                    }
                    else
                    {
                        CPU.PC = word;
                        Console.Out.WriteLine("PC:{0:X4}/{1}  IR:{2}  {3}", CPU.PC, Octal(CPU.PC, 5), Octal(CPU.IR, 6), Op(CPU.PC, CPU.IR));
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
                    CPU.Load(word, arg);
                }
                else if (cmd[0] == 'q') // quit
                {
                    CPU.Exit();
                    break;
                }
                else if (cmd[0] == 'r') // registers
                {
                    Console.Out.WriteLine("A:{0:X4}/{1}  B:{2:X4}/{3}  T:{4:X4}/{5}", CPU.A, Octal(CPU.A, 6), CPU.B, Octal(CPU.B, 6), CPU.T, Octal(CPU.T, 6));
                    Console.Out.WriteLine("PC:{0:X4}/{1}  IR:{2}  {3}", CPU.PC, Octal(CPU.PC, 5), Octal(CPU.IR, 6), Op(CPU.PC, CPU.IR));
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
                        Console.Out.WriteLine("SR:{0:X4}/{1}", CPU.SR, Octal(CPU.SR, 6));
                    }
                }
                else if (cmd[0] == 'u') // unassemble
                {
                    Disassemble(arg);
                    continue;
                }
                else if (cmd[0] == '.') // read breakpoint
                {
                    Int16 ct;
                    while ((arg.Length != 0) && (arg[0] == ' ')) arg = arg.Substring(1);
                    if ((p = arg.IndexOf(' ')) == -1)
                    {
                        ct = -1;
                    }
                    else
                    {
                        if (!ParseWord(arg.Substring(0, p), out ct))
                        {
                            Console.Out.WriteLine("Unrecognized: {0}", arg.Substring(0, p));
                            ct = 0;
                        }
                        else arg = arg.Substring(p + 1);
                    }
                    if (!ParseWord(arg, out word))
                    {
                        Console.Out.WriteLine("Unrecognized: {0}", arg);
                    }
                    CPU.SetBPR(word, ct);
                }
                else if (cmd[0] == '!') // write breakpoint
                {
                    Int16 ct;
                    while ((arg.Length != 0) && (arg[0] == ' ')) arg = arg.Substring(1);
                    if ((p = arg.IndexOf(' ')) == -1)
                    {
                        ct = -1;
                    }
                    else
                    {
                        if (!ParseWord(arg.Substring(0, p), out ct))
                        {
                            Console.Out.WriteLine("Unrecognized: {0}", arg.Substring(0, p));
                            ct = 0;
                        }
                        else arg = arg.Substring(p + 1);
                    }
                    if (!ParseWord(arg, out word))
                    {
                        Console.Out.WriteLine("Unrecognized: {0}", arg);
                    }
                    CPU.SetBPW(word, ct);
                }
                Console.Out.Write("810A>");
            }
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
        }

        static Int16 DISASM_ARG = -1;
        static public void Disassemble(String arg)
        {
            Int16 p;
            if (arg.Length == 0) p = (AUTO_CMD == "unassemble") ? DISASM_ARG : CPU.PC;
            else if (!ParseWord(arg, out p)) Console.Out.WriteLine("Unrecognized: {0}", arg);
            if (p != -1)
            {
                Console.Out.Write("{0}  {1}  >", Octal(p, 5), Op(p, CPU[p], 16));
                AUTO_CMD = "unassemble";
                DISASM_ARG = (Int16)((p + 1) % 32768);
            }
        }

        static public void Load(String arg)
        {
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
            AUTO_CMD = "step";
            STEP_ARG = p;
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
            Int16 n;
            if ((n = RadixValue(s[p++], radix)) == -1) return false;
            result = n;
            result *= sign;
            while ((p < s.Length) && ((n = RadixValue(s[p], radix)) != -1))
            {
                if ((((result * radix) + n) > 32767) && (sign == 1)) break;
                if ((((result * radix) + n) > 32768) && (sign == -1)) break;
                result *= sign;
                result *= radix;
                result += n;
                result *= sign;
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
            String s = Op(addr, word);
            if (s.Length >= width) return s;
            return String.Concat(s, new String(' ', width - s.Length));
        }

        static public String Op(Int16 addr, Int16 word)
        {
            // o ooo xim aaa aaa aaa - memory reference instruction
            // o ooo xis sss aaa aaa - augmented instruction
            Int32 op = (word >> 12) & 15;
            String X = ((word & 0x800) != 0) ? ",X" : null;
            Char I = ((word & 0x400) != 0) ? '*' : ' ';
            if (op == 0)
            {
                Int32 aug = word & 63;
                Int32 sc = (word >> 6) & 15;
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
                    case 30: return "LOB";
                    case 31: return "OVS";
                    case 32: return "TBP";
                    case 33: return "TPB";
                    case 34: return "TBV";
                    case 35: return "TVB";
                    case 36: return String.Format("STX{0}", I); // should show M flag somehow
                    case 37: return String.Format("LIX{0}", I); // should show M flag somehow
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
                switch (aug)
                {
                    case 0: return String.Format("CEU{0} '{1},0", I, Octal(unit));
                    case 1: return String.Format("CEU{0} '{1},1", I, Octal(unit)); // TODO: MAP mode
                    case 2: return String.Format("TEU{0} '{1}", I, Octal(unit));
                    case 4: return String.Format("SNS  {0:D2}", unit & 15);
                    case 6: switch (unit)
                        {
                            case 0: return String.Format("PIE{0}", I);
                            case 1: return String.Format("PID{0}", I);
                            default: return String.Format("DATA  '{0}", Octal(word, 6, '0'));
                        }
                }
            }
            else if (op == 15)
            {
                Int32 aug = (word >> 6) & 7;
                Int16 unit = (Int16)(word & 0x3f);
                switch (aug)
                {
                    case 0: return String.Format("AOP  '{0},0", Octal(unit));
                    case 1: return String.Format("AOP  '{0},1", Octal(unit));
                    case 2: return String.Format("AIP  '{0},0,{1}", Octal(unit), (X == null) ? '0' : '1');
                    case 3: return String.Format("AIP  '{0},1,{1}", Octal(unit), (X == null) ? '0' : '1');
                    case 4: return String.Format("MOP{0} '{1},0", I, Octal(unit));
                    case 5: return String.Format("MOP{0} '{1},1", I, Octal(unit)); // TODO: MAP mode
                    case 6: return String.Format("MIP{0} '{1},0", I, Octal(unit));
                    case 7: return String.Format("MIP{0} '{1},1", I, Octal(unit)); // TODO: MAP mode
                    default: return String.Format("DATA  '{0}", Octal(word, 6, '0'));
                }
            }
            else
            {
                Int16 ea = (Int16)(word & 511);
                if ((word & 0x200) != 0) ea |= (Int16)(addr & 0x7e00); // M flag
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
