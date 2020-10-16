// JSON.cs
// Copyright © 2013,2014,2016,2017,2020 Kenneth Gober
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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace JSON
{
    public enum Type
    {
        Null,
        Boolean,
        Number,
        String,
        Array,
        Object
    }

    public struct Value : IEquatable<Value>, IEnumerable<KeyValuePair<String, Value>>
    {
        private Type mType;
        private Object mValue;
        private Int32 mArrayLength;

        internal Value(Type kind)
        {
            mType = kind;
            if (mType == Type.Array)
                mValue = new Dictionary<Int32, Value>();
            else if (mType == Type.Object)
                mValue = new Dictionary<String, Value>();
            else
                mValue = null;
            mArrayLength = 0;
        }

        internal Value(Boolean value)
        {
            mType = Type.Boolean;
            mValue = value;
            mArrayLength = 0;
        }

        internal Value(Double value)
        {
            mType = Type.Number;
            mValue = value;
            mArrayLength = 0;
        }

        internal Value(String value)
        {
            mType = (value == null) ? Type.Null : Type.String;
            mValue = value;
            mArrayLength = 0;
        }

        public Value this[Int32 index]
        {
            get
            {
                if (mType != Type.Array) throw new InvalidOperationException("Int32 indexer may only be used with Array values");
                Dictionary<Int32, Value> dict = mValue as Dictionary<Int32, Value>;
                if (!dict.ContainsKey(index)) return null;
                return dict[index];
            }
            set
            {
                if (mType != Type.Array) throw new InvalidOperationException("Int32 indexer may only be used with Array values");
                Dictionary<Int32, Value> dict = mValue as Dictionary<Int32, Value>;
                dict[index] = value;
                if (index >= mArrayLength) mArrayLength = index + 1;
            }
        }

        public Value this[String index]
        {
            get
            {
                if (mType != Type.Object) throw new InvalidOperationException("String indexer may only be used with Object values");
                Dictionary<String, Value> dict = mValue as Dictionary<String, Value>;
                if (!dict.ContainsKey(index)) return null;
                return dict[index];
            }
            set
            {
                if (mType != Type.Object) throw new InvalidOperationException("String indexer may only be used with Object values");
                Dictionary<String, Value> dict = mValue as Dictionary<String, Value>;
                dict[index] = value;
            }
        }

        public Object AsObject
        {
            get { return mValue; }
        }

        public Boolean AsBoolean
        {
            get
            {
                if (mType != Type.Boolean) throw new InvalidOperationException("Value is not a Boolean");
                return (Boolean)mValue;
            }
        }

        public Int32 AsInt32
        {
            get
            {
                if (mType == Type.Number) return (Int32)((Double)mValue);
                if (mType == Type.String)
                {
                    Int32 retval;
                    if (Int32.TryParse(mValue as String, out retval)) return retval;
                }
                throw new InvalidOperationException("Value is not a Number");
            }
        }

        public Double AsDouble
        {
            get
            {
                if (mType != Type.Number) throw new InvalidOperationException("Value is not a Number");
                return (Double)mValue;
            }
        }

        public String AsString
        {
            get
            {
                if (mType == Type.Null) return null;
                if (mType != Type.String) throw new InvalidOperationException("Value is not a String");
                return mValue as String;
            }
        }

        public Dictionary<Int32, Value>.ValueCollection AsValueCollection
        {
            get
            {
                if (mType == Type.Null) return null;
                if (mType != Type.Array) throw new InvalidOperationException("Value is not an Array");
                return (mValue as Dictionary<Int32, Value>).Values;
            }
        }

        public Type Type
        {
            get { return mType; }
        }

        public Int32 Count
        {
            get
            {
                if (mType == Type.Array)
                {
                    return mArrayLength;
                }
                if (mType == Type.Object)
                {
                    Dictionary<String, Value> dict = mValue as Dictionary<String, Value>;
                    return dict.Count;
                }
                return -1;
            }
        }

        public override String ToString()
        {
            switch (mType)
            {
                case Type.Null:
                    return null;
                case Type.Boolean:
                    return AsBoolean.ToString();
                case Type.Number:
                    return AsDouble.ToString();
                case Type.String:
                    return AsString;
                case Type.Array:
                case Type.Object:
                    StringWriter buf = new StringWriter();
                    WriteTo(buf);
                    return buf.ToString();
                default:
                    throw new InvalidOperationException();
            }
        }

        public Boolean ContainsKey(String index)
        {
            if (mType != Type.Object) throw new InvalidOperationException("Method may only be used with Object values");
            Dictionary<String, Value> dict = mValue as Dictionary<String, Value>;
            return dict.ContainsKey(index);
        }

        public static implicit operator Value(Boolean value)
        {
            return new Value(value);
        }

        public static implicit operator Value(Double value)
        {
            return new Value(value);
        }

        public static implicit operator Value(String value)
        {
            return new Value(value);
        }

        public static Boolean operator ==(Value value1, Value value2)
        {
            return ((IEquatable<Value>)value1).Equals(value2);
        }

        public static Boolean operator !=(Value value1, Value value2)
        {
            return !((IEquatable<Value>)value1).Equals(value2);
        }

        Boolean IEquatable<Value>.Equals(Value other)
        {
            switch (this.mType)
            {
                case Type.Null:
                    return (other.mType == Type.Null);
                case Type.Boolean:
                    return (other.mType == Type.Boolean) && (this.AsBoolean == other.AsBoolean);
                case Type.Number:
                    return (other.mType == Type.Number) && (this.AsDouble == other.AsDouble);
                case Type.String:
                    return (other.mType == Type.String) && (this.AsString == other.AsString);
                case Type.Array:
                    if ((other.mType != Type.Array) || (this.mArrayLength != other.mArrayLength)) return false;
                    Dictionary<Int32, Value> thisArray = this.mValue as Dictionary<Int32, Value>;
                    Dictionary<Int32, Value> otherArray = other.mValue as Dictionary<Int32, Value>;
                    for (Int32 i = 0; i < this.mArrayLength; i++)
                    {
                        if (thisArray.ContainsKey(i))
                        {
                            if (!otherArray.ContainsKey(i)) return false;
                            if (thisArray[i] != otherArray[i]) return false;
                        }
                        else
                        {
                            if (otherArray.ContainsKey(i)) return false;
                        }
                    }
                    return true;
                case Type.Object:
                    if (other.mType != Type.Object) return false;
                    Dictionary<String, Value> thisObject = this.mValue as Dictionary<String, Value>;
                    Dictionary<String, Value> otherObject = other.mValue as Dictionary<String, Value>;
                    foreach (KeyValuePair<String, Value> thisEntry in thisObject)
                    {
                        if (!otherObject.ContainsKey(thisEntry.Key)) return false;
                        if (thisEntry.Value != otherObject[thisEntry.Key]) return false;
                    }
                    foreach (KeyValuePair<String, Value> otherEntry in otherObject)
                    {
                        if (!thisObject.ContainsKey(otherEntry.Key)) return false;
                    }
                    return true;
                default:
                    throw new InvalidOperationException("Unknown value types cannot be equated");
            }
        }

        IEnumerator<KeyValuePair<String, Value>> IEnumerable<KeyValuePair<String, Value>>.GetEnumerator()
        {
            if (mType == Type.Object)
            {
                Dictionary<String, Value> dict = mValue as Dictionary<String, Value>;
                return dict.GetEnumerator();
            }
            throw new InvalidOperationException("Only Object values may be enumerated");
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            if (mType == Type.Object)
            {
                Dictionary<String, Value> dict = mValue as Dictionary<String, Value>;
                return dict.GetEnumerator();
            }
            if (mType == Type.Array)
            {
                Value[] array = mValue as Value[];
                return array.GetEnumerator();
            }
            throw new InvalidOperationException("Only Object or Array values may be enumerated");
        }

        public void WriteTo(TextWriter output)
        {
            WriteTo(output, 0, false);
        }

        public void WriteTo(TextWriter output, Int32 indent)
        {
            WriteTo(output, indent, false);
        }

        public void WriteTo(TextWriter output, Int32 indent, Boolean sortNames)
        {
            switch (mType)
            {
                case Type.Null:
                    output.Write("null");
                    break;
                case Type.Boolean:
                    output.Write(AsBoolean ? "true" : "false");
                    break;
                case Type.Number:
                    output.Write(AsDouble.ToString());
                    break;
                case Type.String:
                    WriteTo(output, AsString);
                    break;
                case Type.Array:
                    output.Write('[');
                    if ((Count > 1) && (indent != -1))
                    {
                        output.WriteLine();
                        indent += 2;
                    }
                    for (Int32 i = 0; i < Count; )
                    {
                        if (Count > 1) for (Int32 j = 0; j < indent; j++) output.Write(' ');
                        this[i].WriteTo(output, indent);
                        if (++i < Count) output.Write(',');
                        if ((Count > 1) && (indent != -1)) output.WriteLine();
                    }
                    if ((Count > 1) && (indent != -1))
                    {
                        indent -= 2;
                        for (Int32 j = 0; j < indent; j++) output.Write(' ');
                    }
                    output.Write(']');
                    break;
                case Type.Object:
                    output.Write('{');
                    if ((Count > 1) && (indent != -1))
                    {
                        output.WriteLine();
                        indent += 2;
                    }
                    Int32 n = 0;
                    Dictionary<String, Value> dict = mValue as Dictionary<String, Value>;
                    List<String> keys = new List<String>(dict.Keys);
                    if (sortNames) keys.Sort(StringComparer.Ordinal);
                    foreach (String key in keys)
                    {
                        Value value = dict[key];
                        if (Count > 1) for (Int32 j = 0; j < indent; j++) output.Write(' ');
                        WriteTo(output, key);
                        output.Write(':');
                        if (indent != -1) output.Write(' ');
                        value.WriteTo(output, indent);
                        if (++n < Count) output.Write(',');
                        if ((Count > 1) && (indent != -1)) output.WriteLine();
                    }
                    if ((Count > 1) && (indent != -1))
                    {
                        indent -= 2;
                        for (Int32 j = 0; j < indent; j++) output.Write(' ');
                    }
                    output.Write('}');
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }

        private static void WriteTo(TextWriter output, String value)
        {
            output.Write('"');
            foreach (Char c in value)
            {
                if (c == '"') output.Write(@"\""");
                else if (c == '\\') output.Write(@"\\");
                else if (c == 8) output.Write(@"\b");
                else if (c == 9) output.Write(@"\t");
                else if (c == 10) output.Write(@"\n");
                else if (c == 12) output.Write(@"\f");
                else if (c == 13) output.Write(@"\r");
                else if (Char.IsControl(c)) output.Write(@"\u{0:x4}", (Int32)c);
                else output.Write(c);
            }
            output.Write('"');
        }

        public static Value ReadFrom(TextReader input)
        {
            Tokenizer L = new Tokenizer(input);
            return ReadValue(L);
        }

        public static Value ReadFrom(String input)
        {
            return ReadFrom(new StringReader(input));
        }

        private static Value ReadValue(Tokenizer input)
        {
            Token t = input.ReadToken();
            if (t.Type == TokenType.String)
            {
                return new Value(t.Value);
            }
            else if (t.Type == TokenType.Number)
            {
                return new Value(Double.Parse(t.Value));
            }
            else if (t.Type == TokenType.OpenBrace)
            {
                Value retval = new Value(Type.Object);
                t = input.ReadToken();
                if (t.Type == TokenType.CloseBrace) return retval;
                while (t.Type == TokenType.String)
                {
                    String name = t.Value;
                    t = input.ReadToken();
                    if (t.Type != TokenType.Colon) throw new InvalidDataException(String.Concat("Parse error at line ", input.LineNum.ToString(), ", character ", input.CharNum.ToString(), ": Colon (:) expected"));
                    retval[name] = ReadValue(input);
                    t = input.ReadToken();
                    if (t.Type == TokenType.CloseBrace) return retval;
                    if (t.Type != TokenType.Comma) throw new InvalidDataException(String.Concat("Parse error at line ", input.LineNum.ToString(), ", character ", input.CharNum.ToString(), ": Comma (,) or Close Brace (}) expected"));
                    t = input.ReadToken();
                }
                throw new InvalidDataException(String.Concat("Parse error at line ", input.LineNum.ToString(), ", character ", input.CharNum.ToString(), ": String expected"));
            }
            else if (t.Type == TokenType.OpenBracket)
            {
                Value retval = new Value(Type.Array);
                input.SkipWhiteSpace();
                Nullable<Char> c = input.PeekChar();
                if ((c.HasValue) && (c.Value == ']'))
                {
                    input.ReadToken();
                    return retval;
                }
                for (Int32 i = 0; ; i++)
                {
                    retval[i] = ReadValue(input);
                    t = input.ReadToken();
                    if (t.Type == TokenType.CloseBracket) return retval;
                    if (t.Type != TokenType.Comma) throw new InvalidDataException(String.Concat("Parse error at line ", input.LineNum.ToString(), ", character ", input.CharNum.ToString(), ": Comma (,) or Close Bracket (]) expected"));
                }
            }
            else if (t.Type == TokenType.Keyword)
            {
                if (t.Value == "true") return new Value(true);
                if (t.Value == "false") return new Value(false);
                if (t.Value == "null") return new Value(Type.Null);
                if (t.Value == "var")
                {
                    t = input.ReadToken();
                    if (t.Type == TokenType.Keyword)
                    {
                        t = input.ReadToken();
                        if (t.Type == TokenType.Equal)
                        {
                            return ReadValue(input);
                        }
                    }
                }
            }
            throw new InvalidDataException(String.Concat("Parse error at line ", input.LineNum.ToString(), ", character ", input.CharNum.ToString(), ": Value expected"));
        }
    }

    internal enum TokenType
    {
        Nothing,
        OpenBrace,
        CloseBrace,
        OpenBracket,
        CloseBracket,
        Colon,
        Comma,
        Equal,
        String,
        Number,
        Keyword
    }

    internal struct Token
    {
        private TokenType mType;
        private String mValue;

        public Token(TokenType type, String value)
        {
            mType = type;
            mValue = value;
        }

        public TokenType Type
        {
            get { return mType; }
        }

        public String Value
        {
            get { return mValue; }
        }
    }

    internal class Tokenizer
    {
        private TextReader mInput;
        private StringBuilder mBuffer;
        private Int32 mIndex;
        private StringBuilder mScratch;
        private Int32 mLineNum;

        public Tokenizer(TextReader input)
        {
            mInput = input;
            mBuffer = new StringBuilder(256);
            mIndex = -1;
            mScratch = new StringBuilder(32);
        }

        public Int32 LineNum
        {
            get { return mLineNum; }
        }

        public Int32 CharNum
        {
            get { return mIndex + 1; }
        }

        // see http://www.json.org/
        public Token ReadToken()
        {
            SkipWhiteSpace();
            Nullable<Char> c = PeekChar();
            if (!c.HasValue) return new Token(TokenType.Nothing, null);
            if (c.Value == '{')
            {
                SkipChar();
                return new Token(TokenType.OpenBrace, "{");
            }
            if (c.Value == '}')
            {
                SkipChar();
                return new Token(TokenType.CloseBrace, "}");
            }
            if (c.Value == '[')
            {
                SkipChar();
                return new Token(TokenType.OpenBracket, "[");
            }
            if (c.Value == ']')
            {
                SkipChar();
                return new Token(TokenType.CloseBracket, "]");
            }
            if (c.Value == ':')
            {
                SkipChar();
                return new Token(TokenType.Colon, ":");
            }
            if (c.Value == ',')
            {
                SkipChar();
                return new Token(TokenType.Comma, ",");
            }
            if (c.Value == '=')
            {
                SkipChar();
                return new Token(TokenType.Equal, "=");
            }
            if (c.Value == '"')
            {
                mScratch.Length = 0;
                SkipChar();
                while (((c = PeekChar()).HasValue) && (c.Value != '"'))
                {
                    if (Char.IsControl(c.Value)) throw new InvalidDataException();
                    if (c.Value == '\\')
                    {
                        SkipChar();
                        c = ReadChar();
                        if (!c.HasValue) throw new InvalidDataException();
                        switch (c.Value)
                        {
                            case '"':
                                mScratch.Append('"');
                                break;
                            case '\\':
                                mScratch.Append('\\');
                                break;
                            case '/':
                                mScratch.Append('/');
                                break;
                            case 'b':
                                mScratch.Append('\b');
                                break;
                            case 'f':
                                mScratch.Append('\f');
                                break;
                            case 'n':
                                mScratch.Append('\n');
                                break;
                            case 'r':
                                mScratch.Append('\r');
                                break;
                            case 't':
                                mScratch.Append('\t');
                                break;
                            case 'u':
                                Int32 n = 0;
                                for (Int32 i = 0; i < 4; i++)
                                {
                                    c = ReadChar();
                                    Int32 v = HexValue(c);
                                    if (v == -1) throw new InvalidDataException();
                                    n *= 16;
                                    n += v;
                                }
                                mScratch.Append((Char)n);
                                break;
                            default:
                                throw new InvalidDataException();
                        }
                    }
                    else
                    {
                        mScratch.Append(c.Value);
                        SkipChar();
                    }
                }
                if (!c.HasValue) throw new InvalidDataException();
                SkipChar();
                return new Token(TokenType.String, mScratch.ToString());
            }
            if ((c.Value == '-') || (Char.IsDigit(c.Value)))
            {
                mScratch.Length = 0;
                SkipChar();
                if (c.Value == '-')
                {
                    mScratch.Append(c.Value);
                    c = ReadChar();
                    if ((!c.HasValue) || (!Char.IsDigit(c.Value))) throw new InvalidDataException();
                }
                mScratch.Append(c);
                if ((c.Value >= '1') && (c.Value <= '9'))
                {
                    while (((c = PeekChar()).HasValue) && (Char.IsDigit(c.Value)))
                    {
                        mScratch.Append(c.Value);
                        SkipChar();
                    }
                }
                if (((c = PeekChar()).HasValue) && (c.Value == '.'))
                {
                    mScratch.Append(c.Value);
                    SkipChar();
                    while (((c = PeekChar()).HasValue) && (Char.IsDigit(c.Value)))
                    {
                        mScratch.Append(c.Value);
                        SkipChar();
                    }
                }
                if (((c = PeekChar()).HasValue) && ((c.Value == 'e') || (c.Value == 'E')))
                {
                    mScratch.Append(c.Value);
                    SkipChar();
                    c = PeekChar();
                    if (!c.HasValue) throw new InvalidDataException();
                    if ((c.Value == '-') || (c.Value == '+'))
                    {
                        mScratch.Append(c.Value);
                        SkipChar();
                        c = PeekChar();
                        if (!c.HasValue) throw new InvalidDataException();
                    }
                    while (((c = PeekChar()).HasValue) && (Char.IsDigit(c.Value)))
                    {
                        mScratch.Append(c.Value);
                        SkipChar();
                    }
                }
                return new Token(TokenType.Number, mScratch.ToString());
            }
            if (Char.IsLetter(c.Value))
            {
                mScratch.Length = 0;
                mScratch.Append(c.Value);
                SkipChar();
                while (((c = PeekChar()).HasValue) && (Char.IsLetter(c.Value)))
                {
                    mScratch.Append(c.Value);
                    SkipChar();
                }
                return new Token(TokenType.Keyword, mScratch.ToString());
            }
            throw new InvalidDataException();
        }

        private Int32 HexValue(Nullable<Char> character)
        {
            if (!character.HasValue) return -1;
            switch (character.Value)
            {
                case '0': return 0;
                case '1': return 1;
                case '2': return 2;
                case '3': return 3;
                case '4': return 4;
                case '5': return 5;
                case '6': return 6;
                case '7': return 7;
                case '8': return 8;
                case '9': return 9;
                case 'a':
                case 'A': return 10;
                case 'b':
                case 'B': return 11;
                case 'c':
                case 'C': return 12;
                case 'd':
                case 'D': return 13;
                case 'e':
                case 'E': return 14;
                case 'f':
                case 'F': return 15;
                default: return -1;
            }
        }

        internal Nullable<Char> PeekChar()
        {
            FillBuffer();
            if (mIndex == -1) return null;
            return mBuffer[mIndex];
        }

        private Nullable<Char> ReadChar()
        {
            FillBuffer();
            if (mIndex == -1) return null;
            return mBuffer[mIndex++];
        }

        private void SkipChar()
        {
            FillBuffer();
            if (mIndex != -1) mIndex++;
        }

        internal void SkipWhiteSpace()
        {
            Nullable<Char> c;
            while (((c = PeekChar()).HasValue) && (Char.IsWhiteSpace(c.Value))) SkipChar();
        }

        private void FillBuffer()
        {
            if ((mIndex == -1) || (mIndex >= mBuffer.Length))
            {
                mBuffer.Length = 0;
                Int32 n;
                while ((n = mInput.Read()) != -1)
                {
                    Char c = (Char)n;
                    mBuffer.Append(c);
                    if (c == '\n')
                    {
                        mLineNum++;
                        break;
                    }
                }
                mIndex = (mBuffer.Length == 0) ? -1 : 0;
            }
        }
    }
}
