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

namespace TapeDump
{
    class Program
    {
        static Char MODE;
        static Boolean DUMP;
        static Boolean DEBUG;
        static Boolean QUIET;
        static Int32 SKIP;
        static Stream DUMPFILE;
        static UInt16[] CORE = new UInt16[32768];
        static Int32 PC = 0;
        static Int32 LO = 32768;
        static Int32 HI = -1;


        static Int32 Main(String[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Usage: TapeDump [options] imagefile ...");
                Console.Error.WriteLine("Options:");
                Console.Error.WriteLine("  -a - interpret next imagefile as absolute loader tape");
                Console.Error.WriteLine("  -b - interpret next imagefile as BASIC program tape");
                Console.Error.WriteLine("  -o - interpret next imagefile as object (MNEMBLER) tape");
                Console.Error.WriteLine("  -r - interpret next imagefile as raw bytes");
                Console.Error.WriteLine("  -s num - skip first 'num' bytes of next imagefile");
                Console.Error.WriteLine("  -w dumpfile - append a copy of the next tape file to dumpfile");
                Console.Error.WriteLine("  -d - dump words without interpretation");
                Console.Error.WriteLine("  -D - enable extra debug output to stderr");
                Console.Error.WriteLine("  -q - disable messages to stderr");
                return 2;
            }

            Int32 ap = 0;
            while (ap < args.Length)
            {
                String arg = args[ap++];
                if (arg == "-D")
                {
                    DEBUG = true;
                }
                else if (arg == "-q")
                {
                    QUIET = true;
                }
                else if (arg == "-d")
                {
                    DUMP = true;
                }
                else if (arg == "-s")
                {
                    SKIP = Int32.Parse(args[ap++]);
                }
                else if (arg == "-w")
                {
                    DUMPFILE = new FileStream(args[ap++], FileMode.Append, FileAccess.Write);
                }
                else if (arg == "-a")
                {
                    MODE = 'a';
                }
                else if (arg == "-b")
                {
                    MODE = 'b';
                }
                else if (arg == "-o")
                {
                    MODE = 'o';
                }
                else if (arg == "-r")
                {
                    MODE = 'r';
                }
                else
                {
                    Dump(arg);
                }
            }

            if (DUMPFILE != null)
            {
                for (Int32 i = 0; i < CORE.Length; i++)
                {
                    DUMPFILE.WriteByte((Byte)((CORE[PC + i] >> 8) & 0xff));
                    DUMPFILE.WriteByte((Byte)(CORE[PC + i] & 0xff));
                }
                DUMPFILE.Close();
            }

            return 0;
        }

        static public void Dump(String fileName)
        {
            if (!QUIET) Console.Error.WriteLine("Reading {0}", fileName);
            Byte[] tape = File.ReadAllBytes(fileName);

            if (MODE == 'a')
            {
                if (!QUIET) Console.Error.WriteLine("Interpreting file as an absolute loader tape");
                MODE = '\0';
                DumpAbsolute(tape, SKIP);
            }
            else if (MODE == 'b')
            {
                if (!QUIET) Console.Error.WriteLine("Interpreting tape as a BASIC program");
                MODE = '\0';
                DumpBASIC(tape, SKIP);
            }
            else if (MODE == 'o')
            {
                if (!QUIET) Console.Error.WriteLine("Interpreting file as an object (MNEMBLER) tape");
                MODE = '\0';
                DumpObject(tape, SKIP);
            }
            else if (MODE == 'r')
            {
                if (!QUIET) Console.Error.WriteLine("Interpreting file as raw bytes");
                MODE = '\0';
                DumpRaw(tape, SKIP);
            }
            else
            {
                DumpAuto(tape, SKIP);
            }

            SKIP = 0;
        }

        static public void DumpAuto(Byte[] tape, Int32 startAt)
        {
            Int32 p = startAt;
            while ((p < tape.Length) && (tape[p] == 0)) p++;
            if ((p < tape.Length) && tape[p] == 0xff)
            {
                p++;
                if ((p < tape.Length) && ((tape[p] & 0x80) != 0))
                {
                    if (!QUIET) Console.Error.WriteLine("Interpreting file as a BASIC program tape");
                    DumpBASIC(tape, startAt);
                }
                else if (((p + 2) < tape.Length) && ((tape[p + 2] & 0x80) != 0))
                {
                    if (!QUIET) Console.Error.WriteLine("Interpreting file as an absolute loader tape");
                    DumpAbsolute(tape, startAt);
                }
                else
                {
                    if (!QUIET) Console.Error.WriteLine("Interpreting file as raw bytes");
                    DumpRaw(tape, startAt);
                }
            }
            else if (((p + 1) < tape.Length) && (tape[p] == 0x8d) && (tape[p + 1] == 0x8a))
            {
                if (!QUIET) Console.Error.WriteLine("Interpreting file as an object (MNEMBLER) tape");
                DumpObject(tape, startAt);
            }
            else
            {
                if (!QUIET) Console.Error.WriteLine("Interpreting file as raw bytes");
                DumpRaw(tape, startAt);
            }
        }

        static public void DumpRaw(Byte[] tape, Int32 startAt)
        {
            Int32 p = startAt;
            while (p < tape.Length)
            {
                Console.Out.Write("{0:x4} ", p - startAt);
                for (Int32 i = 0; i < 16; i++)
                {
                    if (p + i >= tape.Length)
                    {
                        Console.Out.Write("   ");
                        continue;
                    }
                    Char c = ' ';
                    if (i == 8) c = ':';
                    if ((i & 7) == 4) c = '.';
                    Console.Out.Write(c);
                    Console.Out.Write("{0:x2}", tape[p + i]);
                }
                Console.Out.Write("  ");
                for (Int32 i = 0; i < 16; i++)
                {
                    if (p + i >= tape.Length) break;
                    Byte b = tape[p + i];
                    Console.Out.Write(((b >= 160) && (b < 255)) ? (Char)(b & 0x7f) : '.');
                }
                Console.Out.WriteLine();
                if (((p += 16) & 511) == 0) Console.Out.WriteLine();
            }
        }

        static public void DumpAbsolute(Byte[] tape, Int32 startAt)
        {
            Int32 p = startAt, q = startAt;
            while (q < tape.Length)
            {
                // leader
                Int32 c = 0;
                while ((p < tape.Length) && (tape[p] != 0xff))
                {
                    if (tape[p++] != 0xba) c = 0; else c++;
                    if (c == 3) break;
                }
                if (p == tape.Length)
                {
                    if (!QUIET) Console.Error.WriteLine("Skipped {0:D0} trailer bytes", p - q);
                    break;
                }
                if (c == 3)
                {
                    if (!QUIET) Console.Error.WriteLine("End marker found after {0:D0} bytes, ignoring {1:D0} following bytes", p - q - 3, tape.Length - p);
                    break;
                }
                if ((p != q) && (!QUIET)) Console.Error.WriteLine("Skipped {0:D0} leader bytes", p - q);

                // header
                if (!QUIET) Console.Error.WriteLine("File header found at tape offset {0:D0}", p);
                p++; // skip FF byte
                Int32 addr = tape[p++] << 8;
                addr |= tape[p++];
                Int32 len = -1;
                for (Int32 i = 0; i < 2; i++)
                {
                    len <<= 8;
                    len |= tape[p++];
                }
                len = -len;

                // image text
                if (!QUIET) Console.Error.WriteLine("Loading {0:D0} words at address {1} (0x{2:x4})", len, OctalString(addr, 6, '0'), addr);
                Int32 b = 0;
                for (Int32 i = 0; i < len; i += 64)
                {
                    // block
                    b++;
                    Int32 sum = 0;
                    for (Int32 j = 0; j < 64; j++)
                    {
                        if ((i + j) == len) break;
                        CORE[addr + i + j] = (UInt16)(tape[p++] << 8);
                        CORE[addr + i + j] |= tape[p++];
                        sum += CORE[addr + i + j];
                    }
                    sum &= 0xffff;

                    // checksum
                    UInt16 checksum = (UInt16)(tape[p++] << 8);
                    checksum |= tape[p++];
                    if (!QUIET)
                    {
                        Console.Error.Write("Block {0:D0} Checksum: ", b);
                        if (sum == checksum) Console.Error.WriteLine("{0:x4} OK", sum);
                        else Console.Error.WriteLine("{0:x4} ERROR (expected {1:x4})", sum, checksum);
                    }
                }

                if (DUMPFILE != null)
                {
                    for (Int32 i = 0; i < len; i++)
                    {
                        DUMPFILE.WriteByte((Byte)((CORE[addr + i] >> 8) & 0xff));
                        DUMPFILE.WriteByte((Byte)(CORE[addr + i] & 0xff));
                    }
                    DUMPFILE.Close();
                    DUMPFILE = null;
                }

                if (DUMP)
                {
                    HexDump(CORE, addr, len);
                }
                else
                {
                    for (Int32 pc = addr; pc < addr + len; )
                    {
                        Int32 word = CORE[pc];
                        Console.Out.WriteLine("{0:x4} {1:x4} {2} {3}", pc, word, Op(ref pc), Data(word));
                    }
                }
                Console.Out.WriteLine();

                q = p;
            }
        }

        static public void DumpObject(Byte[] tape, Int32 startAt)
        {
            Int32 p = startAt, q = startAt;
            while (q < tape.Length)
            {
                // leader
                Int32 c = 0;
                while ((p < tape.Length) && (tape[p] != 0x8d))
                {
                    if (tape[p++] != 0xba) c = 0; else c++;
                    if (c == 3) break;
                }
                if (p == tape.Length)
                {
                    if (!QUIET) Console.Error.WriteLine("Skipped {0:D0} trailer bytes", p - q);
                    break;
                }
                if (c == 3)
                {
                    if (!QUIET) Console.Error.WriteLine("End marker found after {0:D0} bytes, ignoring {1:D0} following bytes", p - q - 3, tape.Length - p);
                    break;
                }
                if ((p != q) && (!QUIET)) Console.Error.WriteLine("Skipped {0:D0} leader bytes", p - q);

                // header
                if (((p + 1) >= tape.Length) || (tape[p] != 0x8d) || (tape[p + 1] != 0x8a))
                {
                    if (!QUIET) Console.Error.WriteLine("Expected CR LF header prefix not found");
                    p++;
                    break;
                }
                if (!QUIET) Console.Error.WriteLine("File header found at tape offset {0:D0}", p);
                Int32 b = 0;
                while (((p + 2) < tape.Length) && (tape[p] == 0x8d) && (tape[p + 1] == 0x8a) && ((tape[p + 2] == 0) || (tape[p + 2] == 0xff)))
                {
                    if (tape[p + 2] == 0xff)
                    {
                        p += 3; // block header
                    }
                    else if (((p + 3) < tape.Length) && (tape[p + 2] == 0) && (tape[p + 3] == 0xff))
                    {
                        p += 4; // block header
                    }
                    else if (tape[p + 2] == 0)
                    {
                        p += 3; // this is actually a trailer
                        continue;
                    }
                    else
                    {
                        if (!QUIET) Console.Error.WriteLine("Unrecognized block header (not 8D 8A FF and not 8D 8A 00 FF)");
                        p += 2;
                        break;
                    }
                    b++;
                    Int32 len = 54; // number of words
                    Int32[] block = new Int32[36];
                    Int32 j = 0;
                    c = 0;
                    Int32 sum = 0;
                    for (Int32 i = 0; i < len; i++)
                    {
                        Byte n = tape[p++];
                        Int32 val = n << 8;
                        block[j] <<= 8;
                        block[j] |= n;
                        if (++c == 3)
                        {
                            j++;
                            c = 0;
                        }
                        n = tape[p++];
                        val |= n;
                        sum += val;
                        block[j] <<= 8;
                        block[j] |= n;
                        if (++c == 3)
                        {
                            j++;
                            c = 0;
                        }
                    }

                    // checksum
                    UInt16 checksum = (UInt16)(tape[p++] << 8);
                    checksum |= tape[p++];
                    sum += checksum;
                    sum &= 0xffff;
                    if (!QUIET)
                    {
                        Console.Error.Write("Block {0:D0} Checksum: ", b);
                        if (sum == 0) Console.Error.WriteLine("{0:x4} OK", checksum);
                        else Console.Error.WriteLine("{0:x4} ERROR (expected {1:x4})", (sum + checksum) & 0xffff, checksum);
                    }

                    if (DUMP)
                    {
                        for (Int32 i = 0; i < block.Length; i++)
                        {
                            Int32 c1 = (block[i] >> 16) & 127; if (c1 < 32) c1 += 64; if (c1 == 127) c1 = 32;
                            Int32 c2 = (block[i] >> 8) & 127; if (c2 < 32) c2 += 64; if (c2 == 127) c2 = 32;
                            Int32 c3 = block[i] & 127; if (c3 < 32) c3 += 64; if (c3 == 127) c3 = 32;
                            Console.Out.WriteLine("{0:D3}-{1:D2}  {2}  {3}{4}{5}", b, i, OctalString(block[i], 8), (Char)(c1), (Char)(c2), (Char)(c3));
                        }
                    }
                    else
                    {
                        DumpLoaderBlock(b, block);
                    }
                    Console.Out.WriteLine();
                }

                q = p;
            }
        }

        static public Double SEL810Float(UInt16 w1, UInt16 w2)
        {
            // extract parts of SEL810 float
            // sfffffffffffffff 0ffffffeeeeeeeee
            Int32 sign = (w1 >> 15) & 1;
            UInt32 frac = w1; // 15 bits from w1 (ignore sign bit)
            frac <<= 6; // make room for 6 bits from w2
            frac |= (UInt32)((w2 >> 9) & 0x3f); // +6 = 21 bits
            frac <<= 11; // +11 = 32 bits (also shifts out sign bit)
            Int32 exp = (w2 & 0x00ff) - (((w2 & 0x0100) == 0) ? 0 : 256); // 2's complement

            // check for zero
            if (frac == 0) return 0.0;

            // normalize for conversion to IEEE format
            while ((frac & 0x80000000) == 0)
            {
                frac <<= 1;
                exp--;
            }

            // convert to IEEE format
            frac <<= 1;
            exp--;
            Int64 qword = sign; // sign bit
            qword <<= 11; // make room for exponent
            qword |= (UInt32)((exp + 1023) & 2047);
            qword <<= 32; // make room for significand
            qword |= frac;
            qword <<= 20; // shift to final position
            return BitConverter.Int64BitsToDouble(qword);
        }

        static public String OctalString(Int32 value)
        {
            return OctalString(value, 0, '0');
        }

        static public String OctalString(Int32 value, Int32 minWidth)
        {
            return OctalString(value, minWidth, (minWidth < 0) ? ' ' : '0');
        }

        static public String OctalString(Int32 value, Int32 minWidth, Char padChar)
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

        static public Int32 Bound(Int32 addr)
        {
            return Bound(addr, ref LO, ref HI);
        }

        static public Int32 Bound(Int32 addr, ref Int32 low, ref Int32 high)
        {
            if (addr < low) low = addr;
            if (addr > high) high = addr;
            return addr;
        }

        static public void DumpLoaderBlock(Int32 blockNum, Int32[] block)
        {
            for (Int32 i = 0; i < block.Length; i++)
            {
                Int32 word = block[i];
                Console.Out.Write("{0:x6}  ", word);
                Int32 code = (word >> 17) & 15;
                UInt16 zzzzz = (UInt16)(word & 0x7fff);
                switch ((word >> 22) & 3)
                {
                    case 0: // xxyzzzzz - direct value or memory reference (xx = 00-17)
                        if ((word & 0xff0000) == 0)
                        {
                            // 00booooo - direct value: booooo (b = 0/1), no fixup needed
                            CORE[PC] = zzzzz;
                            Console.Out.WriteLine("{0:D3}-{1:D2}  {2}  {3} {4} {5}", blockNum, i, OctalString(word, 8), OctalString(PC, 5), OctalString(zzzzz, 5), Op(ref PC));
                            break;
                        }
                        // xxyzzzzz - memory referencing instructions (xx = 01-17)
                        if (zzzzz < 512)
                        CORE[PC] = zzzzz;
                        Console.Out.WriteLine("{0:D3}-{1:D2}  {2}  {3} {4} {5}", blockNum, i, OctalString(word, 8), OctalString(PC, 5), OctalString(zzzzz, 5), Op(ref PC));
                        break;

                    case 1: // tooooooo - direct/extended address constant (t = 2/3)
                        Int32 R = (word >> 21) & 1;
                        Int32 X = (word >> 16) & 1;
                        Int32 I = (word >> 15) & 1;
                        switch (code)
                        {
                            case 11:
                                CORE[PC] = (UInt16)((zzzzz & 0x3fff) | (I << 14) | (X << 15));
                                Console.Out.WriteLine("{0:D3}-{1:D2}  {2}  {3} {4} DAC{5} '{6}{7}", blockNum, i, OctalString(word, 8), OctalString(PC, 5), OctalString(CORE[PC++], 5), (I==1) ? '*' : ' ', OctalString(zzzzz, 5), (X==1) ? ",1" : null);
                                break;
                            case 15:
                                CORE[PC] = zzzzz;
                                Console.Out.WriteLine("{0:D3}-{1:D2}  {2}  {3} {4} EAC{5} '{6}{7}", blockNum, i, OctalString(word, 8), OctalString(PC, 5), OctalString(CORE[PC++], 5), (I == 1) ? '*' : ' ', OctalString(zzzzz, 5), (X == 1) ? ",1" : null);
                                break;
                            default:
                                String tmp1 = String.Format("R={0:D0} X={1:D0} I={2:D0} Op={3:D0} Addr={4:D0}", R, X, I, code, zzzzz);
                                Console.Out.WriteLine("{0:D3}-{1:D2}  {2}  {3} {4} {5}~", blockNum, i, OctalString(word, 8), OctalString(PC, 5), OctalString(zzzzz, 5), tmp1);
                                break;
                        }
                        break;

                    case 2: // fooaaaaa - subroutine or common (f = 4/5)
                        Int32 w2 = block[i + 1];
                        Int32 w3 = block[i + 2];
                        Int32 w4 = block[i + 3];
                        Char s1 = (Char)((w3 >> 16) & 255); if (s1 < ' ') s1 += '@';
                        Char s2 = (Char)((w3 >> 8) & 255); if (s2 < ' ') s2 += '@';
                        Char s3 = (Char)(w3 & 255); if (s3 < ' ') s3 += '@';
                        Char s4 = (Char)((w4 >> 16) & 255); if (s4 < ' ') s4 += '@';
                        Char s5 = (Char)((w4 >> 8) & 255); if (s5 < ' ') s5 += '@';
                        Char s6 = (Char)(w4 & 255); if (s6 < ' ') s6 += '@';
                        String name = String.Format("{0}{1}{2}{3}{4}{5}", s1, s2, s3, s4, s5, s6);
                        switch ((w2 >> 22) & 3)
                        {
                            case 0: // NAME
                                Console.Out.WriteLine("{0:D3}-{1:D2}  {2}              NAME {3},{4}", blockNum, i, OctalString(word, 8), name, OctalString(zzzzz, 5));
                                Console.Out.WriteLine("{0:D3}-{1:D2}  {2}", blockNum, ++i, OctalString(block[i], 8));
                                Console.Out.WriteLine("{0:D3}-{1:D2}  {2}", blockNum, ++i, OctalString(block[i], 8));
                                Console.Out.WriteLine("{0:D3}-{1:D2}  {2}", blockNum, ++i, OctalString(block[i], 8));
                                break;
                            case 1: // CALL
                                Console.Out.WriteLine("{0:D3}-{1:D2}  {2}  {3}       CALL {4}", blockNum, i, OctalString(word, 8), OctalString(PC++, 5), name);
                                Console.Out.WriteLine("{0:D3}-{1:D2}  {2}", blockNum, ++i, OctalString(block[i], 8));
                                Console.Out.WriteLine("{0:D3}-{1:D2}  {2}", blockNum, ++i, OctalString(block[i], 8));
                                Console.Out.WriteLine("{0:D3}-{1:D2}  {2}", blockNum, ++i, OctalString(block[i], 8));
                                break;
                            case 2: // CDE
                                Console.Out.WriteLine("{0:D3}-{1:D2}  {2}  {3}       CDE  {4}", blockNum, i, OctalString(word, 8), OctalString(PC++, 5), name);
                                Console.Out.WriteLine("{0:D3}-{1:D2}  {2}", blockNum, ++i, OctalString(block[i], 8));
                                Console.Out.WriteLine("{0:D3}-{1:D2}  {2}", blockNum, ++i, OctalString(block[i], 8));
                                Console.Out.WriteLine("{0:D3}-{1:D2}  {2}", blockNum, ++i, OctalString(block[i], 8));
                                break;
                            case 3: // CRE
                                Console.Out.WriteLine("{0:D3}-{1:D2}  {2}  {3}       CRE  {4}", blockNum, i, OctalString(word, 8), OctalString(PC++, 5), name);
                                Console.Out.WriteLine("{0:D3}-{1:D2}  {2}", blockNum, ++i, OctalString(block[i], 8));
                                Console.Out.WriteLine("{0:D3}-{1:D2}  {2}", blockNum, ++i, OctalString(block[i], 8));
                                Console.Out.WriteLine("{0:D3}-{1:D2}  {2}", blockNum, ++i, OctalString(block[i], 8));
                                break;
                        }
                        break;

                    case 3: // sooaaaaa - loader directive or literal reference (s = 6/7)
                        if ((word & 0x010000) != 0)
                        {
                            // literal referencing instructions
                            Console.Out.WriteLine("{0:D3}-{1:D2}  {2}  ~", blockNum, i, OctalString(word, 8));
                            break;
                        }
                        // loader directive
                        switch (code)
                        {
                            case 0: // ORG
                                Console.Out.WriteLine("{0:D3}-{1:D2}  {2}              ORG  '{3}", blockNum, i, OctalString(word, 8), OctalString(PC = zzzzz, 5));
                                break;
                            case 1: // END
                                Console.Out.WriteLine("{0:D3}-{1:D2}  {2}              END  '{3}", blockNum, i, OctalString(word, 8), OctalString(zzzzz, 5));
                                i = block.Length;
                                break;
                            case 8:
                                Console.Out.WriteLine("{0:D3}-{1:D2}  {2}  $", blockNum, i, OctalString(word, 8));
                                i = block.Length;
                                break;
                            default:
                                Console.Out.WriteLine("{0:D3}-{1:D2}  {2}  ~", blockNum, i, OctalString(word, 8));
                                break;
                        }
                        break;
                }
            }
        }

        static public String Op(ref Int32 pc)
        {
            Int32 word = CORE[pc];
            Int32 op = (word >> 12) & 15;
            String X = ((word & 0x0800) != 0) ? ",1" : null;
            Char I = ((word & 0x0400) != 0) ? '*' : ' ';
            Boolean M = (word & 0x0200) != 0;
            Int32 ad = word & 0x01ff;
            Int32 ea = ((M) ? (pc & 0xfe00) : 0) | ad;
            pc++;
            switch (op)
            {
                case 0: // augmented 00 instructions
                    Int32 sc = (word >> 6) & 15;
                    switch (word & 0x003f)
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
                        case 30: return String.Format("LOB  '{0}", OctalString(CORE[pc++] & 0x7fff, 5));
                        case 31: return "OVS";
                        case 32: return "TBP";
                        case 33: return "TPB";
                        case 34: return "TBV";
                        case 35: return "TVB";
                        case 36: return String.Format("STX{0} '{1}", I, OctalString(CORE[pc++]));
                        case 37: return String.Format("LIX{0} '{1}", I, OctalString(CORE[pc++]));
                        case 38: return "XPX";
                        case 39: return "XPB";
                        case 40: return "SXB";
                        case 41: return String.Format("IXS  {0:D2}", sc);
                        case 42: return "TAX";
                        case 43: return "TXA";
                        default: return String.Format("~Augmented 00 {0}!", OctalString(word, 5, '0'));
                    }
                case 1: return String.Format("LAA{0} '{1}{2}", I, OctalString(ea, 5, '0'), X);
                case 2: return String.Format("LBA{0} '{1}{2}", I, OctalString(ea, 5, '0'), X);
                case 3: return String.Format("STA{0} '{1}{2}", I, OctalString(ea, 5, '0'), X);
                case 4: return String.Format("STB{0} '{1}{2}", I, OctalString(ea, 5, '0'), X);
                case 5: return String.Format("AMA{0} '{1}{2}", I, OctalString(ea, 5, '0'), X);
                case 6: return String.Format("SMA{0} '{1}{2}", I, OctalString(ea, 5, '0'), X);
                case 7: return String.Format("MPY{0} '{1}{2}", I, OctalString(ea, 5, '0'), X);
                case 8: return String.Format("DIV{0} '{1}{2}", I, OctalString(ea, 5, '0'), X);
                case 9: return String.Format("BRU{0} '{1}{2}", I, OctalString(ea, 5, '0'), X);
                case 10: return String.Format("SPB{0} '{1}{2}", I, OctalString(ea, 5, '0'), X);
                case 11: // augmented 13 instruction
                    switch ((word >> 6) & 7)
                    {
                        case 0: return String.Format("CEU{0} '{1},0", I, OctalString(CORE[pc++]));
                        case 1: return String.Format("CEU{0} '{1},1", I, OctalString(CORE[pc++])); // TODO: MAP mode
                        case 2: return String.Format("TEU{0} '{0}", I, OctalString(CORE[pc++]));
                        case 4: return String.Format("SNS  {0:D2}", word & 0x000f);
                        case 6: switch (word & 0x003f)
                            {
                                case 0: return String.Format("PIE  '{0}", OctalString(CORE[pc++]));
                                case 1: return String.Format("PID  '{0}", OctalString(CORE[pc++]));
                                default: return String.Format("~Augmented 13 {0}!", OctalString(word, 5, '0'));
                            }
                        default: return String.Format("~Augmented 13 {0}!", OctalString(word, 5, '0'));
                    }
                case 12: return String.Format("IMS{0} '{1}{2}", I, OctalString(ea, 5, '0'), X);
                case 13: return String.Format("CMA{0} '{1}{2}", I, OctalString(ea, 5, '0'), X);
                case 14: return String.Format("AMB{0} '{1}{2}", I, OctalString(ea, 5, '0'), X);
                default: // augmented 17 instruction
                    switch ((word >> 6) & 7)
                    {
                        case 0: return String.Format("AOP  '{0},0", OctalString(word & 0x003f));
                        case 1: return String.Format("AOP  '{0},1", OctalString(word & 0x003f));
                        case 2: return String.Format("AIP  '{0},0,{1}", OctalString(word & 0x003f), (X == null) ? '0' : '1');
                        case 3: return String.Format("AIP  '{0},1,{1}", OctalString(word & 0x003f), (X == null) ? '0' : '1');
                        case 4: return String.Format("MOP{0} '{1},0", I, OctalString(word & 0x003f));
                        case 5: return String.Format("MOP{0} '{1},1", I, OctalString(word & 0x003f)); // TODO: MAP mode
                        case 6: return String.Format("MIP{0} '{1},0", I, OctalString(word & 0x003f));
                        case 7: return String.Format("MIP{0} '{1},1", I, OctalString(word & 0x003f)); // TODO: MAP mode
                        default: return String.Format("~Augmented 17 {0}!", OctalString(word, 5, '0'));
                    }
            }
        }

        static public String Data(Int32 word)
        {
            String o = OctalString(word, 5, '0');
            Int32 b = (word >> 8) & 255;
            Char c1 = (Char)(((b >= 160) && (b < 255)) ? (b & 127) : 95);
            b = word & 255;
            Char c2 = (Char)(((b >= 160) && (b < 255)) ? (b & 127) : 95);
            return String.Format("'{0}  {1:D5}  {2}{3}", o, word, c1, c2);
        }

        static public void HexDump(UInt16[] data, Int32 offset, Int32 count)
        {
            Int32 p = -(offset % 8);
            while (p < count)
            {
                Console.Out.Write("{0:x4} ", offset + p);
                for (Int32 i = 0; i < 8; i++)
                {
                    String c = " ";
                    if (i == 4) c = ".";
                    Console.Out.Write(c);
                    if ((p + i < 0) || (p + i >= count)) Console.Out.Write("    ");
                    else Console.Out.Write("{0:x4}", data[offset + p + i]);
                }
                Console.Out.Write("  ");
                for (Int32 i = 0; i < 8; i++)
                {
                    if (p + i < 0)
                    {
                        Console.Out.Write("  ");
                        continue;
                    }
                    if (p + i >= count) break;
                    Char c = (Char)((data[offset + p + i] >> 8) & 127);
                    if (c < 32) c = '.';
                    if (c == 127) c = '.';
                    Console.Out.Write(c);
                    c = (Char)(data[offset + p + i] & 127);
                    if (c < 32) c = '.';
                    if (c == 127) c = '.';
                    Console.Out.Write(c);
                }
                Console.Out.WriteLine();
                p += 8;
            }
        }

        static public void DumpBASIC(Byte[] tape, Int32 startAt)
        {
            Int32 p = startAt, q = startAt;
            while (q < tape.Length)
            {
                // leader
                Int32 c = 0;
                while ((p < tape.Length) && (tape[p] != 0xff))
                {
                    if (tape[p++] != 0xba) c = 0; else c++;
                    if (c == 3) break;
                }
                if (p == tape.Length)
                {
                    if (!QUIET) Console.Error.WriteLine("Skipped {0:D0} trailer bytes", p - q);
                    break;
                }
                if (c == 3)
                {
                    if (!QUIET) Console.Error.WriteLine("End marker found after {0:D0} bytes, ignoring {1:D0} following bytes", p - q - 3, tape.Length - p);
                    break;
                }
                if ((p != q) && (!QUIET)) Console.Error.WriteLine("Skipped {0:D0} leader bytes", p - q);

                // header
                if (!QUIET) Console.Error.WriteLine("File header found at tape offset {0:D0}", p);
                p++; // skip FF byte
                Int32 len = -1;
                for (Int32 i = 0; i < 2; i++)
                {
                    len <<= 8;
                    len |= tape[p++];
                }
                len = -len;

                // program text
                if (!QUIET) Console.Error.WriteLine("Reading {0:D0} words of program text...", len);
                UInt16[] text = new UInt16[len];
                Int32 sum = 0;
                for (Int32 i = 0; i < len; i++)
                {
                    text[i] = (UInt16)(tape[p++] << 8);
                    text[i] |= tape[p++];
                    sum += text[i];
                }

                // checksum
                UInt16 checksum = (UInt16)(tape[p++] << 8);
                checksum |= tape[p++];
                sum += checksum;
                sum &= 0xffff;
                if (!QUIET)
                {
                    Console.Error.Write("Checksum: ");
                    if (sum == 0) Console.Error.WriteLine("{0:x4} OK", checksum);
                    else Console.Error.WriteLine("{0:x4} ERROR (expected {1:x4})", (sum + checksum) & 0xffff, checksum);
                }

                if (DUMPFILE != null)
                {
                    for (Int32 i = 0; i < text.Length; i++)
                    {
                        DUMPFILE.WriteByte((Byte)((text[i] >> 8) & 0xff));
                        DUMPFILE.WriteByte((Byte)(text[i] & 0xff));
                    }
                    DUMPFILE.Close();
                    DUMPFILE = null;
                }

                if (DUMP) HexDump(text, 0, len);
                else ListBASIC(text);
                Console.Out.WriteLine();

                q = p;
            }
        }

        static public void ListBASIC(UInt16[] text)
        {
            Int32 p = 0;
            Int32 err = 0;
            while (p < text.Length)
            {
                Int32 q = p;
                UInt16 line = text[q++];
                if ((DEBUG) && (!QUIET))
                {
                    Console.Error.Write("{0:X4}:", p);
                    for (Int32 i = p; i < p + text[q]; i++) Console.Error.Write(" {0:X4}", text[i]);
                    Console.Error.WriteLine();
                }
                p += text[q++];
                Console.Out.Write("{0:D0}  ", line);
                while (q < p)
                {
                    UInt16 word = text[q++];
                    Boolean f = (word & 0x8000) == 0x8000;
                    Int32 op = word & 0x7e00;
                    Int32 val = word & 0x01ff;
                    switch (op)
                    {
                        case 0x0000:
                            break; // end of expression (not needed before ] or STEP)
                        case 0x0200:
                            Console.Out.Write("\""); // literal string
                            while ((val = word & 0x00ff) >= 0x80)
                            {
                                Console.Out.Write((Char)(val & 0x7f));
                                word = text[q++];
                                val = (word >> 8) & 0x00ff;
                                if (val < 0x80) break;
                                Console.Out.Write((Char)(val & 0x7f));
                            }
                            if ((word & 0x8000) == 0x8000) word = text[q++];
                            f = (word & 0x8000) == 0x8000;
                            op = word & 0x7e00;
                            val = word & 0x01ff;
                            if (op == 0x0200) Console.Out.Write("\"");
                            break;
                        case 0x0400:
                            Console.Out.Write(","); // PRINT modifier
                            break;
                        case 0x0600:
                            Console.Out.Write(";");
                            break;
                        case 0x0800:
                            Console.Out.Write(")"); // precedence specifier or function arg
                            break;
                        case 0x0a00:
                            Console.Out.Write("]"); // array index
                            break;
                        case 0x0c00:
                            Console.Out.Write(","); // array index separator
                            break;
                        case 0x0e00:
                            Console.Out.Write("="); // assignment, aka :=
                            break;
                        case 0x1000:
                            Console.Out.Write("+");
                            break;
                        case 0x1200:
                            Console.Out.Write("-");
                            break;
                        case 0x1400:
                            Console.Out.Write("*");
                            break;
                        case 0x1600:
                            Console.Out.Write("/");
                            break;
                        case 0x1800:
                            Console.Out.Write("^");
                            break;
                        case 0x1a00:
                            Console.Out.Write(">");
                            break;
                        case 0x1c00:
                            Console.Out.Write("<");
                            break;
                        case 0x1e00:
                            Console.Out.Write("#"); // comparison, aka <>
                            break;
                        case 0x2000:
                            Console.Out.Write("="); // comparison, aka ==
                            break;
                        case 0x2400:
                            Console.Out.Write("["); // array index
                            break;
                        case 0x2600:
                            Console.Out.Write("("); // precedence specifier or function arg
                            break;
                        case 0x3000:
                            Console.Out.Write(" >= ");
                            break;
                        case 0x3200:
                            Console.Out.Write(" <= ");
                            break;
                        case 0x3400:
                            Console.Out.Write("LET ");
                            break;
                        case 0x3600:
                            Console.Out.Write("DIM ");
                            break;
                        // case 0x3800: // COM?
                        // case 0x3a00: // DEF?
                        case 0x3c00:
                            Console.Out.Write("REM");
                            while ((val = word & 0x00ff) >= 0x80)
                            {
                                Console.Out.Write((Char)(val & 0x7f));
                                if (q == p)
                                {
                                    val = 0;
                                    break;
                                }
                                word = text[q++];
                                if ((val = (word & 0xff00) >> 8) < 0x80) break;
                                Console.Out.Write((Char)(val & 0x7f));
                            }
                            break;
                        case 0x3e00:
                            Console.Out.Write("GOTO ");
                            break;
                        case 0x4000:
                            Console.Out.Write("IF ");
                            break;
                        case 0x4200:
                            Console.Out.Write("FOR ");
                            break;
                        case 0x4400:
                            Console.Out.Write("NEXT ");
                            break;
                        case 0x4600:
                            Console.Out.Write("GOSUB ");
                            break;
                        case 0x4800:
                            Console.Out.Write("RETURN ");
                            break;
                        case 0x4a00:
                            Console.Out.Write("END ");
                            break;
                        //case 0x4c00: // STOP?
                        //case 0x4e00: // WAIT?
                        //case 0x5000: // CALL?
                        //case 0x5200: // DATA?
                        //case 0x5400: // READ?
                        case 0x5600:
                            Console.Out.Write("PRINT ");
                            break;
                        case 0x5800:
                            Console.Out.Write("INPUT ");
                            break;
                        //case 0x5a00: // RESTORE?
                        //case 0x5c00: // MAT?
                        case 0x5e00:
                            Console.Out.Write(" THEN ");
                            break;
                        case 0x6000:
                            Console.Out.Write(" TO ");
                            break;
                        case 0x6200:
                            Console.Out.Write(" STEP ");
                            break;
                        //case 0x6400: // NOT?
                        //case 0x6600: // AND?
                        //case 0x6800: // OR?
                        default:
                            Console.Out.Write('{');
                            Console.Out.Write("op{0:X4}", op);
                            Console.Out.Write('}');
                            err++;
                            q = p;
                            break;
                    }
                    if (f)
                    {
                        switch (val)
                        {
                            case 0:
                                UInt16 w1 = text[q++];
                                UInt16 w2 = text[q++];
                                Console.Out.Write("{0:G6}", SEL810Float(w1, w2));
                                break;
                            case 3:
                                Console.Out.Write("{0:D0}", text[q++]);
                                break;
                            //case 0x01f: // TAB?
                            //case 0x02f: // SIN?
                            case 0x03f:
                                Console.Out.Write("COS");
                                break;
                            //case 0x04f: // TAN?
                            //case 0x05f: // ATN?
                            //case 0x06f: // EXP?
                            case 0x07f:
                                Console.Out.Write("LOG");
                                break;
                            case 0x08f:
                                Console.Out.Write("ABS");
                                break;
                            //case 0x09f: // SQR?
                            case 0x0af:
                                Console.Out.Write("INT");
                                break;
                            case 0x0bf:
                                Console.Out.Write("RND");
                                break;
                            //case 0x0cf: // SGN?
                            //case 0x0df: // ZER?
                            //case 0x0ef: // CON?
                            //case 0x0ff: // IDN?
                            //case 0x10f: // INV?
                            //case 0x11f: // TRN?
                            default:
                                Console.Out.Write('{');
                                Console.Out.Write("lit{0:X3}", val);
                                Console.Out.Write('}');
                                err++;
                                break;
                        }
                    }
                    else if (val == 0)
                    {
                        // do nothing
                    }
                    else if (((val & 0x1f0) >= 0x010) && ((val & 0x00f) >= 4) && ((val & 0x00f) < 15))
                    {
                        // regular variables: single letter, or letter followed by digit
                        Console.Out.Write((Char)(64 + ((val & 0x1f0) >> 4)));
                        if ((val & 0x00f) > 4) Console.Out.Write((Char)(43 + (val & 0x00f)));
                    }
                    else if (((val & 0x1f0) >= 0x010) && ((val & 0x00f) == 1))
                    {
                        // one-dimensional arrays: single letter
                        Console.Out.Write((Char)(64 + ((val & 0x1f0) >> 4)));
                    }
                    else if (((val & 0x1f0) >= 0x010) && ((val & 0x00f) == 2))
                    {
                        // two-dimensional arrays: single letter
                        Console.Out.Write((Char)(64 + ((val & 0x1f0) >> 4)));
                    }
                    else
                    {
                        switch (val)
                        {
                            default:
                                Console.Out.Write('{');
                                Console.Out.Write("val{0:X3}", val);
                                Console.Out.Write('}');
                                err++;
                                break;
                        }
                    }
                }
                Console.Out.WriteLine();
            }
            if ((err != 0) && (!QUIET)) Console.Error.WriteLine("Parse Errors: {0:D0}", err);
        }
    }
}
