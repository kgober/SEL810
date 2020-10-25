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


// Network Protocol:
// Interrupts: send 'I', receive '-' or 24 hex digits (highest to lowest priority)
// CommandReady: send 'C', receive '1' (ready) or '0' (not ready)
// ReadReady: send 'R', receive '1' (ready) or '0' (not ready)
// WriteReady: send 'W', receive '1' (ready) or '0' (not ready)
// Test: send 'T' followed by 4 hex digits (MSB first), receive '1' or '0'
// Command: send 'c' followed by 4 hex digits, receive '.'
// Read: send 'r', receive 4 hex digits
// Write: send 'w' followed by 4 hex digits, receive '.'
// Exit: send 'x', receive 'x'

// Configuration file format:
// num tape-file-pathname
// A decimal number 1-255, a single space, then the tape file pathname
// Tape number 0 is reserved for "prompt user for pathname at load time"
// Lines beginning with # are ignored
// Blank lines are ignored


using System;
using System.IO;
using System.Net.Sockets;

namespace Tape_Server
{
    class Program
    {
        const UInt16 DEFAULT_PORT = 8102;

        static UInt16 TCP_PORT = DEFAULT_PORT;
        static String[] FILE_NAMES = new String[256];
        static Boolean DEBUG = false;

        static void Main(String[] args)
        {
            Int32 ap = 0;
            while (ap < args.Length)
            {
                String arg = args[ap++];
                if (arg == "-c")
                {
                    arg = arg.Substring(2);
                    if (arg.Length == 0) arg = args[ap++];
                    foreach (String line in File.ReadAllLines(arg))
                    {
                        if ((line == null) || (line.Length == 0)) continue;
                        if (line[0] == '#') continue;
                        Int32 p = line.IndexOf(' ');
                        if (p == -1)
                        {
                            Console.Error.WriteLine("Unrecognized configuration file entry: {0}", line);
                            continue;
                        }
                        String s = line.Substring(0, p);
                        Byte num;
                        if ((!Byte.TryParse(s, out num)) || (num == 0))
                        {
                            Console.Error.WriteLine("Invalid tape number: {0}", s);
                            continue;
                        }
                        FILE_NAMES[num] = line.Substring(p + 1);
                        Console.Error.WriteLine("Tape {0:D0}: {1}", num, FILE_NAMES[num]);
                    }
                }
                else if (arg == "-p")
                {
                    arg = arg.Substring(2);
                    if (arg.Length == 0) arg = args[ap++];
                    if (!UInt16.TryParse(arg, out TCP_PORT)) TCP_PORT = DEFAULT_PORT;
                }
                else if (arg == "-d")
                {
                    DEBUG = true;
                }
            }

            TcpListener L = new TcpListener(TCP_PORT);
            L.Start();
            Console.Error.WriteLine("SEL810 Tape Server listening on port {0:D0}.", TCP_PORT);
            while (true)
            {
                FileStream rdr = null;      // the backing file for the tape loaded in the reader
                Int32 rdr_buf = -1;         // reader buffer
                Int32 rdr_num = -1;         // the tape number currently in the reader
                Boolean rdr_en = false;     // whether reader is enabled
                Boolean rdr_ien = false;    // whether reader interrupts are enabled
                Boolean rdr_int = false;    // whether the reader is requesting an interrupt

                FileStream pun = null;      // the backing file for the tape loaded in the punch
                Boolean pun_ien = false;    // whether punch interrupts are enabled
                Boolean pun_int = false;    // whether the punch is requesting an interrupt

                Byte[] buf = new Byte[24];  // byte buffer for TCP send/receive
                Socket S = L.AcceptSocket();
                Console.Error.WriteLine("Connection accepted from {0}.", S.RemoteEndPoint.ToString());
                while (true)
                {
                    Int32 n, p;
                    Int32 ct = S.Receive(buf, 0, 1, SocketFlags.None); // receive command byte from CPU
                    if (ct == 0) break;
                    if (DEBUG) Console.Out.Write((Char)(buf[0]));
                    switch ((Char)(buf[0]))
                    {
                        case 'I': // request Interrupt status
                            if ((!rdr_int) && (!pun_int))
                            {
                                buf[0] = (Byte)'-'; // just send '-' if no interrupts requested
                                n = 1;
                            }
                            else
                            {
                                n = 0;
                                if (rdr_int) n |= 4; // group 0, level 11
                                if (pun_int) n |= 8; // group 0, level 12
                                buf[0] = BinaryToHex(n);
                                buf[1] = (Byte)'-'; // all remaining digits are 0
                                n = 2;
                            }
                            p = 0;
                            while (n > 0)
                            {
                                ct = S.Send(buf, p, n, SocketFlags.None);
                                if (ct == 0) break;
                                if (DEBUG) for (Int32 i = 0; i < ct; i++) Console.Out.Write((Char)(buf[p + i]));
                                p += ct;
                                n -= ct;
                            }
                            break;
                        case 'C': // is device ready for command? (for CEU skip mode)
                            buf[0] = (Byte)('1'); // always ready for commands
                            S.Send(buf, 0, 1, SocketFlags.None);
                            if (DEBUG) Console.Out.Write((Char)(buf[0]));
                            break;
                        case 'R': // is device ready to be read? (for AIP/MIP skip mode)
                            buf[0] = (Byte)(((rdr == null) || (rdr.Position == rdr.Length)) ? '0' : '1');
                            S.Send(buf, 0, 1, SocketFlags.None);
                            if (DEBUG) Console.Out.Write((Char)(buf[0]));
                            break;
                        case 'W': // is device ready to be written? (for AOP/MOP skip mode)
                            buf[0] = (Byte)((pun == null) ? '0' : '1');
                            S.Send(buf, 0, 1, SocketFlags.None);
                            if (DEBUG) Console.Out.Write((Char)(buf[0]));
                            break;
                        case 'T': // test device (TEU)
                            n = 4;  // receive 16 bits of data as 4 4-bit hex digits
                            p = 0;
                            while (n > 0)
                            {
                                ct = S.Receive(buf, p, n, SocketFlags.None);
                                if (ct == 0) break;
                                if (DEBUG) for (Int32 i = 0; i < ct; i++) Console.Out.Write((Char)(buf[p + i]));
                                p += ct;
                                n -= ct;
                            }
                            if (n > 0) break;
                            buf[0] = (Byte)('1'); // always sucessful
                            S.Send(buf, 0, 1, SocketFlags.None);
                            if (DEBUG) Console.Out.Write((Char)(buf[0]));
                            break;
                        case 'c': // command device (CEU)
                            n = 4; // receive 16 bits of data as 4 4-bit hex digits
                            p = 0;
                            while (n > 0)
                            {
                                ct = S.Receive(buf, p, n, SocketFlags.None);
                                if (ct == 0) break;
                                if (DEBUG) for (Int32 i = 0; i < ct; i++) Console.Out.Write((Char)(buf[p + i]));
                                p += ct;
                                n -= ct;
                            }
                            if (n > 0) break;
                            for (Int32 i = 0; i < 4; i++) n = (n << 4) | HexToBinary(buf[i]);
                            if ((n & 0x2000) != 0) // interrupt 0,11 - reader buffer filled
                            {
                                rdr_ien = ((n & 0x4000) != 0); // enable interrupt if n & 0x4000, else disable
                                rdr_int = (rdr_buf != -1) ? rdr_ien : false; // interrupt immediately if buffer full
                            }
                            if ((n & 0x1000) != 0) // interrupt 0,12 - punch buffer emptied
                            {
                                pun_ien = ((n & 0x4000) != 0); // enable interrupt if n & 0x4000, else disable
                                pun_int = pun_ien; // interrupt immediately if buffer empty (which it always is)
                            }
                            if ((n & 0x0800) != 0) // punch power on
                            {
                                if (pun != null) pun.Close(); // if a tape was already open, close it
                                p = n & 0x00ff;
                                String name = FILE_NAMES[p]; // get name of new tape
                                if (name == null) // if we don't know this one yet, ask user
                                {
                                    Console.Error.Write("Enter tape {0:D0} pathname for punch: ", p);
                                    name = Console.In.ReadLine();
                                    if (p != 0) FILE_NAMES[p] = name; // never save name for tape #0
                                }
                                pun = File.Open(name, FileMode.Append, FileAccess.Write);
                            }
                            if ((n & 0x0400) != 0) // punch power off
                            {
                                if (pun != null) pun.Close();
                                pun = null;
                            }
                            if ((n & 0x0300) == 0x0300) // special case: change reader tape
                            {
                                if (rdr != null) rdr.Close(); // if a tape was already open, close it
                                p = n & 0x00ff;
                                String name = FILE_NAMES[p]; // get name of new tape
                                if (name == null) // if we don't know this one yet, ask user
                                {
                                    Console.Error.Write("Enter tape {0:D0} pathname for reader: ", p);
                                    name = Console.In.ReadLine();
                                    if ((p != 0) && (File.Exists(name))) FILE_NAMES[p] = name;
                                }
                                rdr = File.Open(name, FileMode.Open, FileAccess.Read);
                                if (rdr == null) break;
                                rdr_num = p;
                                rdr_en = true;
                                if (rdr_buf == -1) rdr_buf = rdr.ReadByte(); // pre-fill read buffer
                                rdr_int = (rdr_buf != -1) ? rdr_ien : false; // interrupt immediately if buffer full
                            }
                            if ((n & 0x0200) != 0) // reader enable
                            {
                                p = n & 0x00ff;
                                if ((rdr != null) && (p != rdr_num)) // if a different tape was open, close it
                                {
                                    rdr.Close();
                                    rdr = null;
                                }
                                if (rdr == null)
                                {
                                    String name = FILE_NAMES[p]; // get name of new tape
                                    if (name == null) // if we don't know this one yet, ask user
                                    {
                                        Console.Error.Write("Enter tape {0:D0} pathname for reader: ", p);
                                        name = Console.In.ReadLine();
                                        if ((p != 0) && (File.Exists(name))) FILE_NAMES[p] = name;
                                    }
                                    rdr = File.Open(name, FileMode.Open, FileAccess.Read);
                                }
                                if (rdr == null) break;
                                rdr_num = p;
                                rdr_en = true;
                                if (rdr_buf == -1) rdr_buf = rdr.ReadByte();
                                rdr_int = (rdr_buf != -1) ? rdr_ien : false; // interrupt immediately if buffer full
                            }
                            if ((n & 0x0100) != 0) // reader disable
                            {
                                rdr_en = false; // don't close backing file, this might just be a pause
                            }
                            buf[0] = (Byte)'.'; // send '.' acknowledgement
                            S.Send(buf, 0, 1, SocketFlags.None);
                            if (DEBUG) Console.Out.Write((Char)(buf[0]));
                            break;
                        case 'r': // read device (AIP/MIP)
                            if (rdr_buf != -1) // send 16 bits as 4 4-bit hex digits
                            {
                                buf[0] = BinaryToHex((rdr_buf >> 12) & 15);
                                buf[1] = BinaryToHex((rdr_buf >> 8) & 15);
                                buf[2] = BinaryToHex((rdr_buf >> 4) & 15);
                                buf[3] = BinaryToHex(rdr_buf & 15);
                            }
                            else // send '????' if buffer is empty
                            {
                                for (Int32 i = 0; i < 4; i++) buf[i] = (Byte)'?';
                            }
                            n = 4;
                            p = 0;
                            while (n > 0)
                            {
                                ct = S.Send(buf, p, n, SocketFlags.None);
                                if (ct == 0) break;
                                if (DEBUG) for (Int32 i = 0; i < ct; i++) Console.Out.Write((Char)(buf[p + i]));
                                p += ct;
                                n -= ct;
                            }
                            rdr_int = false; // reading clears interrupt
                            if (rdr_en) // if reader is enabled, re-fill buffer from backing file
                            {
                                rdr_buf = rdr.ReadByte();
                                if ((rdr_buf != -1) && (rdr_ien)) rdr_int = true; // raise interrupt again
                            }
                            break;
                        case 'w': // write device (AOP/MOP)
                            n = 4; // receive 16 bits of data as 4 4-bit hex digits
                            p = 0;
                            while (n > 0)
                            {
                                ct = S.Receive(buf, p, n, SocketFlags.None);
                                if (ct == 0) break;
                                if (DEBUG) for (Int32 i = 0; i < ct; i++) Console.Out.Write((Char)(buf[p + i]));
                                p += ct;
                                n -= ct;
                            }
                            if (n > 0) break;
                            if (pun != null)
                            {
                                if (pun.Position == 0) // for new files, first write some leader bytes
                                {
                                    for (Int32 i = 0; i < 128; i++) pun.WriteByte(0);
                                }
                                pun.WriteByte((Byte)((HexToBinary(buf[0]) << 4) | HexToBinary(buf[1])));
                            }
                            pun_int = false;
                            buf[0] = (Byte)'.'; // send '.' acknowledgement
                            S.Send(buf, 0, 1, SocketFlags.None);
                            if (DEBUG) Console.Out.Write((Char)(buf[0]));
                            if (pun_ien)
                            {
                                if (pun != null) pun.Flush();
                                pun_int = true;
                            }
                            break;
                        case 'x': // exit (CPU Emulator wants to disconnect)
                            buf[0] = (Byte)'x';
                            S.Send(buf, 0, 1, SocketFlags.None);
                            if (DEBUG) Console.Out.WriteLine((Char)(buf[0]));
                            break;
                    }
                }
                S.Close();
                Console.Error.WriteLine("Connection closed.");
            }
        }

        static Int32 HexToBinary(Int32 value)
        {
            value |= 32; // force letters to lower case
            if (value > 96) return value - 87; // 'a' -> 10, 'b' -> 11, etc.
            return value - 48; // '0' -> 0, '1' -> 1, etc.
        }

        static Byte BinaryToHex(Int32 value)
        {
            if (value > 9) return (Byte)(value + 87); // 10 -> 'a', 11 -> 'b', etc.
            return (Byte)(value + 48); // 0 -> '0', 1 -> '1', etc.
        }
    }
}
