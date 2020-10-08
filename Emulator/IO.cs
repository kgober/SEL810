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


using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace Emulator
{
    abstract class IO
    {
        public abstract Boolean CommandReady { get; }
        public abstract Boolean ReadReady { get; }
        public abstract Boolean WriteReady { get; }
        public abstract Boolean Test(Int16 word);
        public abstract void Command(Int16 word);
        public abstract Int16 Read();
        public abstract void Write(Int16 word);
        public abstract void Exit();
    }

    class Teletype : IO
    {
        private static TimeSpan sReaderReadDelay = new TimeSpan(0, 0, 0, 0, 20); // 50 is more accurate
        private static TimeSpan sReaderStopDelay = new TimeSpan(0, 0, 0, 0, 12);
        private static TimeSpan sKeyboardDelay = new TimeSpan(0, 0, 0, 0, 40); // 100 is more accurate
        private static TimeSpan sPrinterDelay = new TimeSpan(0, 0, 0, 0, 40); // 100 is more accurate

        private Thread mWorker;
        private NetworkStream mNetwork;
        private Stream mReader;
        private DateTime mLastRead = DateTime.MinValue;
        private Int32 mReaderBuf;
        private Int32 mReaderCount;
        private Stream mPunch;
        private DateTime mLastWrite = DateTime.MinValue;
        private Int32 mCommand = 0x0400;
        private Int32 mMode = 1;

        public Teletype()
        {
            mWorker = new Thread(new ThreadStart(WorkerThread));
            mWorker.Start();
        }

        public override Boolean CommandReady
        {
            get { return true; }
        }

        public override Boolean ReadReady
        {
            get
            {
                Boolean f = false;
                DateTime now = DateTime.Now;
                if (((mCommand & 0x0400) != 0) && (mNetwork != null))
                {
                    if (((now - mLastRead) > sKeyboardDelay) && (mNetwork.DataAvailable)) f = true;
                }
                if (((mCommand & 0x0800) != 0) && (mReader != null))
                {
                    if ((mReaderBuf == -1) && ((now - mLastRead) > sReaderStopDelay))
                    {
                        if ((mReaderBuf = mReader.ReadByte()) != -1) Console.Out.Write(((++mReaderCount % 512) == 0) ? '^' : '`');
                    }
                    if (((now - mLastRead) > sReaderReadDelay) && (mReaderBuf != -1)) f = true;
                }
                return f;
            }
        }

        public override Boolean WriteReady
        {
            get
            {
                if ((DateTime.Now - mLastWrite) < sPrinterDelay) return false;
                Boolean f = false;
                if ((mMode == 1) && (mNetwork != null)) f = true;
                if ((mMode == 2) && (mPunch != null)) f = true;
                if ((mMode == 3) && (mNetwork != null) && (mPunch != null)) f = true;
                return f;
            }
        }

        public Int32 Mode
        {
            get { return mMode; }
            set { mMode = value; }
        }

        public override Boolean Test(Int16 word)
        {
            return true;
        }

        public override void Command(Int16 word)
        {
            mCommand = word;
            if ((mCommand & 0x0800) == 0) mReaderBuf = -1;
            // TODO: drop a keyboard char if disabling keyboard
        }

        public override Int16 Read()
        {
            mLastRead = DateTime.Now;
            if (((mCommand & 0x0800) != 0) && (mReaderBuf != -1))
            {
                Int16 rc = (Int16)(mReaderBuf & 0xff);
                mReaderBuf = -1;
                return rc;
            }
            return (Int16)(mNetwork.ReadByte());
        }

        public override void Write(Int16 word)
        {
            mLastWrite = DateTime.Now;
            Byte b = (Byte)((word >> 8) & 0xff);
            if ((mMode & 1) != 0) mNetwork.WriteByte(b);
            if ((mMode & 2) != 0) mPunch.WriteByte(b);
        }

        public override void Exit()
        {
            if (mPunch != null) mPunch.Close();
            if (mReader != null) mReader.Close();
            if (mNetwork != null) mNetwork.Close();
            mWorker.Abort();
        }

        public void SetReader(String inputFile)
        {
            if ((inputFile == null) || (inputFile.Length == 0))
            {
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
                mPunch = null;
                Console.Out.Write("[-PUN]");
                return;
            }
            mPunch = new FileStream(outputFile, FileMode.Append, FileAccess.Write);
            Console.Out.Write("[+PUN]");
        }

        private void WorkerThread()
        {
            TcpListener L = new TcpListener(8101);
            L.Start();
            while (true)
            {
                TcpClient C = L.AcceptTcpClient();
                mNetwork = C.GetStream();
                Console.Out.Write("[+TTY]");
                while (C.Connected) Thread.Sleep(100);
                C.Close();
                Console.Out.Write("[-TTY]");
                mNetwork = null;
            }
        }
    }
}
