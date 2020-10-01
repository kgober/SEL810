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


// Future Improvements:
// allow writing end tape marker BA BA BA 
// READ, DATA, and RESTORE statements
// DEF statement
// COM statement
// CALL and WAIT statements
// MAT statement and MAT functions ZER, CON, IDN, TRN, INV
// op 0x2200, 0x2800-0x2e00, 0x6a00-

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace MakeBASIC
{
    class Program
    {
        static List<List<Int32>> PRG = new List<List<Int32>>();

        static Int32 Main(String[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("MakeBASIC - convert standard input to SEL810 BASIC words");
                Console.Error.WriteLine("Usage: MakeBASIC outputfile");
                return 2;
            }

            List<Int32> BUF = new List<Int32>();
            Int32 line = 0;
            Boolean end = false;
            String cmd;
            while ((cmd = Console.In.ReadLine()) != null)
            {
                // ignore blank lines and comment lines
                if ((cmd.Length == 0) || (cmd[0] == '#')) continue;

                String arg = String.Empty;
                Int32 p = cmd.IndexOf(' ');
                if (p != -1)
                {
                    arg = cmd.Substring(p + 1);
                    cmd = cmd.Substring(0, p);
                }
                Int32 op = 0;
                Int32 val = 0;
                Double num;
                switch (cmd.ToUpper())
                {
                    // "LINE" begins a new program line.
                    case "LINE": // arg = line number
                        if (!Int32.TryParse(arg, out p))
                        {
                            Console.Error.WriteLine("Error: invalid line number {0}", arg);
                            break;
                        }
                        else if ((p < 0) || (p > 32767))
                        {
                            Console.Error.WriteLine("Error: line number {0:D0} out of range (0-32767)", p);
                            break;
                        }
                        AddLine(BUF);
                        line = p;
                        BUF = new List<Int32>();
                        BUF.Add(line);
                        BUF.Add(0);
                        break;

                    // "LOAD" reads a SEL810 BASIC program tape into memory where it may be edited.
                    case "LOAD": // arg = file name
                        LoadFile(arg);
                        break;

                    // "ENDVAL" adds a token to a statement that tells the interpreter when an
                    // expression has ended. It does not appear to be needed before "STEP" or "]".
                    case "ENDVAL": // no arg
                        op = 0x0000;
                        BUF.Add(op);
                        break;

                    // "STR" adds a string literal (aka "label") to a "PRINT" statement. The string
                    // must be terminated using "ENDSTR", "ENDSTRF", or "ENDSTRN".
                    case "STR": // arg = string
                        op = 0x0200;
                        p = 0;
                        while (p < arg.Length)
                        {
                            val = arg[p++] | 128;
                            BUF.Add(op | val);
                            op = 0;
                            if (p == arg.Length) break;
                            op = (arg[p++] | 128) << 8;
                        }
                        if (op != 0) BUF.Add(op);
                        break;

                    // "ENDSTR" terminates a string literal.  A variable name may follow.
                    case "ENDSTR": // arg = variable name (optional)
                        op = 0x0200;
                        val = GetVar(arg);
                        BUF.Add(op | val);
                        break;

                    // "ENDSTRF" terminates a string literal that is followed by a function name.
                    case "ENDSTRF": // arg = function name
                        op = 0x0200;
                        val = GetFunc(arg);
                        if (val == 0)
                        {
                            Console.Error.Write("Error: function name required");
                            break;
                        }
                        op |= 0x8000;
                        BUF.Add(op | val);
                        break;

                    // "ENDSTRN" terminates a string literal that is followed by a numeric value.
                    case "ENDSTRN": // arg = numeric value
                        op = 0x0200;
                        if (!Double.TryParse(arg, out num))
                        {
                            Console.Error.WriteLine("Error: numeric value required");
                            break;
                        }
                        op |= 0x8000;
                        val = 0;
                        BUF.Add(op | val);
                        AddNum(BUF, num);
                        break;

                    // "APPEND," and "APPEND;" add a comma or semicolon to a "PRINT" statement.
                    // A variable name may follow.
                    case "APPEND,": // arg = variable name (optional)
                    case "APPEND;":
                        switch (cmd[6])
                        {
                            case ',': op = 0x0400; break;
                            case ';': op = 0x0600; break;
                        }
                        val = GetVar(arg);
                        BUF.Add(op | val);
                        break;

                    // "APPEND,F" and "APPEND;F" add a comma or semicolon that is followed by
                    // a function name to a "PRINT" statement.
                    case "APPEND,F": // arg = variable name (optional)
                    case "APPEND;F":
                        switch (cmd[6])
                        {
                            case ',': op = 0x0400; break;
                            case ';': op = 0x0600; break;
                        }
                        op |= 0x8000;
                        val = GetFunc(arg);
                        if (val == 0)
                        {
                            Console.Error.Write("Error: function name required");
                            break;
                        }
                        BUF.Add(op | val);
                        break;

                    // "APPEND,N" and "APPEND;N" add a comma or semicolon that is followed by
                    // a numeric value to a "PRINT" statement.
                    case "APPEND,N": // arg = variable name (optional)
                    case "APPEND;N":
                        switch (cmd[6])
                        {
                            case ',': op = 0x0400; break;
                            case ';': op = 0x0600; break;
                        }
                        if (!Double.TryParse(arg, out num))
                        {
                            Console.Error.WriteLine("Error: numeric value required");
                            break;
                        }
                        op |= 0x8000;
                        val = 0;
                        BUF.Add(op | val);
                        AddNum(BUF, num);
                        break;

                    // ")" closes an expression opened by a matching "(".
                    case ")": // no arg
                        op = 0x0800;
                        BUF.Add(op);
                        break;

                    // "]" closes an array index or size opened by a matching "[".
                    case "]": // no arg
                        op = 0x0a00;
                        BUF.Add(op);
                        break;

                    // operators that may be followed by a variable name.
                    case ",": // arg = variable name (optional)
                    case "=":
                    case "+":
                    case "-":
                    case "*":
                    case "/":
                    case "^":
                    case ">":
                    case "<":
                    case "[":
                    case "(":
                        switch (cmd[0])
                        {
                            case ',': op = 0x0c00; break;
                            case '=': op = 0x0e00; break;
                            case '+': op = 0x1000; break;
                            case '-': op = 0x1200; break;
                            case '*': op = 0x1400; break;
                            case '/': op = 0x1600; break;
                            case '^': op = 0x1800; break;
                            case '>': op = 0x1a00; break;
                            case '<': op = 0x1c00; break;
                            case '[': op = 0x2400; break;
                            case '(': op = 0x2600; break;
                        }
                        val = GetVar(arg);
                        BUF.Add(op | val);
                        break;

                    // operators that are followed by a function name.
                    case ",F": // arg = function name
                    case "=F":
                    case "+F":
                    case "-F":
                    case "*F":
                    case "/F":
                    case "^F":
                    case ">F":
                    case "<F":
                    case "[F":
                    case "(F":
                        switch (cmd[0])
                        {
                            case ',': op = 0x0c00; break;
                            case '=': op = 0x0e00; break;
                            case '+': op = 0x1000; break;
                            case '-': op = 0x1200; break;
                            case '*': op = 0x1400; break;
                            case '/': op = 0x1600; break;
                            case '^': op = 0x1800; break;
                            case '>': op = 0x1a00; break;
                            case '<': op = 0x1c00; break;
                            case '[': op = 0x2400; break;
                            case '(': op = 0x2600; break;
                        }
                        op |= 0x8000;
                        val = GetFunc(arg);
                        if (val == 0)
                        {
                            Console.Error.WriteLine("Error: function name required");
                            break;
                        }
                        BUF.Add(op | val);
                        break;

                    // operators that are followed by a numeric value.
                    case ",N": // arg = numeric value
                    case "=N":
                    case "+N":
                    case "-N":
                    case "*N":
                    case "/N":
                    case "^N":
                    case ">N":
                    case "<N":
                    case "[N":
                    case "(N":
                        switch (cmd[0])
                        {
                            case ',': op = 0x0c00; break;
                            case '=': op = 0x0e00; break;
                            case '+': op = 0x1000; break;
                            case '-': op = 0x1200; break;
                            case '*': op = 0x1400; break;
                            case '/': op = 0x1600; break;
                            case '^': op = 0x1800; break;
                            case '>': op = 0x1a00; break;
                            case '<': op = 0x1c00; break;
                            case '[': op = 0x2400; break;
                            case '(': op = 0x2600; break;
                        }
                        if (!Double.TryParse(arg, out num))
                        {
                            Console.Error.WriteLine("Error: numeric value required");
                            break;
                        }
                        op |= 0x8000;
                        val = 0;
                        BUF.Add(op | val);
                        AddNum(BUF, num);
                        break;

                    // operators that are followed by a word value.
                    case ",W": // arg = word value
                    case "[W":
                        switch (cmd[0])
                        {
                            case ',': op = 0x0c00; break;
                            case '[': op = 0x2400; break;
                        }
                        if (!Int32.TryParse(arg, out p))
                        {
                            Console.Error.WriteLine("Error: invalid word value {0}", arg);
                            break;
                        }
                        else if ((p < 0) || (p > 32767))
                        {
                            Console.Error.WriteLine("Error: word value {0:D0} out of range (0-32767)", p);
                            break;
                        }
                        op |= 0x8000;
                        val = 3;
                        BUF.Add(op | val);
                        BUF.Add(p);
                        break;

                    // 2-character relational operators that may be followed by a variable name.
                    case "!=": // arg = variable name (optional)
                    case "==":
                    case ">=":
                    case "<=":
                        switch (cmd[0])
                        {
                            case '!': op = 0x1e00; break;
                            case '=': op = 0x2000; break;
                            case '>': op = 0x3000; break;
                            case '<': op = 0x3200; break;
                        }
                        val = GetVar(arg);
                        BUF.Add(op | val);
                        break;

                    // 2-character relational operators that are followed by a function name.
                    case "!=F": // arg = variable name
                    case "==F":
                    case ">=F":
                    case "<=F":
                        switch (cmd[0])
                        {
                            case '!': op = 0x1e00; break;
                            case '=': op = 0x2000; break;
                            case '>': op = 0x3000; break;
                            case '<': op = 0x3200; break;
                        }
                        op |= 0x8000;
                        val = GetFunc(arg);
                        if (val == 0)
                        {
                            Console.Error.WriteLine("Error: function name required");
                            break;
                        }
                        BUF.Add(op | val);
                        break;

                    // 2-character relational operators that are followed by a numeric value.
                    case "!=N": // arg = variable name
                    case "==N":
                    case ">=N":
                    case "<=N":
                        switch (cmd[0])
                        {
                            case '!': op = 0x1e00; break;
                            case '=': op = 0x2000; break;
                            case '>': op = 0x3000; break;
                            case '<': op = 0x3200; break;
                        }
                        if (!Double.TryParse(arg, out num))
                        {
                            Console.Error.WriteLine("Error: numeric value required");
                            break;
                        }
                        op |= 0x8000;
                        val = 0;
                        BUF.Add(op | val);
                        AddNum(BUF, num);
                        break;

                    // "LET" statement.
                    case "LET": // arg = variable name
                        op = 0x3400;
                        if (BUF.Count != 2)
                        {
                            Console.Error.WriteLine("Error: statement must appear at start of line");
                            break;
                        }
                        val = GetVar(arg);
                        if (val == 0)
                        {
                            Console.Error.WriteLine("Error: variable name required");
                            break;
                        }
                        BUF.Add(op | val);
                        break;

                    // "DIM" statement.
                    case "DIM": // arg = array variable name
                        op = 0x3600;
                        if (BUF.Count != 2)
                        {
                            Console.Error.WriteLine("Error: statement must appear at start of line");
                            break;
                        }
                        val = GetVar(arg);
                        if (((val & 15) != 1) && ((val & 15) != 2))
                        {
                            Console.Error.WriteLine("Error: array variable name required");
                            break;
                        }
                        BUF.Add(op | val);
                        break;

                    // "REM" statement.
                    case "REM": // arg = string
                        op = 0x3c00;
                        if (BUF.Count != 2)
                        {
                            Console.Error.WriteLine("Error: statement must appear at start of line");
                            break;
                        }
                        p = 0;
                        while (p < arg.Length)
                        {
                            val = arg[p++] | 128;
                            BUF.Add(op | val);
                            op = 0;
                            if (p == arg.Length) break;
                            op = (arg[p++] | 128) << 8;
                        }
                        if (op != 0) BUF.Add(op);
                        break;

                    // "GOTO" statement.
                    case "GOTO": // arg = line number
                        op = 0x3e00;
                        if (BUF.Count != 2)
                        {
                            Console.Error.WriteLine("Error: statement must appear at start of line");
                            break;
                        }
                        if (!Int32.TryParse(arg, out p))
                        {
                            Console.Error.WriteLine("Error: invalid line number {0}", arg);
                            break;
                        }
                        else if ((p < 0) || (p > 32767))
                        {
                            Console.Error.WriteLine("Error: line number {0:D0} out of range (0-32767)", p);
                            break;
                        }
                        op |= 0x8000;
                        val = 3;
                        BUF.Add(op | val);
                        BUF.Add(p);
                        break;

                    // "IF" statement followed by a variable name
                    case "IF": // arg = variable name
                        op = 0x4000;
                        if (BUF.Count != 2)
                        {
                            Console.Error.WriteLine("Error: statement must appear at start of line");
                            break;
                        }
                        val = GetVar(arg);
                        if (val == 0)
                        {
                            Console.Error.WriteLine("Error: variable name required");
                            break;
                        }
                        BUF.Add(op | val);
                        break;

                    // "IF" statement followed by a function name
                    case "IFF": // arg = function name
                        op = 0x4000;
                        if (BUF.Count != 2)
                        {
                            Console.Error.WriteLine("Error: statement must appear at start of line");
                            break;
                        }
                        op |= 0x8000;
                        val = GetFunc(arg);
                        if (val == 0)
                        {
                            Console.Error.WriteLine("Error: function name required");
                            break;
                        }
                        BUF.Add(op | val);
                        break;

                    // "IF" statement followed by a numeric value
                    case "IFN": // arg = numeric value
                        op = 0x4000;
                        if (BUF.Count != 2)
                        {
                            Console.Error.WriteLine("Error: statement must appear at start of line");
                            break;
                        }
                        if (!Double.TryParse(arg, out num))
                        {
                            Console.Error.WriteLine("Error: numeric value required");
                            break;
                        }
                        op |= 0x8000;
                        val = 0;
                        BUF.Add(op | val);
                        AddNum(BUF, num);
                        break;

                    // "FOR" statement.
                    case "FOR": // arg = variable name
                        op = 0x4200;
                        if (BUF.Count != 2)
                        {
                            Console.Error.WriteLine("Error: statement must appear at start of line");
                            break;
                        }
                        val = GetVar(arg);
                        if (val == 0)
                        {
                            Console.Error.WriteLine("Error: variable name required");
                            break;
                        }
                        else if (((val & 15) == 1) || ((val & 15) == 2))
                        {
                            Console.Error.WriteLine("Error: non-array variable name required");
                            break;
                        }
                        BUF.Add(op | val);
                        break;

                    // "NEXT" statement.
                    case "NEXT": // arg = variable name
                        op = 0x4400;
                        if (BUF.Count != 2)
                        {
                            Console.Error.WriteLine("Error: statement must appear at start of line");
                            break;
                        }
                        val = GetVar(arg);
                        if (val == 0)
                        {
                            Console.Error.WriteLine("Error: variable name required");
                            break;
                        }
                        else if (((val & 15) == 1) || ((val & 15) == 2))
                        {
                            Console.Error.WriteLine("Error: non-array variable name required");
                            break;
                        }
                        BUF.Add(op | val);
                        break;

                    // "GOSUB" statement.
                    case "GOSUB": // arg = line number
                        op = 0x4600;
                        if (BUF.Count != 2)
                        {
                            Console.Error.WriteLine("Error: statement must appear at start of line");
                            break;
                        }
                        if (!Int32.TryParse(arg, out p))
                        {
                            Console.Error.WriteLine("Error: invalid line number {0}", arg);
                            break;
                        }
                        else if ((p < 0) || (p > 32767))
                        {
                            Console.Error.WriteLine("Error: line number {0:D0} out of range (0-32767)", p);
                            break;
                        }
                        op |= 0x8000;
                        val = 3;
                        BUF.Add(op | val);
                        BUF.Add(p);
                        break;

                    // "RETURN" statement.
                    case "RETURN": // no arg
                        op = 0x4800;
                        if (BUF.Count != 2)
                        {
                            Console.Error.WriteLine("Error: statement must appear at start of line");
                            break;
                        }
                        BUF.Add(op | val);
                        break;

                    // "END" statement.
                    case "END": // no arg
                        op = 0x4a00;
                        if (BUF.Count != 2)
                        {
                            Console.Error.WriteLine("Error: statement must appear at start of line");
                            break;
                        }
                        BUF.Add(op | val);
                        break;

                    // "PRINT" statement.  A variable name may follow.
                    case "PRINT": // arg = variable name (optional)
                        op = 0x5600;
                        if (BUF.Count != 2)
                        {
                            Console.Error.WriteLine("Error: statement must appear at start of line");
                            break;
                        }
                        val = GetVar(arg);
                        BUF.Add(op | val);
                        break;

                    // "PRINT" statement followed by a function name.
                    case "PRINTF": // arg = function name
                        op = 0x5600;
                        if (BUF.Count != 2)
                        {
                            Console.Error.WriteLine("Error: statement must appear at start of line");
                            break;
                        }
                        op |= 0x8000;
                        val = GetFunc(arg);
                        if (val == 0)
                        {
                            Console.Error.WriteLine("Error: function name required");
                            break;
                        }
                        BUF.Add(op | val);
                        break;

                    // "PRINT" statement followed by a numeric value.
                    case "PRINTN": // arg = numeric value
                        op = 0x5600;
                        if (BUF.Count != 2)
                        {
                            Console.Error.WriteLine("Error: statement must appear at start of line");
                            break;
                        }
                        if (!Double.TryParse(arg, out num))
                        {
                            Console.Error.WriteLine("Error: numeric value required");
                            break;
                        }
                        op |= 0x8000;
                        val = 0;
                        BUF.Add(op | val);
                        AddNum(BUF, num);
                        break;

                    // "INPUT" statement.
                    case "INPUT": // arg = variable name
                        op = 0x5800;
                        if (BUF.Count != 2)
                        {
                            Console.Error.WriteLine("Error: statement must appear at start of line");
                            break;
                        }
                        val = GetVar(arg);
                        if (val == 0)
                        {
                            Console.Error.WriteLine("Error: variable name required");
                            break;
                        }
                        BUF.Add(op | val);
                        break;

                    // "THEN" clause of an "IF" statement.
                    case "THEN": // arg = line number
                        op = 0x5e00;
                        if ((BUF.Count == 2) || ((BUF[2] & 0x7e00) != 0x4000))
                        {
                            Console.Error.WriteLine("Error: IF statement required");
                            break;
                        }
                        if (!Int32.TryParse(arg, out p))
                        {
                            Console.Error.WriteLine("Error: invalid line number {0}", arg);
                            break;
                        }
                        else if ((p < 0) || (p > 32767))
                        {
                            Console.Error.WriteLine("Error: line number {0:D0} out of range (0-32767)", p);
                            break;
                        }
                        op |= 0x8000;
                        val = 3;
                        BUF.Add(op | val);
                        BUF.Add(p);
                        break;

                    // "TO" clause of a "FOR" statement, starting with a variable name
                    // or a parenthesized expression
                    case "TO": // arg = variable name (optional)
                        op = 0x6000;
                        if ((BUF.Count == 2) || ((BUF[2] & 0x7e00) != 0x4200))
                        {
                            Console.Error.WriteLine("Error: FOR statement required");
                            break;
                        }
                        val = GetVar(arg);
                        BUF.Add(op | val);
                        break;

                    // "TO" clause of a "FOR" statement, starting with a function name.
                    case "TOF": // arg = function name
                        op = 0x6000;
                        if ((BUF.Count == 2) || ((BUF[2] & 0x7e00) != 0x4200))
                        {
                            Console.Error.WriteLine("Error: FOR statement required");
                            break;
                        }
                        op |= 0x8000;
                        val = GetFunc(arg);
                        if (val == 0)
                        {
                            Console.Error.WriteLine("Error: function name required");
                            break;
                        }
                        BUF.Add(op | val);
                        break;

                    // "TO" clause of a "FOR" statement, starting with a numeric value.
                    case "TON": // arg = numeric value
                        op = 0x6000;
                        if ((BUF.Count == 2) || ((BUF[2] & 0x7e00) != 0x4200))
                        {
                            Console.Error.WriteLine("Error: FOR statement required");
                            break;
                        }
                        if (!Double.TryParse(arg, out num))
                        {
                            Console.Error.WriteLine("Error: numeric value required");
                            break;
                        }
                        op |= 0x8000;
                        val = 0;
                        BUF.Add(op | val);
                        AddNum(BUF, num);
                        break;

                    // "STEP" clause of a "FOR" statement, starting with a variable name
                    // or a parenthesized expression.
                    case "STEP": // arg = variable name (optional)
                        op = 0x6200;
                        if ((BUF.Count == 2) || ((BUF[2] & 0x7e00) != 0x4200))
                        {
                            Console.Error.WriteLine("Error: FOR statement required");
                            break;
                        }
                        val = GetVar(arg);
                        BUF.Add(op | val);
                        break;

                    // "STEP" clause of a "FOR" statement, starting with a function name.
                    case "STEPF": // arg = function name
                        op = 0x6200;
                        if ((BUF.Count == 2) || ((BUF[2] & 0x7e00) != 0x4200))
                        {
                            Console.Error.WriteLine("Error: FOR statement required");
                            break;
                        }
                        op |= 0x8000;
                        val = GetFunc(arg);
                        if (val == 0)
                        {
                            Console.Error.WriteLine("Error: function name required");
                            break;
                        }
                        BUF.Add(op | val);
                        break;

                    // "STEP" clause of a "FOR" statement, starting with a numeric value.
                    case "STEPN": // arg = numeric value
                        op = 0x6200;
                        if ((BUF.Count == 2) || ((BUF[2] & 0x7e00) != 0x4200))
                        {
                            Console.Error.WriteLine("Error: FOR statement required");
                            break;
                        }
                        if (!Double.TryParse(arg, out num))
                        {
                            Console.Error.WriteLine("Error: numeric value required");
                            break;
                        }
                        op |= 0x8000;
                        val = 0;
                        BUF.Add(op | val);
                        AddNum(BUF, num);
                        break;

                    // "DEC" adds an arbitrary decimal word to the current line.  Use with care.
                    case "DEC": // arg = decimal word value
                        if (!Int32.TryParse(arg, out p))
                        {
                            Console.Error.WriteLine("Error: invalid decimal value {0}", arg);
                            break;
                        }
                        else if ((p < -32768) || (p > 65535))
                        {
                            Console.Error.WriteLine("Error: value {0:D0} will not fit in a 16-bit word", p);
                            break;
                        }
                        BUF.Add(p);
                        break;

                    // "HEX" adds an arbitrary hexadecimal word to the current line.  Use with care.
                    case "HEX": // arg = hexadecimal word value
                        if (!Int32.TryParse(arg, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out p))
                        {
                            Console.Error.WriteLine("Error: invalid hex value {0}", arg);
                            break;
                        }
                        else if ((p < -32768) || (p > 65535))
                        {
                            Console.Error.WriteLine("Error: value {0:X4} will not fit in a 16-bit word", p);
                            break;
                        }
                        BUF.Add(p);
                        break;

                    // "MAKE" ends input, like EOF.
                    case "MAKE": // no arg
                        end = true;
                        break;

                    default:
                        Console.Error.WriteLine("Error: unrecognized command {0}", cmd);
                        break;
                }
                if (end) break;
            }
            AddLine(BUF);
            Console.Error.WriteLine("Program lines: {0:D0}", PRG.Count);

            // write tape leader
            Console.Error.WriteLine("Writing 127 leader bytes...");
            FileStream OUT = new FileStream(args[0], FileMode.Create);
            for (Int32 i = 0; i < 127; i++) OUT.WriteByte(0);

            // write header
            Console.Error.WriteLine("Writing header...");
            OUT.WriteByte(0xff);
            Int32 n = 0;
            for (Int32 i = 0; i < PRG.Count; i++) n += PRG[i].Count;
            n = -n;
            OUT.WriteByte((Byte)((n >> 8) & 255));
            OUT.WriteByte((Byte)(n & 255));

            // write program text
            Console.Error.WriteLine("Writing {0:D0} words of program text...", -n);
            Int32 sum = 0;
            for (Int32 i = 0; i < PRG.Count; i++)
            {
                BUF = PRG[i];
                for (Int32 j = 0; j < BUF.Count; j++)
                {
                    n = BUF[j];
                    sum += n;
                    OUT.WriteByte((Byte)((n >> 8) & 255));
                    OUT.WriteByte((Byte)(n & 255));
                }
            }

            // write checksum
            n = (0x10000 - (sum & 0xffff)) & 0xffff;
            Console.Error.WriteLine("Checksum: {0:x4}", n);
            OUT.WriteByte((Byte)((n >> 8) & 255));
            OUT.WriteByte((Byte)(n & 255));

            // write tape trailer
            Console.Error.WriteLine("Writing 128 trailer bytes...");
            for (Int32 i = 0; i < 128; i++) OUT.WriteByte(0);
            OUT.Close();

            return 0;
        }

        // a variable reference is a 9-bit value llllldddd.
        // lllll designates the first character ('A'=1, 'B'=2, etc.)
        // for 2-character variable names, dddd indicates the digit ('0'=5, '1'=6, etc.)
        // for 1-character variable names, dddd indicates type:
        //   1 - single-dimensional array (vector)
        //   2 - two-dimensional array (matrix)
        //   4 - regular variable
        static public Int32 GetVar(String arg)
        {
            if ((arg == null) || (arg.Length == 0) || (arg.Length > 2)) return 0;
            Char letter = arg[0];
            if (letter >= 96) letter -= ' '; // make uppercase
            if ((letter < 'A') || (letter > 'Z')) return 0;
            Int32 retval = (letter - '@') * 16;
            if (arg.Length == 1) return retval + 4;
            Char digit = arg[1];
            if (digit == '.') return retval + 1;
            if (digit == ':') return retval + 2;
            if ((digit >= '0') && (digit <= '9')) return retval + (digit - '0' + 5);
            return 0;
        }

        // a function reference is a 9-bit value fffff1111.
        // fffff designates the function name.
        static public Int32 GetFunc(String arg)
        {
            if ((arg == null) || (arg.Length == 0)) return 0;
            switch (arg.ToUpper())
            {
                case "TAB": return 0x01f;
                case "SIN": return 0x02f;
                case "COS": return 0x03f;
                case "TAN": return 0x04f;
                case "ATN": return 0x05f;
                case "EXP": return 0x06f;
                case "LOG": return 0x07f;
                case "ABS": return 0x08f;
                case "SQR": return 0x09f;
                case "INT": return 0x0af;
                case "RND": return 0x0bf;
                case "SGN": return 0x0cf;
                case "ZER": return 0x0df;
                case "CON": return 0x0ef;
                case "IDN": return 0x0ff;
                case "INV": return 0x10f;
                case "TRN": return 0x11f;
            }
            return 0;
        }

        static public void AddNum(List<Int32> lineBuf, Double number)
        {
            if (number == 0.0)
            {
                lineBuf.Add(0);
                lineBuf.Add(0);
                return;
            }

            // extract fields from IEEE double
            Int64 qword = BitConverter.DoubleToInt64Bits(number);
            Int32 sign = (Int32)((qword >> 63) & 1); // sign (1 bit)
            Int32 exp = ((Int32)((qword >> 52) & 2047)) - 1023; // exponent (11 bits)
            Int32 frac = (Int32)((qword >> 21) & 0x7fffffff); // exponent (use 31 of 52 bits)

            // IEEE format: (sign) 1.frac * 2^exp (sign/magnitude frac)
            // SEL810 format: (sign) 0.frac * 2^exp (2's complement frac)
            frac = (frac >> 1) | 0x40000000; // make implicit 1 explicit
            exp++; // adjust exp
            if (sign == 1) frac = -frac; // adjust frac if negative

            // construct SEL810 words
            Int32 w1 = (frac >> 16) & 0xffff; // sign bit and 15 frac bits
            Int32 w2 = (frac & 0xfc00) >> 1; // next 6 frac bits
            w2 |= (exp & 0x01ff); // exponent

            lineBuf.Add(w1);
            lineBuf.Add(w2);
        }

        static public void AddLine(List<Int32> lineBuf)
        {
            if ((lineBuf == null) || (lineBuf.Count == 0)) return;
            lineBuf[1] = lineBuf.Count;
            Int32 q = 0;
            while ((q < PRG.Count) && (PRG[q][0] < lineBuf[0])) q++;
            if (q == PRG.Count) // line is appended at end
            {
                PRG.Add(lineBuf);
            }
            else if (PRG[q][0] != lineBuf[0]) // line is inserted at q
            {
                PRG.Insert(q, lineBuf);
            }
            else if (lineBuf.Count <= 2) // line at q is removed
            {
                PRG.RemoveAt(q);
            }
            else // line at q is replaced
            {
                PRG[q] = lineBuf;
            }
        }

        static public void LoadFile(String arg)
        {
            Console.Error.WriteLine("Reading {0}...", arg);
            Byte[] tape = File.ReadAllBytes(arg);
            Int32 p = 0;

            // leader
            while (tape[p] == 0) p++;

            // program size
            Int32 n = 0xff;
            for (Int32 i = 0; i < 3; i++)
            {
                n <<= 8;
                n |= tape[p++];
            }
            n = -n;

            // program text
            UInt16[] text = new UInt16[n];
            Int32 sum = 0;
            for (Int32 i = 0; i < n; i++)
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

            PRG = new List<List<Int32>>();
            p = 0;
            while (p < n)
            {
                List<Int32> BUF = new List<Int32>();
                for (Int32 i = 0; i < text[p + 1]; i++) BUF.Add(text[p + i]);
                PRG.Add(BUF);
                p += text[p + 1];
            }
        }
    }
}
