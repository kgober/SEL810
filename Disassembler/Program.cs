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
// Consider merging adjacent fragments
// Allow a non-instruction after SPB (inline argument)
// Identify reachable code
// Generate references for reads and writes
// remove Call tags without HasReturn
// treat jumps to invalid instructions as invalid themselves
// non-fragment instructions both read and written are probably data
// non-fragment instructions in general are probably data
// LOB/PIE/PID/CEU/TEU/MOP/MIP with a Call tag is probably not an instruction, but the next word probably is


using System;
using System.Collections.Generic;
using System.IO;

namespace Disassembler
{
    [Flags]
    enum CTag : byte
    {
        None        = 0x00,
        Valid       = 0x01, // this is a valid opcode
        Reachable   = 0x02, // this is reachable code
        Branch      = 0x04, // target of a branch (BRU, LOB)
        Call        = 0x08, // target of a call (SPB)
        HasReturn   = 0x10, // target of a return (BRU*)
        Return      = 0x20, // return from subroutine
        EntryPoint  = 0x80, // program entry point
    }

    [Flags]
    enum DTag : byte
    {
        None        = 0x00,
        Read        = 0x01, // an instruction reads from this address
        Write       = 0x02, // in instruction writes to this address
        Indirect    = 0x04, // this is an indirect address word (DAC)
        Extended    = 0x08, // this is an extended address word (EAC)
        Map0        = 0x10, // Map was set for indirect, from lower half of memory
        Map1        = 0x20, // Map was set for indirect, from upper half of memory
        Immediate   = 0x40, // this is an immediate operand
        Address     = 0x80, // this is an address operand
    }

    class Program
    {
        static TextWriter OUT = Console.Out;
        static Boolean DEBUG = false;

        static UInt16[] CORE = new UInt16[32768];   // core memory image
        static CTag[] CTAG = new CTag[32768];       // code tags
        static DTag[] DTAG = new DTag[32768];       // data tags

        static List<Int32> ENTRY = new List<Int32>();
        static List<Fragment> FRAGS = new List<Fragment>();

        static void Main(String[] args)
        {
            Int32 start = 0;
            Int32 p = 32768, q = 0;

            if (args.Length == 0)
            {
                Console.Error.WriteLine("Usage: Disassemble option ...");
                Console.Error.WriteLine("Options:");
                Console.Error.WriteLine("  -e addr - tag 'addr' as a program entry point");
                Console.Error.WriteLine("  -i addr imagefile - load core image file at 'addr'");
                Console.Error.WriteLine("  -a tapefile - load absolute tape file");
                Console.Error.WriteLine("  -s num - skip first 'num' bytes of next file");
                Console.Error.WriteLine("  -d - enable extra debug output");
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
                    else if ((arg[1] == 'd') || (arg[1] == 'D'))
                    {
                        DEBUG = true;
                    }
                    else if ((arg[1] == 'e') || (arg[1] == 'E'))
                    {
                        arg = arg.Substring(2);
                        if (arg.Length == 0) arg = args[ap++];
                        ENTRY.Add(Int32.Parse(arg));
                    }
                    else if ((arg[1] == 'i') || (arg[1] == 'I'))
                    {
                        arg = arg.Substring(2);
                        if (arg.Length == 0) arg = args[ap++];
                        Int32 addr = Int32.Parse(arg);
                        arg = args[ap++];
                        Byte[] buf = File.ReadAllBytes(arg);
                        Int32 end;
                        if (LoadImage(buf, start, addr, out end))
                        {
                            if (addr < p) p = addr;
                            if (end > q) q = end;
                        }
                        start = 0;
                    }
                    else if ((arg[1] == 'a') || (arg[1] == 'A'))
                    {
                        arg = arg.Substring(2);
                        if (arg.Length == 0) arg = args[ap++];
                        Byte[] buf = File.ReadAllBytes(arg);
                        Int32 addr, end;
                        if (LoadAbsolute(buf, start, out addr, out end))
                        {
                            if (addr < p) p = addr;
                            if (end > q) q = end;
                        }
                        start = 0;
                    }
                    else if ((arg[1] == 's') || (arg[1] == 'S'))
                    {
                        arg = arg.Substring(2);
                        if (arg.Length == 0) arg = args[ap++];
                        start = Int32.Parse(arg);
                    }
                    else
                    {
                        Console.Error.WriteLine("Unrecognized option: {0}", arg);
                    }
                }
                else
                {
                    Console.Error.WriteLine("Unrecognized option: {0}", arg);
                }
            }

            // identify code fragments (sequences of valid instructions terminated by HLT, LOB, or BRU)
            IdentifyFragments();

            // identify jumps between fragments
            foreach (Fragment frag in FRAGS) Connect(frag);

            // ensure entry points can't be considered dead
            foreach (Int32 entry in ENTRY)
            {
                CTAG[entry] |= CTag.EntryPoint;
                Fragment frag = FindFragment(entry);
                if (frag != null) frag.Source.Add(frag);
            }

            // assign tags
            foreach (Fragment frag in FRAGS) if (!frag.Dead) AssignTags(frag);

            // generate listing
            Fragment current = FindFragment(p);
            while (p < q)
            {
                Fragment frag = FindFragment(p);
                UInt16 word = CORE[p];
                String label = Label(p, false);
                String text = Disassemble(p, word);
                if (CTagIs(p, CTag.Call) || CTagIs(p, CTag.EntryPoint))
                {
                    OUT.WriteLine();
                    OUT.WriteLine();
                }
                else if (frag != current)
                {
                    OUT.WriteLine();
                }
                if (DEBUG)
                {
                    Char F = (frag == null) ? ',' : (frag.Dead) ? ';' : '#';
                    Char V = (CTagIs(p, CTag.Valid)) ? 'V' : '-';
                    Char E = (CTagIs(p, CTag.EntryPoint)) ? 'E' : '-';
                    Char B = (CTagIs(p, CTag.Branch)) ? 'B' : '-';
                    Char S = (CTagIs(p, CTag.Call)) ? 'S' : '-';
                    Char R = (CTagIs(p, CTag.HasReturn) || CTagIs(p, CTag.Return)) ? 'R' : '-';
                    Boolean r = DTagIs(p, DTag.Read);
                    Boolean w = DTagIs(p, DTag.Write);
                    Char D = (r && w) ? 'D' : (r) ? 'R' : (w) ? 'W' : '-';
                    Char I = (DTagIs(p, DTag.Indirect)) ? 'I' : '-';
                    Char L = (DTagIs(p, DTag.Extended)) ? 'L' : '-';
                    Boolean m0 = DTagIs(p, DTag.Map0);
                    Boolean m1 = DTagIs(p, DTag.Map1);
                    Char M = (m0 && m1) ? '2' : (m1) ? '1' : (m0) ? '0' : '-';
                    Char A = (DTagIs(p, DTag.Address)) ? 'A' : (DTagIs(p, DTag.Immediate)) ? 'I' : '-';
                    text = String.Format("{0,-16}{1} {2}{3}{4}{5}{6} {7}{8}{9}{10}{11}", text, F, V, E, B, S, R, D, I, L, M, A);
                }
                OUT.WriteLine("{0}  {1:x4}[{2}{3}]{4}  {5,-9} {6}", Octal(p), word, ASCII(word >> 8), ASCII(word), Octal(word, 6), label, text);
                current = frag;
                p++;
            }
        }

        static Boolean LoadImage(Byte[] buf, Int32 startAt, Int32 loadStart, out Int32 loadEnd)
        {
            Int32 p = loadStart;
            Int32 q = startAt;
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
            loadEnd = p;
            return true;
        }

        static Boolean LoadAbsolute(Byte[] buf, Int32 startAt, out Int32 loadStart, out Int32 loadEnd)
        {
            loadStart = 32768;
            loadEnd = -1;
            Int32 p = startAt;
            while ((p < buf.Length) && (buf[p] != 0xff)) p++; // skip leader
            if ((p++ + 4) >= buf.Length) return false;
            Int32 addr = buf[p++] << 8;
            addr |= buf[p++];
            Int32 len = -1;
            for (Int32 i = 0; i < 2; i++) len = (len << 8) | buf[p++];
            len = -len;
            if ((p + len * 2 + (len + 63) / 32 + 2) >= buf.Length) return false;
            for (Int32 i = 0; i < len; i += 64)
            {
                Int32 sum = 0;
                for (Int32 j = 0; j < 64; j++)
                {
                    if ((i + j) == len) break;
                    CORE[addr + i + j] = (UInt16)(buf[p++] << 8);
                    CORE[addr + i + j] |= buf[p++];
                    sum += CORE[addr + i + j];
                }
                sum &= 0xffff;
                UInt16 checksum = (UInt16)(buf[p++] << 8);
                checksum |= buf[p++];
                if (sum != checksum)
                {
                    Console.Error.WriteLine("Checksum mismatch");
                    return false;
                }
            }
            loadStart = addr;
            loadEnd = addr + len;
            return true;
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

            public Boolean Dead
            {
                get { return ((Target.Count == 0) && (Source.Count == 0)); }
            }

            public void AdjustStart(Int32 amount)
            {
                mStart += amount;
                mLength -= amount;
            }
        }

        // identify code fragments (sequences of valid instructions terminated by HLT, LOB, or BRU)
        static void IdentifyFragments()
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

        // look for a sequence of valid instructions starting at 'start'
        // applies 'Valid' code tags
        static Fragment TryFragment(Int32 start)
        {
            Int32 limit = 32768;
            Int32 min = start;
            Int32 addr = start;
            while (addr < limit)
            {
                UInt16 word = CORE[addr];
                Int32 op = (word >> 12) & 15;
                if (op == 0) // augmented 00 instructions
                {
                    Int32 sc = (word >> 6) & 15;
                    op = ((word >> 4) & 0xc0) | (word & 63);
                    if (op == 0) // HLT
                    {
                        if (sc != 0) return null;
                        CTAG[addr++] |= CTag.Valid;
                        limit = addr;
                    }
                    else if (op == 30) // LOB
                    {
                        if (sc != 0) return null;
                        if (CORE[addr + 1] >= 32768) return null;
                        CTAG[addr++] |= CTag.Valid;
                        addr++; // operand
                        limit = addr;
                    }
                    else if ((op == 36) || (op == 37) || (op == 36 + 64) || (op == 37 + 64)) // STX, LIX
                    {
                        if ((sc != 0) && (sc != 8)) return null;
                        CTAG[addr++] |= CTag.Valid;
                        addr++; // operand
                    }
                    else if (op == 17) // SAS
                    {
                        if (sc != 0) return null;
                        CTAG[addr++] |= CTag.Valid;
                        min = addr + 3;
                    }
                    else if (((op >= 18) && (op <= 22)) || (op == 26) || (op == 40)) // SAZ, SAN, SAP, SOF, IBS, SNO, SXB
                    {
                        if (sc != 0) return null;
                        CTAG[addr++] |= CTag.Valid;
                        min = addr + 2;
                    }
                    else if (op == 41) // IXS
                    {
                        CTAG[addr++] |= CTag.Valid;
                        min = addr + 2;
                    }
                    else if ((op >= 8) && (op <= 15)) // RSA, LSA, FRA, FLL, FRL, RSL, LSL, FLA
                    {
                        CTAG[addr++] |= CTag.Valid;
                    }
                    else if ((op >= 1) && (op <= 43)) // all others
                    {
                        if (sc != 0) return null;
                        CTAG[addr++] |= CTag.Valid;
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
                        CTAG[addr++] |= CTag.Valid;
                        addr++; // operand
                        min = addr + 2;
                    }
                    else if (op == 1) // CEU (wait mode)
                    {
                        if (mod == 1) return null;
                        CTAG[addr++] |= CTag.Valid;
                        addr++; // operand
                    }
                    else if (op == 4) // SNS
                    {
                        if (mod != 0) return null;
                        if (unit > 15) return null;
                        CTAG[addr++] |= CTag.Valid;
                        min = addr + 2;
                    }
                    else if (op == 6) // PIE, PID
                    {
                        if (mod != 0) return null;
                        if (unit > 1) return null;
                        CTAG[addr++] |= CTag.Valid;
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
                        CTAG[addr++] |= CTag.Valid;
                        min = addr + 2;
                    }
                    else if ((op == 1) || (op == 3) || (op == 3 + 8)) // AOP (wait mode), AIP (wait mode), AIP (merge, wait mode)
                    {
                        if (mod != 0) return null;
                        CTAG[addr++] |= CTag.Valid;
                    }
                    else if ((op == 4) || (op == 6)) // MOP (skip mode), MIP (skip mode)
                    {
                        if (mod == 1) return null;
                        CTAG[addr++] |= CTag.Valid;
                        addr++; // operand
                        min = addr + 2;
                    }
                    else if ((op == 5) || (op == 7)) // MOP (wait mode), MIP (wait mode)
                    {
                        if (mod == 1) return null;
                        CTAG[addr++] |= CTag.Valid;
                        addr++; // operand
                    }
                    else
                    {
                        return null;
                    }
                }
                else if (op == 9) // BRU
                {
                    CTAG[addr++] |= CTag.Valid;
                    limit = addr;
                }
                else if (op == 10) // SPB
                {
                    CTAG[addr++] |= CTag.Valid;
                    min = addr + 1;
                }
                else if (op == 12) // IMS
                {
                    CTAG[addr++] |= CTag.Valid;
                    // if IMS nnn is followed by BRU* nnn, assume nnn contains an address and the skip doesn't happen
                    if (((word & 0xfc00) == 0xc000) && ((CORE[addr] & 0xfc00) == 0x9400) && ((word & 0x3ff) == (CORE[addr] & 0x3ff))) min = addr + 1; 
                    else min = addr + 2;
                }
                else if (op == 13) // CMA
                {
                    CTAG[addr++] |= CTag.Valid;
                    min = addr + 3;
                }
                else
                {
                    CTAG[addr++] |= CTag.Valid;
                }
                if (limit < min) limit = 32768;
            }
            return new Fragment(start, limit - start);
        }

        // find fragments jumped to by this fragment
        // applies Branch and Call code tags, and adjusts fragments to include SPB return address
        static void Connect(Fragment frag)
        {
            Int32 addr = frag.Start;
            Int32 limit = frag.Start + frag.Length;
            while (addr < limit)
            {
                UInt16 word = CORE[addr];
                if (!CTagIs(addr++, CTag.Valid)) continue; // skip over operands
                Int32 op = (word >> 12) & 15;
                if (op == 0) // augmented 00 instructions
                {
                    op = word & 63;
                    if (op == 30) // LOB
                    {
                        Int32 target = CORE[addr++];
                        CTAG[target] |= CTag.Branch;
                        Fragment tf = FindFragment(target);
                        if (tf == null) continue;
                        if (tf != frag) Connect(frag, tf);
                    }
                }
                else if (op == 9) // BRU
                {
                    Int32 target = MemOpAddr(addr - 1, word);
                    CTAG[target] |= CTag.Branch;
                    Fragment tf = FindFragment(target);
                    if (tf == null) continue;
                    if (tf != frag) Connect(frag, tf);
                }
                else if (op == 10) // SPB
                {
                    Int32 target = MemOpAddr(addr - 1, word);
                    CTAG[target] |= CTag.Call;
                    Fragment tf = FindFragment(target + 1);
                    if (tf == null) continue;
                    if (tf != frag) Connect(frag, tf);
                    if (FindFragment(target) == null) tf.AdjustStart(-1);
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

        static Boolean CTagIs(Int32 addr, CTag flags)
        {
            return CTagIs(CTAG[addr], flags);
        }

        static Boolean CTagIs(CTag tag, CTag flags)
        {
            if (flags == CTag.None) return (tag == flags);
            return ((tag & flags) == flags);
        }

        static Boolean DTagIs(Int32 addr, DTag flags)
        {
            return DTagIs(DTAG[addr], flags);
        }

        static Boolean DTagIs(DTag tag, DTag flags)
        {
            if (flags == DTag.None) return (tag == flags);
            return ((tag & flags) == flags);
        }

        // assign tags to locations referenced by a fragment
        static void AssignTags(Fragment frag)
        {
            Int32 addr = frag.Start;
            Int32 limit = frag.Start + frag.Length;
            while (addr < limit)
            {
                UInt16 word = CORE[addr++];
                if (!CTagIs(addr - 1, CTag.Valid)) continue; // skip over operands
                if (CTagIs(addr - 1, CTag.Call)) continue; // skip saved PC
                Int32 op = (word >> 12) & 15;
                if (op == 0) // augmented 00 instructions
                {
                    op = word & 63;
                    if (op == 30) // LOB
                    {
                        DTAG[addr++] |= DTag.Extended;
                    }
                    else if (op == 36) // STX
                    {
                        Boolean ind = ((word & 0x400) != 0);
                        Boolean map = ((word & 0x200) != 0);
                        Int32 target = (ind) ? AugOpAddr(addr, map) : addr;
                        if (ind) DTAG[target] |= DTag.Write;
                        DTAG[addr++] |= (ind) ? DTag.Address : DTag.Immediate;
                    }
                    else if (op == 37) // LIX
                    {
                        Boolean ind = ((word & 0x400) != 0);
                        Boolean map = ((word & 0x200) != 0);
                        Int32 target = (ind) ? AugOpAddr(addr, map) : addr;
                        if (ind) DTAG[target] |= DTag.Read;
                        DTAG[addr++] |= (ind) ? DTag.Address : DTag.Immediate;
                    }
                }
                else if (op == 11) // augmented 13 instructions
                {
                    op = (word >> 6) & 7;
                    if (op <= 2) // CEU, TEU
                    {
                        Boolean ind = ((word & 0x400) != 0);
                        Boolean map = ((word & 0x200) != 0);
                        Int32 target = (ind) ? AugOpAddr(addr, map) : addr;
                        if (ind) DTAG[target] |= DTag.Read;
                        DTAG[addr++] |= (ind) ? DTag.Address : DTag.Immediate;
                    }
                    else if (op == 6) // PIE, PID
                    {
                        DTAG[addr++] |= DTag.Immediate;
                    }
                }
                else if (op == 15) // augmented 17 instructions
                {
                    op = (word >> 6) & 7;
                    if ((op == 4) || (op == 5)) // MOP
                    {
                        Boolean ind = ((word & 0x400) != 0);
                        Boolean map = ((word & 0x200) != 0);
                        Int32 target = (ind) ? AugOpAddr(addr, map) : addr;
                        if (ind) DTAG[target] |= DTag.Read;
                        DTAG[addr++] |= (ind) ? DTag.Address : DTag.Immediate;
                    }
                    else if ((op == 6) || (op == 7)) // MIP
                    {
                        Boolean ind = ((word & 0x400) != 0);
                        Boolean map = ((word & 0x200) != 0);
                        Int32 target = (ind) ? AugOpAddr(addr, map) : addr;
                        if (ind) DTAG[target] |= DTag.Write;
                        DTAG[addr++] |= (ind) ? DTag.Address : DTag.Immediate;
                    }
                }
                else // memory reference instructions
                {
                    Boolean idx = ((word & 0x800) != 0);
                    Boolean ind = ((word & 0x400) != 0);
                    Boolean map = ((word & 0x200) != 0);
                    Int32 ptr = word & 0x1ff;
                    if (map) ptr |= (addr - 1) & 0x7e00;
                    Int32 target = MemOpAddr(addr - 1, word);
                    switch (op)
                    {
                        case 1: // LAA
                        case 2: // LBA
                        case 5: // AMA
                        case 6: // SMA
                        case 7: // MPY
                        case 8: // DIV
                        case 13: // CMA
                        case 14: // AMB
                            DTAG[target] |= DTag.Read;
                            break;
                        case 3: // STA
                        case 4: // STB
                            DTAG[target] |= DTag.Write;
                            break;
                        case 12: // IMS
                            DTAG[target] |= DTag.Read | DTag.Write;
                            break;
                        case 9: // BRU
                            if ((ind) && (!idx) && (CTagIs(ptr, CTag.Call)))
                            {
                                CTAG[addr - 1] |= CTag.Return;
                                CTAG[ptr] |= CTag.HasReturn;
                            }
                            break;
                    }
                }
            }
        }

        // disassemble a program word
        static String Disassemble(Int32 addr, Int32 word)
        {
            Fragment frag = FindFragment(addr);
            CTag ctag = CTAG[addr];
            DTag dtag = DTAG[addr];
            if (CTagIs(ctag, CTag.EntryPoint | CTag.Valid)) return DecodeOp(addr, word);
            if ((CTagIs(ctag, CTag.Call)) && (frag != null)) return "DATA **";
            if (CTagIs(ctag, CTag.Valid) && !DTagIs(dtag, DTag.Indirect)) return DecodeOp(addr, word);
            if (DTagIs(dtag, DTag.Extended))
            {
                return String.Format("EAC  {0}", Label(word & 0x7fff, true, true));
            }
            if (DTagIs(dtag, DTag.Address) || DTagIs(dtag, DTag.Indirect))
            {
                Boolean idx = ((word & 0x8000) != 0);
                Boolean ind = ((word & 0x4000) != 0);
                word &= 0x3fff;
                if (DTagIs(dtag, DTag.Map1)) word |= 0x4000;
                return String.Format("DAC{0} {1}{2}", (ind) ? '*' : ' ', Label(word, true), (idx) ? ",1" : null);
            }
            if (DTagIs(dtag, DTag.Immediate))
            {
                return String.Format("DATA '{0}", Octal(word, 6));
            }
            return String.Format("DATA {0}", Label(word, true));
        }

        // generate a label for an address
        static String Label(Int32 addr, Boolean operand)
        {
            return Label(addr, operand, false);
        }

        static String Label(Int32 addr, Boolean operand, Boolean force)
        {
            if ((addr == 0) && (operand) && (!force)) return "0";
            String num = Octal(addr);
            if (addr >= 32768) return (operand) ? String.Concat("'", num) : null;
            CTag ctag = CTAG[addr];
            if (CTagIs(ctag, CTag.EntryPoint)) return String.Concat("E", num, (operand) ? null : ":"); // (E)ntry
            if (CTagIs(ctag, CTag.Call)) return String.Concat("S", num, (operand) ? null : ":"); // (S)ubroutine
            if (CTagIs(ctag, CTag.Branch)) return String.Concat("B", num, (operand) ? null : ":"); // (B)ranch target
            DTag dtag = DTAG[addr];
            if (DTagIs(dtag, DTag.Indirect | DTag.Write)) return String.Concat("V", num, (operand) ? null : ":"); // (V)ector
            if (DTagIs(dtag, DTag.Indirect)) return String.Concat("I", num, (operand) ? null : ":"); // (I)ndirect
            if (DTagIs(dtag, DTag.Read | DTag.Write)) return String.Concat("D", num, (operand) ? null : ":"); // (D)ata
            if (DTagIs(dtag, DTag.Write)) return String.Concat("W", num, (operand) ? null : ":"); // (W)rite only
            if (DTagIs(dtag, DTag.Read)) return String.Concat("R", num, (operand) ? null : ":"); // (R)ead only
            return (operand) ? String.Concat("'", num) : null;
        }

        // decode an instruction word
        static String DecodeOp(Int32 addr, Int32 word)
        {
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
                String arg = Label(target, true, (((op == 9) || (op == 10)) && (!idx)));
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

        // calculate the address referenced by a memory instruction
        static Int32 MemOpAddr(Int32 PC, Int32 IR)
        {
            Int32 op = (IR >> 12) & 15;
            Boolean idx = ((IR & 0x800) != 0);
            Boolean ind = ((IR & 0x400) != 0);
            Boolean map = ((IR & 0x200) != 0);
            Int32 addr = IR & 511;
            if (map) addr |= PC & 0x7e00;
            DTag tag = DTag.Indirect;
            // if instruction is BRU* (non-indexed) and destination is a Call, don't tag this as Indirect
            if ((op == 9) && (ind) && (!idx) && CTagIs(addr, CTag.Call)) tag = DTag.None;
            while (ind)
            {
                DTAG[addr] |= tag;
                tag = DTag.Indirect;
                Int32 word = CORE[addr];
                ind = ((word & 0x4000) != 0);
                addr = (word & 0x3fff) | (PC & 0x4000);
            }
            return addr;
        }

        // calculate the address referenced by an augmented instruction
        static Int32 AugOpAddr(Int32 addr, Boolean map)
        {
            Boolean ind;
            Int32 PC = (addr - 1) & 0x4000;
            DTag tag = DTag.None;
            do
            {
                if (map) DTAG[addr] |= (PC == 0) ? DTag.Map0 : DTag.Map1;
                DTAG[addr] |= tag;
                tag = DTag.Indirect;
                Int32 word = CORE[addr];
                ind = ((word & 0x4000) != 0);
                addr = word & 0x3fff;
                if (map) addr |= PC;
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
