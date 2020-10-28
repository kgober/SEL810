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
using System.Collections.Generic;
using System.IO;

namespace Disassembler
{
    [Flags]
    enum Tag : byte
    {
        None        = 0x00,
        Call        = 0x01, // target of a call (SPB)
        Direct      = 0x02, // direct data
        Indirect    = 0x04, // indirect data
        Extended    = 0x08, // extended address
        Map         = 0x10, // instruction map bit set
        EntryPoint  = 0x80, // program entry point
    }

    class Program
    {
        static TextWriter OUT = Console.Out;

        static UInt16[] CORE = new UInt16[32768];   // core memory image
        static String[] CLABEL = new String[32768]; // code labels
        static String[] DLABEL = new String[32768]; // data labels
        static Tag[] TAGS = new Tag[32768];         // address tags

        static List<Int32> ENTRY = new List<Int32>();
        static List<Fragment> FRAGS = new List<Fragment>();

        static void Main(String[] args)
        {
            Int32 addr = 0;
            Int32 p, q = 0;

            if (args.Length == 0)
            {
                Console.Error.WriteLine("Usage: Disassemble [options] dumpfile ...");
                Console.Error.WriteLine("Options:");
                Console.Error.WriteLine("  -e addr - tag 'addr' as a program entry point");
                Console.Error.WriteLine("  -l addr - load following dump file(s) at 'addr'");
                Console.Error.WriteLine("dump files should be core images, not tape files");
                return;
            }

            Int32 ap = 0;
            while (ap < args.Length)
            {
                String arg = args[ap++];
                if ((arg == null) || (arg.Length == 0)) continue;
                if (arg[0] == '-')
                {
                    if (arg.Length == 1)
                    {
                        // - by itself, ignore this for now
                    }
                    else if ((arg[1] == 'e') || (arg[1] == 'E'))
                    {
                        arg = arg.Substring(2);
                        if (arg.Length == 0) arg = args[ap++];
                        ENTRY.Add(Int32.Parse(arg));
                    }
                    else if ((arg[1] == 'l') || (arg[1] == 'L'))
                    {
                        arg = arg.Substring(2);
                        if (arg.Length == 0) arg = args[ap++];
                        addr = Int32.Parse(arg);
                    }
                    else
                    {
                        // unrecognized option, ignore for now
                    }
                }
                else
                {
                    Byte[] buf = File.ReadAllBytes(arg);
                    Int32 end = Load(addr, buf);
                    if (end > q) q = end;
                }
            }

            // identify code fragments (sequences of valid instructions terminated by HLT, LOB, or BRU)
            FindFragments();

            // identify jumps between fragments
            foreach (Fragment frag in FRAGS) Connect(frag);

            // ensure entry points can't be considered dead
            foreach (Int32 entry in ENTRY)
            {
                TAGS[entry] |= Tag.EntryPoint;
                Fragment frag = FindFragment(entry);
                if (frag != null) frag.Source.Add(frag);
            }

            // exclude dead fragments (fragments that are never entered, and which don't enter other fragments)
            List<Fragment> L = new List<Fragment>();
            foreach (Fragment frag in FRAGS) if ((frag.Source.Count == 0) && (frag.Target.Count == 0)) L.Add(frag);
            foreach (Fragment frag in L) FRAGS.Remove(frag);

            // generate labels
            foreach (Fragment frag in FRAGS) DoLabels(frag);

            // generate listing
            for (p = addr; p < q; p++)
            {
                UInt16 word = CORE[p];
                String label = CLABEL[p];
                if (label == null) label = DLABEL[p];
                if (label != null) label = String.Concat(label, ":");
                String text = Decode(p, word);
                if ((TAGS[p] & Tag.EntryPoint) != 0)
                {
                    OUT.WriteLine();
                    OUT.WriteLine();
                    text = String.Format("{0,-16}; *** ENTRY POINT ***", text);
                }
                else if ((TAGS[p] & Tag.Call) != 0)
                {
                    OUT.WriteLine();
                    OUT.WriteLine();
                }
                else if (IsFragmentStart(p))
                {
                    OUT.WriteLine();
                }
                OUT.WriteLine("{0}  {1:x4}[{2}{3}]{4}  {5,-9} {6}", Octal(p), word, ASCII(word >> 8), ASCII(word), Octal(word, 6), label, text);
            }
        }

        static Int32 Load(Int32 addr, Byte[] buf)
        {
            Int32 p = addr;
            Int32 q = 0;
            while (q < buf.Length)
            {
                CORE[p] &= 0x00ff;
                CORE[p] |= (UInt16)(buf[q++] << 8);
                if (q < buf.Length)
                {
                    CORE[p] &= 0xff00;
                    CORE[p] |= buf[q++];
                }
                p++;
            }
            return p;
        }

        class Fragment
        {
            Int32 mStart;
            Int32 mLength;
            public readonly List<Fragment> Target = new List<Fragment>();
            public readonly List<Fragment> Source = new List<Fragment>();

            public Fragment(Int32 start, Int32 length)
            {
                mStart = start;
                mLength = length;
            }

            public Int32 Start
            {
                get { return mStart; }
            }

            public Int32 Length
            {
                get { return mLength; }
            }

            public void AdjustStart(Int32 amount)
            {
                mStart += amount;
                mLength -= amount;
            }
        }

        // find code fragments (sequences of valid instructions terminated by HLT, LOB, or BRU)
        static void FindFragments()
        {
            Fragment frag;
            Int32 start = 0;
            while (start < 32768)
            {
                if ((CORE[start] != 0) && ((frag = TryFragment(start)) != null))
                {
                    FRAGS.Add(frag);
                    start += frag.Length;
                }
                else
                {
                    start++;
                }
            }
        }

        static Fragment TryFragment(Int32 start)
        {
            Int32 limit = 32768;
            Int32 min = start;
            Int32 addr = start;
            while (addr < limit)
            {
                UInt16 word = CORE[addr++];
                Int32 op = (word >> 12) & 15;
                if (op == 0) // augmented 00 instructions
                {
                    Int32 sc = (word >> 6) & 15;
                    op = ((word >> 4) & 0xc0) | (word & 63);
                    if (op == 0) // HLT
                    {
                        if (sc != 0) return null;
                        limit = addr;
                    }
                    else if (op == 30) // LOB
                    {
                        if (sc != 0) return null;
                        if (CORE[addr++] >= 32768) return null;
                        limit = addr;
                    }
                    else if ((op == 36) || (op == 37) || (op == 36 + 64) || (op == 37 + 64)) // STX, LIX
                    {
                        if ((sc != 0) && (sc != 8)) return null;
                        addr++; // operand
                    }
                    else if (op == 17) // SAS
                    {
                        if (sc != 0) return null;
                        min = addr + 3;
                    }
                    else if (((op >= 18) && (op <= 22)) || (op == 26) || (op == 40)) // SAZ, SAN, SAP, SOF, IBS, SNO, SXB
                    {
                        if (sc != 0) return null;
                        min = addr + 2;
                    }
                    else if (op == 41) // IXS
                    {
                        min = addr + 2;
                    }
                    else if ((op >= 8) && (op <= 15)) // RSA, LSA, FRA, FLL, FRL, RSL, LSL, FLA
                    {
                        // valid with any shift count, do nothing
                    }
                    else if ((op >= 1) && (op <= 43)) // all others
                    {
                        if (sc != 0) return null;
                    }
                    else
                    {
                        return null;
                    }
                }
                else if (op == 11) // augmented 13 instructions
                {
                    Int32 unit = word & 63;
                    Int32 mod = (word >> 9) & 3;
                    op = ((word >> 8) & 8) | ((word >> 6) & 7);
                    if ((op == 0) || (op == 2)) // CEU (skip mode), TEU
                    {
                        if (mod == 1) return null;
                        addr++; // operand
                        min = addr + 2;
                    }
                    else if (op == 1) // CEU (wait mode)
                    {
                        if (mod == 1) return null;
                        addr++; // operand
                    }
                    else if (op == 4) // SNS
                    {
                        if (mod != 0) return null;
                        if (unit > 15) return null;
                        min = addr + 2;
                    }
                    else if (op == 6) // PIE, PID
                    {
                        if (mod != 0) return null;
                        if (unit > 1) return null;
                        addr++; // operand
                    }
                    else
                    {
                        return null;
                    }
                }
                else if (op == 15) // augmented 17 instructions
                {
                    Int32 unit = word & 63;
                    Int32 mod = (word >> 9) & 3;
                    op = ((word >> 8) & 8) | ((word >> 6) & 7);
                    if ((op == 0) || (op == 2) || (op == 2 + 8)) // AOP (skip mode), AIP (skip mode), AIP (merge, skip mode)
                    {
                        if (mod != 0) return null;
                        min = addr + 2;
                    }
                    else if ((op == 1) || (op == 3) || (op == 3 + 8)) // AOP (wait mode), AIP (wait mode), AIP (merge, wait mode)
                    {
                        if (mod != 0) return null;
                    }
                    else if ((op == 4) || (op == 6)) // MOP (skip mode), MIP (skip mode)
                    {
                        if (mod == 1) return null;
                        addr++; // operand
                        min = addr + 2;
                    }
                    else if ((op == 5) || (op == 7)) // MOP (wait mode), MIP (wait mode)
                    {
                        if (mod == 1) return null;
                        addr++; // operand
                    }
                }
                else if (op == 9) // BRU
                {
                    limit = addr;
                }
                else if (op == 10) // SPB
                {
                    min = addr + 1;
                }
                else if (op == 12) // IMS
                {
                    // if IMS nnn is followed by BRU* nnn, assume nnn contains an address and the skip doesn't happen
                    if (((word & 0xfc00) == 0xc000) && ((CORE[addr] & 0xfc00) == 0x9400) && ((word & 0x3ff) == (CORE[addr] & 0x3ff))) min = addr + 1; 
                    else min = addr + 2;
                }
                else if (op == 13) // CMA
                {
                    min = addr + 3;
                }
                if (limit < min) limit = 32768;
            }
            return new Fragment(start, limit - start);
        }

        static void Connect(Fragment frag)
        {
            Int32 addr = frag.Start;
            Int32 limit = frag.Start + frag.Length;
            while (addr < limit)
            {
                UInt16 word = CORE[addr++];
                Int32 op = (word >> 12) & 15;
                if (op == 0) // augmented 00 instructions
                {
                    op = word & 63;
                    if (op == 30) // LOB
                    {
                        Int32 target = CORE[addr++];
                        Fragment tf = FindFragment(target);
                        if ((tf != null) && (tf != frag)) Connect(frag, tf);
                    }
                    else if ((op == 36) || (op == 37)) // STX, LIX
                    {
                        addr++; // operand
                    }
                }
                else if (op == 11) // augmented 13 instructions
                {
                    op = (word >> 6) & 7;
                    if (op != 4) addr++; // CEU, TEU, PIE, PID operands
                }
                else if (op == 15) // augmented 17 instructions
                {
                    if ((word & 0x0100) != 0) addr++; // MOP, MIP operands
                }
                else if (op == 9) // BRU
                {
                    Int32 target = EA(addr - 1, word);
                    Fragment tf = FindFragment(target);
                    if ((tf != null) && (tf != frag)) Connect(frag, tf);
                }
                else if (op == 10) // SPB
                {
                    Int32 target = EA(addr - 1, word) + 1;
                    Fragment tf = FindFragment(target);
                    if ((tf != null) && (tf != frag)) Connect(frag, tf);
                }
            }
        }

        static void Connect(Fragment source, Fragment target)
        {
            if (!source.Target.Contains(target)) source.Target.Add(target);
            if (!target.Source.Contains(source)) target.Source.Add(source);
        }

        static Boolean IsFragmentStart(Int32 addr)
        {
            Fragment frag = FindFragment(addr);
            if (frag == null) return false;
            return (addr == frag.Start);
        }

        static Fragment FindFragment(Int32 addr)
        {
            foreach (Fragment frag in FRAGS)
            {
                if ((addr >= frag.Start) && (addr < (frag.Start + frag.Length))) return frag;
            }
            return null;
        }

        // assign labels to references and tag operands
        static void DoLabels(Fragment frag)
        {
            Int32 addr = frag.Start;
            Int32 limit = frag.Start + frag.Length;
            while (addr < limit)
            {
                UInt16 word = CORE[addr++];
                Int32 op = (word >> 12) & 15;
                if (op == 0) // augmented 00 instructions
                {
                    op = word & 63;
                    if (op == 30) // LOB
                    {
                        Int32 target = CORE[addr];
                        CLABEL[target] = String.Concat("L", Octal(target));
                        TAGS[addr++] |= Tag.Extended;
                    }
                    else if ((op == 36) || (op == 37)) // STX, LIX
                    {
                        Boolean ind = ((word & 0x400) != 0);
                        Boolean map = ((word & 0x200) != 0);
                        Int32 target = (ind) ? Indirect(addr, map) : addr;
                        if ((ind) && (DLABEL[target] == null)) DLABEL[target] = String.Concat("D", Octal(target));
                        if (map) TAGS[addr] |= Tag.Map;
                        TAGS[addr++] |= (ind) ? Tag.Indirect : Tag.Direct;
                    }
                }
                else if (op == 11) // augmented 13 instructions
                {
                    op = (word >> 6) & 7;
                    if (op < 3) // CEU, TEU
                    {
                        Boolean ind = ((word & 0x400) != 0);
                        Boolean map = ((word & 0x200) != 0);
                        Int32 target = (ind) ? Indirect(addr, map) : addr;
                        if ((ind) && (DLABEL[target] == null)) DLABEL[target] = String.Concat("D", Octal(target));
                        if (map) TAGS[addr] |= Tag.Map;
                        TAGS[addr++] |= (ind) ? Tag.Indirect : Tag.Direct;
                    }
                    else if (op != 4) TAGS[addr++] |= Tag.Direct; // PIE, PID operand
                }
                else if (op == 15) // augmented 17 instructions
                {
                    if ((word & 0x0100) != 0) // MOP, MIP
                    {
                        Boolean ind = ((word & 0x400) != 0);
                        Boolean map = ((word & 0x200) != 0);
                        Int32 target = (ind) ? Indirect(addr, map) : addr;
                        if ((ind) && (DLABEL[target] == null)) DLABEL[target] = String.Concat("D", Octal(target));
                        if (map) TAGS[addr] |= Tag.Map;
                        TAGS[addr++] |= (ind) ? Tag.Indirect : Tag.Direct;
                    }
                }
                else if (op == 9) // BRU
                {
                    Int32 target = EA(addr - 1, word);
                    CLABEL[target] = String.Concat("L", Octal(target));
                }
                else if (op == 10) // SPB
                {
                    Int32 target = EA(addr - 1, word);
                    CLABEL[target] = String.Concat("S", Octal(target));
                    TAGS[target] |= Tag.Call;
                    Fragment lf = FindFragment(target);
                    Fragment tf = FindFragment(target + 1);
                    if ((lf == null) && (tf != null)) tf.AdjustStart(-1);
                }
                else
                {
                    Int32 target = EA(addr - 1, word);
                    if (DLABEL[target] == null) DLABEL[target] = String.Concat("D", Octal(target));
                }
            }
        }

        static String Decode(Int32 addr, Int32 word)
        {
            if (((TAGS[addr] & Tag.Call) != 0) && ((TAGS[addr] & Tag.EntryPoint) == 0)) return "DATA **";
            if ((TAGS[addr] & Tag.Direct) != 0) return String.Format("DATA '{0}", Octal(word, 6));
            if ((TAGS[addr] & Tag.Indirect) != 0)
            {
                Boolean idx = ((word & 0x8000) != 0);
                Boolean ind = ((word & 0x4000) != 0);
                Int32 target = word & 0x3fff;
                if ((TAGS[addr] & Tag.Map) != 0) target |= addr & 0x4000;
                String arg = CLABEL[target];
                if (arg == null) arg = DLABEL[target];
                if (arg == null) arg = String.Concat("'", Octal(target));
                return String.Format("DAC{0} {1}{2}", (ind) ? '*' : ' ', arg, (idx) ? ",1" : null);
            }
            if ((TAGS[addr] & Tag.Extended) != 0)
            {
                Int32 target = word & 0x7fff;
                String arg = CLABEL[target];
                if (arg == null) arg = DLABEL[target];
                if (arg == null) arg = String.Concat("'", Octal(target));
                return String.Format("EAC  {0}", arg);
            }
            Int32 op = (word >> 12) & 15;
            if (op == 0) // augmented 00 instructions
            {
                Int32 sc = (word >> 6) & 63;
                op = word & 63;
                if ((op >= 8) && (op <= 15) && (sc < 16))
                {
                    switch (op)
                    {
                        case 8: return String.Format("RSA  {0:D0}", sc);
                        case 9: return String.Format("LSA  {0:D0}", sc);
                        case 10: return String.Format("FRA  {0:D0}", sc);
                        case 11: return String.Format("FLL  {0:D0}", sc);
                        case 12: return String.Format("FRL  {0:D0}", sc);
                        case 13: return String.Format("RSL  {0:D0}", sc);
                        case 14: return String.Format("LSL  {0:D0}", sc);
                        default: return String.Format("FLA  {0:D0}", sc);
                    }
                }
                else if (((op == 36) || (op == 37)) && ((sc == 16) || (sc == 24))) // STX, LIX
                {
                    return (op == 36) ? "STX*" : "LIX*";
                }
                else if ((op <= 43) && (sc == 0))
                {
                    switch (op)
                    {
                        case 0: return "HLT";
                        case 1: return "RNA";
                        case 2: return "NEG";
                        case 3: return "CLA";
                        case 4: return "TBA";
                        case 5: return "TAB";
                        case 6: return "IAB";
                        case 7: return "CSB";
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
                        case 36: return "STX";
                        case 37: return "LIX";
                        case 38: return "XPX";
                        case 39: return "XPB";
                        case 40: return "SXB";
                        case 41: return "IXS";
                        case 42: return "TAX";
                        default: return "TXA";
                    }
                }
                else
                {
                    Boolean idx = ((word & 0x800) != 0);
                    Boolean ind = ((word & 0x400) != 0);
                    Boolean map = ((word & 0x200) != 0);
                    Int32 target = word & 0x1ff;
                    if (map) target |= addr & 0x7e00;
                    return String.Format("ZZZ{0} '{1}{2}", (ind) ? '*' : ' ', Octal(target), (idx) ? ",1" : null);
                }
            }
            else if (op == 11) // augmented 13 instructions
            {
                Int32 unit = word & 63;
                Int32 mod = (word >> 9) & 7;
                op = (word >> 6) & 7;
                if ((op <= 2) && (mod != 1))
                {
                    Boolean ind = ((word & 0x400) != 0);
                    switch (op)
                    {
                        case 0: return String.Format("CEU{0} {1:D0}", (ind) ? '*' : ' ', unit);
                        case 1: return String.Format("CEU{0} {1:D0},W", (ind) ? '*' : ' ', unit);
                        default: return String.Format("TEU{0} {1:D0}", (ind) ? '*' : ' ', unit);
                    }
                }
                else if ((op == 4) && (mod == 0) && (unit < 16))
                {
                    return String.Format("SNS  {0:D0}", unit);
                }
                else if ((op == 6) && (mod == 0) && (unit < 2))
                {
                    return (unit == 0) ? "PIE" : "PID";
                }
                else
                {
                    return String.Format("DATA '{0}", Octal(word, 6));
                }
            }
            else if (op == 15) // augmented 17 instructions
            {
                Int32 unit = word & 63;
                Int32 mod = (word >> 9) & 7;
                op = (word >> 6) & 7;
                if (mod == 0)
                {
                    switch (op)
                    {
                        case 0: return String.Format("AOP  {0:D0}", unit);
                        case 1: return String.Format("AOP  {0:D0},W", unit);
                        case 2: return String.Format("AIP  {0:D0}", unit);
                        case 3: return String.Format("AIP  {0:D0},W", unit);
                        case 4: return String.Format("MOP  {0:D0}", unit);
                        case 5: return String.Format("MOP  {0:D0},W", unit);
                        case 6: return String.Format("MIP  {0:D0}", unit);
                        default: return String.Format("MIP  {0:D0},W", unit);
                    }
                }
                else if ((mod == 2) || (mod == 3))
                {
                    switch (op)
                    {
                        case 4: return String.Format("MOP* {0:D0}", unit);
                        case 5: return String.Format("MOP* {0:D0},W", unit);
                        case 6: return String.Format("MIP* {0:D0}", unit);
                        case 7: return String.Format("MIP* {0:D0},W", unit);
                        default: return String.Format("DATA '{0}", Octal(word, 6));
                    }
                }
                else if (mod == 4)
                {
                    switch (op)
                    {
                        case 2: return String.Format("AIP  {0:D0},R", unit);
                        case 3: return String.Format("AIP  {0:D0},W,R", unit);
                        default: return String.Format("DATA '{0}", Octal(word, 6));
                    }
                }
                else
                {
                    return String.Format("DATA '{0}", Octal(word, 6));
                }
            }
            else
            {
                Boolean idx = ((word & 0x800) != 0);
                Boolean ind = ((word & 0x400) != 0);
                Boolean map = ((word & 0x200) != 0);
                Int32 target = word & 0x1ff;
                if (map) target |= addr & 0x7e00;
                String arg = CLABEL[target];
                if (arg == null) arg = DLABEL[target];
                if (arg == null) arg = String.Concat("'", Octal(target));
                switch (op)
                {
                    case 1: return String.Format("LAA{0} {1}{2}", (ind) ? '*' : ' ', arg, (idx) ? ",1" : null);
                    case 2: return String.Format("LBA{0} {1}{2}", (ind) ? '*' : ' ', arg, (idx) ? ",1" : null);
                    case 3: return String.Format("STA{0} {1}{2}", (ind) ? '*' : ' ', arg, (idx) ? ",1" : null);
                    case 4: return String.Format("STB{0} {1}{2}", (ind) ? '*' : ' ', arg, (idx) ? ",1" : null);
                    case 5: return String.Format("AMA{0} {1}{2}", (ind) ? '*' : ' ', arg, (idx) ? ",1" : null);
                    case 6: return String.Format("SMA{0} {1}{2}", (ind) ? '*' : ' ', arg, (idx) ? ",1" : null);
                    case 7: return String.Format("MPY{0} {1}{2}", (ind) ? '*' : ' ', arg, (idx) ? ",1" : null);
                    case 8: return String.Format("DIV{0} {1}{2}", (ind) ? '*' : ' ', arg, (idx) ? ",1" : null);
                    case 9: return String.Format("BRU{0} {1}{2}", (ind) ? '*' : ' ', arg, (idx) ? ",1" : null);
                    case 10: return String.Format("SPB{0} {1}{2}", (ind) ? '*' : ' ', arg, (idx) ? ",1" : null);
                    case 12: return String.Format("IMS{0} {1}{2}", (ind) ? '*' : ' ', arg, (idx) ? ",1" : null);
                    case 13: return String.Format("CMA{0} {1}{2}", (ind) ? '*' : ' ', arg, (idx) ? ",1" : null);
                    default: return String.Format("AMB{0} {1}{2}", (ind) ? '*' : ' ', arg, (idx) ? ",1" : null);
                }
            }
        }

        static Int32 EA(Int32 PC, Int32 IR)
        {
            Boolean ind = ((IR & 0x400) != 0);
            Boolean map = ((IR & 0x200) != 0);
            Int32 addr = IR & 511;
            if (map) addr |= PC & 0x7e00;
            if (ind) addr = Indirect(PC, addr, true);
            return addr;
        }

        static Int32 Indirect(Int32 addr, Boolean map)
        {
            return Indirect(addr, addr, map);
        }

        static Int32 Indirect(Int32 PC, Int32 addr, Boolean map)
        {
            Boolean ind;
            do
            {
                DLABEL[addr] = String.Concat("I", Octal(addr));
                TAGS[addr] |= Tag.Indirect;
                Int32 word = CORE[addr];
                ind = ((word & 0x4000) != 0);
                addr = word & 0x3fff;
                if (map) addr |= PC & 0x4000;
            } while (ind);
            return addr;
        }

        static Char ASCII(Int32 value)
        {
            value &= 127;
            if (value < 32) return '~';
            if (value == 127) return '~';
            return (Char)value;
        }

        static String Octal(Int32 value)
        {
            return Octal(value, 5);
        }

        static String Octal(Int32 value, Int32 width)
        {
            String num = Convert.ToString(value, 8);
            Int32 len = num.Length;
            if (len >= width) return num;
            String pad = new String('0', width - len);
            return String.Concat(pad, num);
        }
    }
}
