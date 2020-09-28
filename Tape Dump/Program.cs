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
        static Boolean DEBUG;

        static Int32 Main(String[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Usage: TapeDump [-d] imagefile");
                return 2;
            }

            Int32 ap = 0;
            if (args[ap] == "-d")
            {
                DEBUG = true;
                ap++;
            }

            Console.Error.WriteLine("Reading tape...");
            Byte[] tape = File.ReadAllBytes(args[ap]);
            Int32 p = 0;
            
            // leader
            while (tape[p] == 0) p++;
            if (p != 0) Console.Error.WriteLine("Skipped {0:D0} leader bytes", p);

            // program size
            Int32 n = 0xff;
            for (Int32 i = 0; i < 3; i++)
            {
                n <<= 8;
                n |= tape[p++];
            }
            n = -n;

            // program text
            Console.Error.WriteLine("Reading {0:D0} words of program text...", n);
            UInt16[] text = new UInt16[n];
            Int32 sum = 0;
            for (Int32 i = 0; i < n; i++)
            {
                text[i] = (UInt16)(tape[p++] << 8);
                text[i] |= tape[p++];
                if ((n == 2619) && (i == 0x01f0) && (text[i] == 0xa0c0))
                {
                    text[i] = 0xa0c3; // patch for SEL810_basic_blackjack.bin
                    Console.Error.WriteLine("Patching SEL810_basic_blackjack.bin 01F0:A0C0 -> A0C3");
                }
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

            // dump BASIC program
            DumpBASIC(text);

            return 0;
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
                            // not sure why these appear
                            break;
                        case 0x0200:
                            Console.Out.Write("\"");
                            while ((val = word & 0x00ff) >= 0x80)
                            {
                                Console.Out.Write((Char)(val & 0x7f));
                                if (q == p)
                                {
                                    val = 0;
                                    break;
                                }
                                word = text[q++];
                                val = (word & 0xff00) >> 8;
                                if ((val & 0xfe) == 0x02)
                                {
                                    Console.Out.Write("\"");
                                    val = word & 0x01ff;
                                    break;
                                }
                                if (val < 0x80) break;
                                Console.Out.Write((Char)(val & 0x7f));
                            }
                            break;
                        case 0x0400:
                            Console.Out.Write(","); // PRINT modifier
                            break;
                        case 0x0600:
                            Console.Out.Write(";");
                            break;
                        case 0x0800:
                            Console.Out.Write(")");
                            break;
                        case 0x0a00:
                            Console.Out.Write("]"); // not verified
                            break;
                        case 0x0c00:
                            Console.Out.Write(","); // array index separator - not verified
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
                            Console.Out.Write("<"); // not verified
                            break;
                        case 0x1e00:
                            Console.Out.Write("<>"); // not verified
                            break;
                        case 0x2000:
                            Console.Out.Write("="); // comparison, aka ==
                            break;
                        case 0x2400:
                            Console.Out.Write("["); // not verified
                            break;
                        case 0x2600:
                            Console.Out.Write("(");
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
                            Console.Out.Write("DIM "); // not verified
                            break;
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
                            Console.Out.Write("GOSUB "); // not verified
                            break;
                        case 0x4800:
                            Console.Out.Write("RETURN "); // not verified
                            break;
                        case 0x4a00:
                            Console.Out.Write("END ");
                            break;
                        case 0x5600:
                            Console.Out.Write("PRINT ");
                            break;
                        case 0x5800:
                            Console.Out.Write("INPUT ");
                            break;
                        case 0x5e00:
                            Console.Out.Write(" THEN ");
                            break;
                        case 0x6000:
                            Console.Out.Write(" TO ");
                            break;
                        case 0x6200:
                            Console.Out.Write(" STEP ");
                            break;
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
                            //case 0x03f:
                            //    Console.Out.Write("SIN"); // not verified
                            //    break;
                            case 0x07f:
                                Console.Out.Write("LOG"); // not verified
                                break;
                            case 0x08f:
                                Console.Out.Write("ABS"); // not verified, wild guess
                                break;
                            case 0x0af:
                                Console.Out.Write("INT");
                                break;
                            case 0x0bf:
                                Console.Out.Write("RND"); // not verified
                                break;
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
                        Console.Out.Write((Char)(64 + ((val & 0x1f0) >> 4)));
                        if ((val & 0x00f) > 4) Console.Out.Write((Char)(43 + (val & 0x00f)));
                    }
                    else if (((val & 0x1f0) >= 0x010) && ((val & 0x00f) == 1))
                    {
                        Console.Out.Write((Char)(64 + ((val & 0x1f0) >> 4)));
                        Console.Out.Write('#');
                    }
                    else if (((val & 0x1f0) >= 0x010) && ((val & 0x00f) == 2))
                    {
                        Console.Out.Write((Char)(64 + ((val & 0x1f0) >> 4)));
                        Console.Out.Write('%');
                    }
                    else
                    {
                        switch (val)
                        {
                            //case 0x0af:
                            //    Console.Out.Write("INT");
                            //    break;
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
            UInt32 frac = w1 ; // 15 bits from w1 (ignore sign bit)
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
    }
}
