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
        static Boolean DEBUG;

        static Int32 Main(String[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Usage: TapeDump [options] imagefile ...");
                Console.Error.WriteLine("Options:");
                Console.Error.WriteLine("  -a absolute loader tape");
                Console.Error.WriteLine("  -b BASIC program tape");
                Console.Error.WriteLine("  -o object (MNEMBLER) tape");
                Console.Error.WriteLine("  -r raw tape (default)");
                Console.Error.WriteLine("  -d debug");
                return 2;
            }

            Int32 ap = 0;
            while (ap < args.Length)
            {
                String arg = args[ap++];
                if (arg == "-d")
                {
                    DEBUG = true;
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
                    MODE = '\0';
                }
                else
                {
                    Dump(arg);
                }
            }

            return 0;
        }

        static public void Dump(String fileName)
        {
            Console.Error.WriteLine("Reading {0}", fileName);
            Byte[] tape = File.ReadAllBytes(fileName);

            if (MODE == 'a')
            {
                DumpAbsolute(tape);
            }
            else if (MODE == 'b')
            {
                DumpBASIC(tape);
            }
            else if (MODE == 'o')
            {
                DumpObject(tape);
            }
            else
            {
                DumpRaw(tape);
            }
        }

        static public void DumpRaw(Byte[] tape)
        {
            Int32 p = 0;
            while (p < tape.Length)
            {
                Console.Out.Write("{0:x4} ", p);
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

        static public void DumpAbsolute(Byte[] tape)
        {
            Int32 p = 0, q = 0;
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
                    Console.Error.WriteLine("Skipped {0:D0} trailer bytes", p - q);
                    break;
                }
                if (c == 3)
                {
                    Console.Error.WriteLine("End marker found after {0:D0} bytes, ignoring {1:D0} following bytes", p - q - 3, tape.Length - p);
                    break;
                }
                if (p != q) Console.Error.WriteLine("Skipped {0:D0} leader bytes", p - q);

                // header
                p++; // skip FF byte
                Int32 addr = tape[p++] << 8;
                addr |= tape[p++];
                Console.Error.WriteLine("Load address: {0:x4}", addr);
                Int32 len = -1;
                for (Int32 i = 0; i < 2; i++)
                {
                    len <<= 8;
                    len |= tape[p++];
                }
                len = -len;

                // image text
                Console.Error.WriteLine("Reading {0:D0} words of image text...", len);
                UInt16[] text = new UInt16[len];
                Int32 b = 0;
                for (Int32 i = 0; i < len; i += 64)
                {
                    // block
                    Int32 sum = 0;
                    for (Int32 j = 0; j < 64; j++)
                    {
                        if ((i + j) == len) break;
                        text[i + j] = (UInt16)(tape[p++] << 8);
                        text[i + j] |= tape[p++];
                        sum += text[i + j];
                    }
                    sum &= 0xffff;

                    // checksum
                    UInt16 checksum = (UInt16)(tape[p++] << 8);
                    checksum |= tape[p++];
                    Console.Error.Write("Block {0:D0} Checksum: ", ++b);
                    if (sum == checksum) Console.Error.WriteLine("{0:x4} OK", sum);
                    else Console.Error.WriteLine("{0:x4} ERROR (expected {1:x4})", sum, checksum);
                }

                DumpRaw(text, addr);
                Console.Out.WriteLine();

                q = p;
            }
        }

        static public void DumpObject(Byte[] tape)
        {
            Int32 b = 0, p = 0, q = 0;
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
                    Console.Error.WriteLine("Skipped {0:D0} trailer bytes", p - q);
                    break;
                }
                if (c == 3)
                {
                    Console.Error.WriteLine("End marker found after {0:D0} bytes, ignoring {1:D0} following bytes", p - q - 3, tape.Length - p);
                    break;
                }
                if (p != q) Console.Error.WriteLine("Skipped {0:D0} leader bytes", p - q);

                // header
                if (((p + 3) >= tape.Length) || (tape[p] != 0x8d) || (tape[p + 1] != 0x8a) || (tape[p + 2] != 0))
                {
                    Console.Error.WriteLine("Expected 8D 8A 00 header prefix not found");
                    p++;
                    break;
                }
                p += 3;
                if ((p == tape.Length) || (tape[p] == 0)) continue; // this is actually a trailer
                if ((p < tape.Length) && (tape[p] != 0xff))
                {
                    Console.Error.WriteLine("Unrecognized block header (not CR LF 00 00 and not CR LF 00 0F)");
                    break;
                }
                p++; // skip FF start-of-block marker
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
                Console.Error.Write("Block {0:D0} Checksum: ", ++b);
                if (sum == 0) Console.Error.WriteLine("{0:x4} OK", checksum);
                else Console.Error.WriteLine("{0:x4} ERROR (expected {1:x4})", (sum + checksum) & 0xffff, checksum);

                for (Int32 i = 0; i < block.Length; i++)
                {
                    Console.Out.WriteLine("{0:x2}-{1:x2}  {2:x6}  {3}", b, i, block[i], LoaderDirective(block[i]));
                }
                Console.Out.WriteLine();

                q = p;
            }
        }

        static public void DumpRaw(UInt16[] text, Int32 addrOffset)
        {
            Int32 p = 0;
            while (p < text.Length)
            {
                Console.Out.Write("{0:x4} ", p + addrOffset);
                for (Int32 i = 0; i < 8; i++)
                {
                    if (p + i >= text.Length)
                    {
                        Console.Out.Write("     ");
                        continue;
                    }
                    Char c = ' ';
                    if (i == 4) c = ':';
                    if ((i & 3) == 2) c = '.';
                    Console.Out.Write(c);
                    Console.Out.Write("{0:x4}", text[p + i]);
                }
                Console.Out.Write("  ");
                for (Int32 i = 0; i < 8; i++)
                {
                    if (p + i >= text.Length) break;
                    Char c = (Char)((text[p + i] >> 8) & 127);
                    if (c < 32) c = '.';
                    if (c == 127) c = '.';
                    Console.Out.Write(c);
                    c = (Char)(text[p + i] & 127);
                    if (c < 32) c = '.';
                    if (c == 127) c = '.';
                    Console.Out.Write(c);
                }
                Console.Out.WriteLine();
                p += 8;
            }
        }

        static public void DumpBASIC(Byte[] tape)
        {
            Int32 p = 0, q = 0;
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
                    Console.Error.WriteLine("Skipped {0:D0} trailer bytes", p - q);
                    break;
                }
                if (c == 3)
                {
                    Console.Error.WriteLine("End marker found after {0:D0} bytes, ignoring {1:D0} following bytes", p - q - 3, tape.Length - p);
                    break;
                }
                if (p != q) Console.Error.WriteLine("Skipped {0:D0} leader bytes", p - q);

                // header
                p++; // skip FF byte
                Int32 len = -1;
                for (Int32 i = 0; i < 2; i++)
                {
                    len <<= 8;
                    len |= tape[p++];
                }
                len = -len;

                // program text
                Console.Error.WriteLine("Reading {0:D0} words of program text...", len);
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
                Console.Error.Write("Checksum: ");
                if (sum == 0) Console.Error.WriteLine("{0:x4} OK", checksum);
                else Console.Error.WriteLine("{0:x4} ERROR (expected {1:x4})", (sum + checksum) & 0xffff, checksum);

                DumpBASIC(text);
                Console.Out.WriteLine();

                q = p;
            }
        }

        static public void DumpBASIC(UInt16[] text)
        {
            Int32 p = 0;
            Int32 err = 0;
            while (p < text.Length)
            {
                Int32 q = p;
                UInt16 line = text[q++];
                if (DEBUG)
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
            if (err != 0) Console.Error.WriteLine("Parse Errors: {0:D0}", err);
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

        static private Int32 addr = -1;

        static public String LoaderDirective(Int32 word)
        {
            Int32 code = (word >> 17) & 15;
            switch (word & 0xc10000) // 60200000
            {
                // non-memory instructions, or data
                // 0ddddddd (000aaaaa, 001aaaaa, 0000bbxx
                // 1ddddddd
                case 0x000000:
                case 0x010000:
                    return String.Format("{0} {1}      {2}       {3}", OctalString(addr++, 5, '0'), OctalString(word, 8, '0'), "", "");

                // memory referencing instructions
                // 2ddddddd
                // 3ddddddd
                case 0x400000:
                case 0x410000:
                    return String.Format("{0} {1}      {2}       {3}", OctalString(addr++, 5, '0'), OctalString(word, 8, '0'), "", "");

                // subroutine or common
                // 4ddddddd
                // 5ddddddd
                case 0x800000:
                case 0x810000:
                    return String.Format("{0} {1}      {2}       {3}", OctalString(addr++, 5, '0'), OctalString(word, 8, '0'), "", "");

                // special
                // 6d[0145]ddddd
                // 7d[0145]ddddd
                case 0xc00000:
                    switch (code)
                    {
                        case 0: return String.Format("      {0}      ORG  '{1}", OctalString(word, 8, '0'), OctalString(addr = word & 0xffff, 5, '0'));
                        case 1: return String.Format("      {0}      END", OctalString(word, 8, '0'));
                        case 8: return String.Format("      {0} $", OctalString(word, 8, '0'));
                        default: return "Unhandled";
                    }

                // literal referencing instructions
                // 6d[2367]ddddd
                // 7d[2367]ddddd
                default:
                    return String.Format("{0} {1}      {2}       {3}", OctalString(addr++, 5, '0'), OctalString(word, 8, '0'), "", "");
            }
        }
    }
}
