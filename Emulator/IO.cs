// IO.cs
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
// Interrupts: send 'I', receive 24 hex digits ('-' means remaining digits are '0')
// CommandReady: send 'C', receive '1' (ready) or '0' (not ready)
// ReadReady: send 'R', receive '1' (ready) or '0' (not ready)
// WriteReady: send 'W', receive '1' (ready) or '0' (not ready)
// Test: send 'T' followed by 4 hex digits (MSB first), receive '1' or '0'
// Command: send 'c' followed by 4 hex digits, receive '.'
// Read: send 'r', receive 4 hex digits (or '????' if not ready)
// Write: send 'w' followed by 4 hex digits, receive '.'
// Exit: send 'x', receive 'x'


using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Emulator
{
    abstract class IO
    {
        public abstract Int16[] Interrupts { get; }     // get interrupts requested by device
        public abstract Boolean CommandReady { get; }   // check if device ready for command (CEU)
        public abstract Boolean ReadReady { get; }      // check if device ready for read (AIP/MIP)
        public abstract Boolean WriteReady { get; }     // check if device ready for write (AOP/MOP)
        public abstract Boolean Test(Int16 word);       // test device (TEU)
        public abstract Boolean Command(Int16 word);    // command device (CEU)
        public abstract Boolean Read(out Int16 word);   // read device (AIP/MIP)
        public abstract Boolean Write(Int16 word);      // write device (AOP/MOP)
        public abstract void Exit();                    // disconnect device from Emulator
    }

    class Teletype : IO
    {
        [Flags]
        public enum Mode
        {
            None = 0,
            Printer = 1,
            Punch = 2,
            Both = 3,
            Keyboard = 4,
            Reader = 8,
        }

        private static TimeSpan sReaderReadDelay = new TimeSpan(0, 0, 0, 0, 20); // 50 is more accurate
        private static TimeSpan sReaderStopDelay = new TimeSpan(0, 0, 0, 0, 12);
        private static TimeSpan sKeyboardDelay = new TimeSpan(0, 0, 0, 0, 40); // 100 is more accurate
        private static TimeSpan sPrinterDelay = new TimeSpan(0, 0, 0, 0, 40); // 100 is more accurate

        private Int32 mNetworkPort;
        private Thread mNetworkThread;
        private volatile Boolean mNetworkExit;
        private NetworkStream mTerminal;

        private Mode mInMode = Mode.Keyboard;
        private DateTime mLastRead = DateTime.MinValue;
        private Stream mReader;
        private Int32 mReaderCount;
        private Int32 mReaderBuf;
        private Int32 mKeyBuf;

        private Mode mOutMode = Mode.Printer;
        private DateTime mLastWrite = DateTime.MinValue;
        private Stream mPunch;
        private Int32 mPunchCount;

        private Int16[] mInterrupts = new Int16[8];
        private Boolean mInt_EnIn;
        private Boolean mInt_In;
        private Boolean mInt_EnOut;
        private Boolean mInt_Out;

        public Teletype(Int32 listenPort)
        {
            mNetworkPort = listenPort;
            mNetworkThread = new Thread(new ThreadStart(TerminalThread));
            mNetworkThread.Start();
        }

        public override Int16[] Interrupts
        {
            get
            {
                DateTime now = DateTime.Now;
                if ((mInMode == Mode.Keyboard) && (mTerminal != null) && (mKeyBuf == -1))
                {
                    if ((mTerminal.DataAvailable) && ((now - mLastRead) > sKeyboardDelay))
                    {
                        mKeyBuf = mTerminal.ReadByte() | 128;
                    }
                }
                if ((mInMode == Mode.Reader) && (mReader != null) && (mReaderBuf == -1))
                {
                    if ((mReader.Position < mReader.Length) && ((now - mLastRead) > sReaderStopDelay))
                    {
                        mReaderBuf = mReader.ReadByte();
                        Console.Out.Write(((++mReaderCount % 512) == 0) ? '^' : '`');
                    }
                }
                if ((mInt_EnIn) && (!mInt_In))
                {
                    if ((mInMode == Mode.Keyboard) && (mKeyBuf != -1)) mInt_In = true;
                    else if ((mInMode == Mode.Reader) && (mReaderBuf != -1)) mInt_In = true;
                }
                if ((mInt_EnOut) && (!mInt_Out))
                {
                    if ((now - mLastWrite) > sPrinterDelay) mInt_Out = true;
                }
                if ((!mInt_In) && (!mInt_Out)) return null;
                mInterrupts[0] = 0;
                if (mInt_In) mInterrupts[0] |= 2;
                if (mInt_Out) mInterrupts[0] |= 1;
                return mInterrupts;
            }
        }

        public override Boolean CommandReady
        {
            get
            {
                DateTime now = DateTime.Now;
                if ((mInMode == Mode.Keyboard) && (mTerminal != null) && (mKeyBuf == -1))
                {
                    if ((mTerminal.DataAvailable) && ((now - mLastRead) > sKeyboardDelay))
                    {
                        mKeyBuf = mTerminal.ReadByte() | 128;
                    }
                }
                if ((mInMode == Mode.Reader) && (mReader != null) && (mReaderBuf == -1))
                {
                    if ((mReader.Position < mReader.Length) && ((now - mLastRead) > sReaderStopDelay))
                    {
                        mReaderBuf = mReader.ReadByte();
                        Console.Out.Write(((++mReaderCount % 512) == 0) ? '^' : '`');
                    }
                }
                return true;
            }
        }

        public override Boolean ReadReady
        {
            get
            {
                DateTime now = DateTime.Now;
                if ((mInMode == Mode.Keyboard) && (mTerminal != null) && (mKeyBuf == -1))
                {
                    if ((mTerminal.DataAvailable) && ((now - mLastRead) > sKeyboardDelay))
                    {
                        mKeyBuf = mTerminal.ReadByte() | 128;
                    }
                }
                if ((mInMode == Mode.Reader) && (mReader != null) && (mReaderBuf == -1))
                {
                    if ((mReader.Position < mReader.Length) && ((now - mLastRead) > sReaderStopDelay))
                    {
                        mReaderBuf = mReader.ReadByte();
                        Console.Out.Write(((++mReaderCount % 512) == 0) ? '^' : '`');
                    }
                }
                if ((mInMode == Mode.Keyboard) && (mKeyBuf != -1)) return true;
                if ((mInMode == Mode.Reader) && (mReaderBuf != -1) && ((now - mLastRead) > sReaderReadDelay)) return true;
                return false;
            }
        }

        public override Boolean WriteReady
        {
            get
            {
                if ((DateTime.Now - mLastWrite) < sPrinterDelay) return false;
                if ((mOutMode == Mode.Printer) && (mTerminal != null)) return true;
                if ((mOutMode == Mode.Punch) && (mPunch != null)) return true;
                if ((mOutMode == Mode.Both) && (mTerminal != null) && (mPunch != null)) return true;
                return false;
            }
        }

        public Mode OutputMode
        {
            get { return mOutMode; }
            set { mOutMode = value; }
        }

        public override Boolean Test(Int16 word)
        {
            return true;
        }

        public override Boolean Command(Int16 word)
        {
            if ((word & 0x2000) != 0)
            {
                mInt_EnIn = ((word & 0x4000) != 0);
                if (!mInt_EnIn)
                {
                    mInterrupts[0] &= 0x0ffd;
                    mInt_In = false;
                }
            }
            if ((word & 0x1000) != 0)
            {
                mInt_EnOut = ((word & 0x4000) != 0);
                if (!mInt_EnOut)
                {
                    mInterrupts[0] &= 0x0ffe;
                    mInt_Out = false;
                }
            }
            if ((word & 0x0800) != 0)
            {
                mInMode = Mode.Reader;
                mReaderBuf = -1;
                mKeyBuf = -1;
                mInt_In = false;
            }
            if ((word & 0x0400) != 0)
            {
                mInMode = Mode.Keyboard;
                mReaderBuf = -1;
                mKeyBuf = -1;
                mInt_In = false;
            }
            if ((word & 0x0200) != 0)
            {
                mInMode = Mode.None;
                mReaderBuf = -1;
                mKeyBuf = -1;
                mInt_In = false;
            }
            return true;
        }

        public override Boolean Read(out Int16 word)
        {
            DateTime now = DateTime.Now;
            if ((mInMode == Mode.Reader) && (mReaderBuf != -1))
            {
                word = (Int16)(mReaderBuf & 0xff);
                mReaderBuf = -1;
                mInt_In = false;
                mLastRead = now;
                return true;
            }
            if ((mInMode == Mode.Keyboard) && (mKeyBuf != -1))
            {
                word = (Int16)(mKeyBuf & 0xff);
                mKeyBuf = -1;
                mInt_In = false;
                mLastRead = now;
                return true;
            }
            word = 0;
            return false;
        }

        public override Boolean Write(Int16 word)
        {
            DateTime now = DateTime.Now; 
            Byte b = (Byte)((word >> 8) & 0xff);
            if (((mOutMode & Mode.Printer) != 0) && (mTerminal != null))
            {
                mTerminal.WriteByte(b);
                mLastWrite = now;
            }
            if (((mOutMode & Mode.Punch) != 0) && (mPunch != null))
            {
                if (mPunch.Position == 0) for (Int32 i = 0; i < 128; i++) mPunch.WriteByte(0);
                mPunch.WriteByte(b);
                Console.Out.Write(((++mReaderCount % 512) == 0) ? '+' : '-');
                mLastWrite = now;
            }
            mInt_Out = false;
            return true;
        }

        public override void Exit()
        {
            if (mReader != null) SetReader(null);
            if (mPunch != null) SetPunch(null);
            mNetworkExit = true;
            mNetworkThread.Join();
        }

        public void SetReader(String inputFile)
        {
            if ((inputFile == null) || (inputFile.Length == 0))
            {
                if (mReader != null) mReader.Close();
                mReader = null;
                Console.Out.Write("[-RDR]");
                return;
            }
            mReader = new FileStream(inputFile, FileMode.Open, FileAccess.Read);
            mReaderCount = 0;
            Console.Out.Write("[+RDR]");
        }

        public void SetPunch(String outputFile)
        {
            if ((outputFile == null) || (outputFile.Length == 0))
            {
                if (mPunch != null) mPunch.Close();
                mPunch = null;
                Console.Out.Write("[-PUN]");
                return;
            }
            mPunch = new FileStream(outputFile, FileMode.Append, FileAccess.Write);
            Console.Out.Write("[+PUN]");
        }

        private void TerminalThread()
        {
            TcpListener L = new TcpListener(IPAddress.Any, mNetworkPort);
            L.Start();
            while (!mNetworkExit)
            {
                while ((!L.Pending()) && (!mNetworkExit)) Thread.Sleep(100);
                if (mNetworkExit) break;
                TcpClient C = L.AcceptTcpClient();
                lock(this) mTerminal = C.GetStream();
                Console.Out.Write("[+TTY]");
                while ((C.Connected) && (!mNetworkExit)) Thread.Sleep(100);
                C.Close();
                Console.Out.Write("[-TTY]");
                lock(this) mTerminal = null;
            }
            L.Stop();
        }
    }

    
    class NetworkDevice : IO
    {
        private String mHost;
        private Int32 mPort;
        private TcpClient mClient;
        private Socket mSocket;
        private Boolean mLazyConnect;
        private SocketError mLastSocketError;
        private Byte[] mBuf = new Byte[24];
        private Int16[] mInts = new Int16[8];

        public NetworkDevice(String host, Int32 port)
        {
            mHost = host;
            mPort = port;
            mClient = new TcpClient();
            mSocket = mClient.Client;
            mLazyConnect = true;
        }

        public override Int16[] Interrupts
        {
            get
            {
                if (!mSocket.Connected)
                {
                    if (!mLazyConnect) return null;
                    if (!Connect()) return null;
                }
                mBuf[0] = (Byte)'I';
                if (mSocket.Send(mBuf, 0, 1, SocketFlags.None, out mLastSocketError) == 0) return null;
                Int32 p = 0;
                while (p < 24)
                {
                    Int32 ct = mSocket.Receive(mBuf, p, 1, SocketFlags.None, out mLastSocketError);
                    if (ct == 0) return null;
                    if (mBuf[p] == (Byte)'-') break;
                    mBuf[p] = HexToBinary(mBuf[p]);
                    p++;
                }
                if (p == 0) return null;
                while (p < 24) mBuf[p++] = 0;
                p = 0;
                for (Int32 i = 0; i < 8; i++)
                {
                    Int32 n = mBuf[p++] << 8;
                    n |= mBuf[p++] << 4;
                    n |= mBuf[p++];
                    mInts[i] = (Int16)(n);
                }
                return mInts;
            }
        }

        public override Boolean CommandReady
        {
            get
            {
                if (!mSocket.Connected)
                {
                    if (!mLazyConnect) return false;
                    if (!Connect()) return false;
                }
                mBuf[0] = (Byte)'C';
                if (mSocket.Send(mBuf, 0, 1, SocketFlags.None, out mLastSocketError) == 0) return false;
                if (mSocket.Receive(mBuf, 0, 1, SocketFlags.None, out mLastSocketError) == 0) return false;
                return (mBuf[0] == '1');
            }
        }

        public override Boolean ReadReady
        {
            get
            {
                if (!mSocket.Connected)
                {
                    if (!mLazyConnect) return false;
                    if (!Connect()) return false;
                }
                mBuf[0] = (Byte)'R';
                if (mSocket.Send(mBuf, 0, 1, SocketFlags.None, out mLastSocketError) == 0) return false;
                if (mSocket.Receive(mBuf, 0, 1, SocketFlags.None, out mLastSocketError) == 0) return false;
                return (mBuf[0] == '1');
            }
        }

        public override Boolean WriteReady
        {
            get
            {
                if (!mSocket.Connected)
                {
                    if (!mLazyConnect) return false;
                    if (!Connect()) return false;
                }
                mBuf[0] = (Byte)'W';
                if (mSocket.Send(mBuf, 0, 1, SocketFlags.None, out mLastSocketError) == 0) return false;
                if (mSocket.Receive(mBuf, 0, 1, SocketFlags.None, out mLastSocketError) == 0) return false;
                return (mBuf[0] == '1');
            }
        }

        public override Boolean Test(Int16 word)
        {
            if (!mSocket.Connected)
            {
                if (!mLazyConnect) return false;
                if (!Connect()) return false;
            }
            mBuf[0] = (Byte)'T';
            mBuf[1] = BinaryToHex((word >> 12) & 15);
            mBuf[2] = BinaryToHex((word >> 8) & 15);
            mBuf[3] = BinaryToHex((word >> 4) & 15);
            mBuf[4] = BinaryToHex(word & 15);
            Int32 n = 5;
            Int32 p = 0;
            while (n > 0)
            {
                Int32 ct = mSocket.Send(mBuf, p, n, SocketFlags.None, out mLastSocketError);
                if (ct == 0) return false;
                p += ct;
                n -= ct;
            }
            if (mSocket.Receive(mBuf, 0, 1, SocketFlags.None, out mLastSocketError) == 0) return false;
            return (mBuf[0] == '1');
        }

        public override Boolean Command(Int16 word)
        {
            if (!mSocket.Connected)
            {
                if (!mLazyConnect) return false;
                if (!Connect()) return false;
            }
            mBuf[0] = (Byte)'c';
            mBuf[1] = BinaryToHex((word >> 12) & 15);
            mBuf[2] = BinaryToHex((word >> 8) & 15);
            mBuf[3] = BinaryToHex((word >> 4) & 15);
            mBuf[4] = BinaryToHex(word & 15);
            Int32 n = 5;
            Int32 p = 0;
            while (n > 0)
            {
                Int32 ct = mSocket.Send(mBuf, p, n, SocketFlags.None, out mLastSocketError);
                if (ct == 0) return false;
                p += ct;
                n -= ct;
            }
            if (mSocket.Receive(mBuf, 0, 1, SocketFlags.None, out mLastSocketError) == 0) return false;
            return (mBuf[0] == '.');
        }

        public override Boolean Read(out Int16 word)
        {
            word = 0;
            if (!mSocket.Connected)
            {
                if (!mLazyConnect) return false;
                if (!Connect()) return false;
            }
            mBuf[0] = (Byte)'r';
            if (mSocket.Send(mBuf, 0, 1, SocketFlags.None, out mLastSocketError) == 0) return false;
            Int32 n = 4;
            Int32 p = 0;
            while (n > 0)
            {
                Int32 ct = mSocket.Receive(mBuf, p, n, SocketFlags.None, out mLastSocketError);
                if (ct == 0) return false;
                p += ct;
                n -= ct;
            }
            n = HexToBinary(mBuf[0]) << 12;
            n |= HexToBinary(mBuf[1]) << 8;
            n |= HexToBinary(mBuf[2]) << 4;
            n |= HexToBinary(mBuf[3]);
            word = (Int16)(n);
            return true;
        }

        public override Boolean Write(Int16 word)
        {
            if (!mSocket.Connected)
            {
                if (!mLazyConnect) return false;
                if (!Connect()) return false;
            }
            mBuf[0] = (Byte)'w';
            mBuf[1] = BinaryToHex((word >> 12) & 15);
            mBuf[2] = BinaryToHex((word >> 8) & 15);
            mBuf[3] = BinaryToHex((word >> 4) & 15);
            mBuf[4] = BinaryToHex(word & 15);
            Int32 n = 5;
            Int32 p = 0;
            while (n > 0)
            {
                Int32 ct = mSocket.Send(mBuf, p, n, SocketFlags.None, out mLastSocketError);
                if (ct == 0) return false;
                p += ct;
                n -= ct;
            }
            if (mSocket.Receive(mBuf, 0, 1, SocketFlags.None, out mLastSocketError) == 0) return false;
            return (mBuf[0] == '.');
        }

        public override void Exit()
        {
            if (!mSocket.Connected) return;
            mBuf[0] = (Byte)'x';
            Int32 ct = mSocket.Send(mBuf, 0, 1, SocketFlags.None, out mLastSocketError);
            if (ct != 0) mSocket.Receive(mBuf, 0, 1, SocketFlags.None, out mLastSocketError);
            mSocket.Close();
        }

        public Boolean Connect()
        {
            if (mSocket.Connected) return true;
            try
            {
                mSocket.Connect(mHost, mPort);
                return mSocket.Connected;
            }
            catch
            {
                return false;
            }
        }

        private Byte HexToBinary(Int32 value)
        {
            value |= 32; // force letters to lower case
            if (value > 96) return (Byte)(value - 87); // 'a' -> 10, 'b' -> 11, etc.
            return (Byte)(value - 48); // '0' -> 0, '1' -> 1, etc.
        }

        private Byte BinaryToHex(Int32 value)
        {
            if (value > 9) return (Byte)(value + 87); // 10 -> 'a', 11 -> 'b', etc.
            return (Byte)(value + 48); // 0 -> '0', 1 -> '1', etc.
        }
    }
}
