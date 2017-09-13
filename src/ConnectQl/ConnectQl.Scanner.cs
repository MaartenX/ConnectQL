// <auto-generated />
using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Collections.Generic;

namespace ConnectQl.Internal
{
    /// <summary>
    /// The token.
    /// </summary>
    [GeneratedCode("CoCo/R", "0.1")]
    internal class Token
    {
        /// <summary>
        /// Gets or sets a value indicating whether the token is comment.
        /// </summary>
        public bool IsComment { get; set; } = true;

        /// <summary>
        /// Gets or sets the tokens index.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Gets or sets the token kind.
        /// </summary>
        public int Kind { get; set; }

        /// <summary>
        /// Gets or sets the token position in bytes in the source text (starting at 0).
        /// </summary>
        public int Pos { get; set; }

        /// <summary>
        /// Gets or sets the token position in characters in the source text (starting at 0).
        /// </summary>
        public int CharPos { get; set; }

        /// <summary>
        /// Gets or sets the token column (starting at 1).
        /// </summary>
        public int Col { get; set; }

        /// <summary>
        /// Gets or sets the token line (starting at 1).
        /// </summary>
        public int Line { get; set; }

        /// <summary>
        /// Gets or sets the token value.
        /// </summary>
        public string Val { get; set; }

        /// <summary>
        /// Gets or sets the ML 2005-03-11 Tokens are kept in linked list.
        /// </summary>
        public Token Next { get; set; }
    }

    /// <summary>
    /// The Buffer.
    /// This Buffer supports the following cases:
    /// 1) seekable stream (file)
    ///    a) whole stream in Buffer
    ///    b) part of stream in Buffer
    /// 2) non seekable stream (network, console)
    /// </summary>
    [GeneratedCode("CoCo/R", "0.1")]
    internal class Buffer
    {
        /// <summary>
        /// The end of file token.
        /// </summary>
        public const int Eof = char.MaxValue + 1;

        /// <summary>
        /// The minimum buffer length.
        /// </summary>
        private const int MinBufferLength = 1024; // 1KB

        /// <summary>
        /// The maximum buffer length.
        /// </summary>
        private const int MaxBufferLength = Buffer.MinBufferLength * 64; // 64KB

        /// <summary>
        /// The input buffer.
        /// </summary>
        private byte[] byteBuffer;

        /// <summary>
        /// The position of first byte in the buffer relative to input stream.
        /// </summary>
        private int firstByte;

        /// <summary>
        /// The length of the buffer.
        /// </summary>
        private int bufferLength;

        /// <summary>
        /// The length of input stream (may change if the stream is no file).
        /// </summary>
        private int inputStreamLength;

        /// <summary>
        /// The current position in the buffer.
        /// </summary>
        private int currentPosition;

        /// <summary>
        /// The input stream (seekable).
        /// </summary>
        private Stream stream;

        /// <summary>
        /// <c>true</c> if the stream was opened by the user.
        /// </summary>
        private bool isUserStream;

        /// <summary>
        /// Initializes a new instance of the <see cref="Buffer" /> class.
        /// </summary>
        /// <param name="stream">
        /// The stream.
        /// </param>
        /// <param name="isUserStream">
        /// <c>true</c> if the stream was opened by the user.
        /// </param>
        public Buffer(Stream stream, bool isUserStream)
        {
            this.stream = stream;
            this.isUserStream = isUserStream;

            if (this.stream.CanSeek)
            {
                this.inputStreamLength = (int)this.stream.Length;
                this.bufferLength = Math.Min(this.inputStreamLength, Buffer.MaxBufferLength);
                this.firstByte = int.MaxValue; // nothing in the Buffer so far
            }
            else
            {
                this.inputStreamLength = this.bufferLength = this.firstByte = 0;
            }

            this.byteBuffer = new byte[(this.bufferLength > 0) ? this.bufferLength : Buffer.MinBufferLength];
            if (this.inputStreamLength > 0)
            {
                this.Pos = 0; // setup Buffer to position 0 (start)
            }
            else
            {
                this.currentPosition = 0; // index 0 is already after the file, thus Pos = 0 is invalid
            }

            if (this.bufferLength == this.inputStreamLength && this.stream.CanSeek)
            {
                this.Close();
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Buffer" /> class.
        /// Called in UTF8Buffer constructor.
        /// </summary>
        /// <param name="b">
        /// The buffer to base this buffer on.
        /// </param>
        protected Buffer(Buffer b)
        {
            this.byteBuffer = b.byteBuffer;
            this.firstByte = b.firstByte;
            this.bufferLength = b.bufferLength;
            this.inputStreamLength = b.inputStreamLength;
            this.currentPosition = b.currentPosition;
            this.stream = b.stream;

            b.stream = null;
            this.isUserStream = b.isUserStream;
        }

        ~Buffer()
        {
            this.Close();
        }

        /// <summary>
        /// Closes the buffer.
        /// </summary>
        protected void Close()
        {
            if (!this.isUserStream && this.stream != null)
            {
                this.stream.Dispose();
                this.stream = null;
            }
        }

        /// <summary>
        /// Reads a character from the buffer and moves the buffer pointer.
        /// </summary>
        /// <returns>
        ///	The character.
        /// </returns>
        public virtual int Read()
        {
            if (this.currentPosition < this.bufferLength)
            {
                return this.byteBuffer[this.currentPosition++];
            }
            else if (this.Pos < this.inputStreamLength)
            {
                this.Pos = this.Pos; // shift Buffer start to Pos
                return this.byteBuffer[this.currentPosition++];
            }
            else if (this.stream != null && !this.stream.CanSeek && this.ReadNextStreamChunk() > 0)
            {
                return this.byteBuffer[this.currentPosition++];
            }
            else
            {
                return Buffer.Eof;
            }
        }

        /// <summary>
        /// Reads a character from the buffer without moving the buffer pointer.
        /// </summary>
        /// <returns>
        ///	The character.
        /// </returns>
        public int Peek()
        {
            var curPos = this.Pos;
            var ch = this.Read();
            this.Pos = curPos;
            return ch;
        }
        /// <summary>
        /// Gets a string starting at the beg position until the end position.
        /// </summary>
        /// <param name="beg">
        /// The position of the beginning of the string, zero-based inclusive in bytes.
        /// </param>
        /// <param name="end">
        /// The position of the end of the string, zero-based inclusive in bytes.
        /// </param>
        /// <returns>
        ///	The string.
        /// </returns>
        public string GetString(int beg, int end)
        {
            var len = 0;
            var byteBuffer = new char[end - beg];
            var oldPos = this.Pos;
            this.Pos = beg;
            while (this.Pos < end)
            {
                byteBuffer[len++] = (char)this.Read();
            }

            this.Pos = oldPos;
            return new string(byteBuffer, 0, len);
        }

        /// <summary>
        /// Gets or sets the position in the buffer.
        /// </summary>
        public int Pos
        {
            get => this.currentPosition + this.firstByte;

            set
            {
                if (value >= this.inputStreamLength && this.stream != null && !this.stream.CanSeek)
                {
                    // Wanted position is after Buffer and the stream
                    // is not seek-able e.g. network or console,
                    // thus we have to read the stream manually till
                    // the wanted position is in sight.
                    while (value >= this.inputStreamLength && this.ReadNextStreamChunk() > 0)
                    {
                    }
                }

                if (value < 0 || value > this.inputStreamLength)
                {
                    throw new FatalError("Buffer out of bounds access, position: " + value);
                }

                if (value >= this.firstByte && value < this.firstByte + this.bufferLength)
                { // already in Buffer
                    this.currentPosition = value - this.firstByte;
                }
                else if (this.stream != null)
                { // must be swapped in
                    this.stream.Seek(value, SeekOrigin.Begin);
                    this.bufferLength = this.stream.Read(this.byteBuffer, 0, this.byteBuffer.Length);
                    this.firstByte = value;
                    this.currentPosition = 0;
                }
                else
                {
                    // set the position to the end of the file, Pos will return inputStreamLength.
                    this.currentPosition = this.inputStreamLength - this.firstByte;
                }
            }
        }

        /// <summary>
        /// Read the next chunk of bytes from the stream, increases the Buffer
        /// if needed and updates the fields inputStreamLength and bufferLength.
        /// </summary>
        /// <returns>
        /// The number of bytes read.
        /// </returns>
        private int ReadNextStreamChunk()
        {
            var free = this.byteBuffer.Length - this.bufferLength;
            if (free == 0)
            {
                // in the case of a growing input stream
                // we can neither seek in the stream, nor can we
                // foresee the maximum length, thus we must adapt
                // the Buffer size on demand.
                var newBuf = new byte[this.bufferLength * 2];
                Array.Copy(this.byteBuffer, newBuf, this.bufferLength);
                this.byteBuffer = newBuf;
                free = this.bufferLength;
            }

            var read = this.stream.Read(this.byteBuffer, this.bufferLength, free);
            if (read > 0)
            {
                this.inputStreamLength = this.bufferLength = this.bufferLength + read;
                return read;
            }

            // end of stream reached
            return 0;
        }
    }

    /// <summary>
    /// A buffer that supports UTF-8 encoding.
    /// </summary>
    [GeneratedCode("CoCo/R", "0.1")]
    internal class UTF8Buffer : Buffer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Buffer" /> class.
        /// </summary>
        /// <param name="b">
        /// The buffer to base this UTF-8 Buffer on.
        /// </param>
        public UTF8Buffer(Buffer b) : base(b) { }

        /// <summary>
        /// Reads a character from the buffer and moves the buffer pointer.
        /// </summary>
        /// <returns>
        ///	The character.
        /// </returns>
        public override int Read()
        {
            int ch;
            do
            {
                ch = base.Read();
                // until we find a utf8 start (0xxxxxxx or 11xxxxxx)
            } while ((ch >= 128) && ((ch & 0xC0) != 0xC0) && (ch != Buffer.Eof));
            if (ch < 128 || ch == Buffer.Eof)
            {
                // nothing to do, first 127 chars are the same in ascii and utf8
                // 0xxxxxxx or end of file character
            }
            else if ((ch & 0xF0) == 0xF0)
            {
                // 11110xxx 10xxxxxx 10xxxxxx 10xxxxxx
                int c1 = ch & 0x07; ch = base.Read();
                int c2 = ch & 0x3F; ch = base.Read();
                int c3 = ch & 0x3F; ch = base.Read();
                int c4 = ch & 0x3F;
                ch = (((((c1 << 6) | c2) << 6) | c3) << 6) | c4;
            }
            else if ((ch & 0xE0) == 0xE0)
            {
                // 1110xxxx 10xxxxxx 10xxxxxx
                int c1 = ch & 0x0F; ch = base.Read();
                int c2 = ch & 0x3F; ch = base.Read();
                int c3 = ch & 0x3F;
                ch = (((c1 << 6) | c2) << 6) | c3;
            }
            else if ((ch & 0xC0) == 0xC0)
            {
                // 110xxxxx 10xxxxxx
                int c1 = ch & 0x1F; ch = base.Read();
                int c2 = ch & 0x3F;
                ch = (c1 << 6) | c2;
            }
            return ch;
        }
    }
    /// <summary>
    /// The scanner.
    /// </summary>
    [GeneratedCode("CoCo/R", "0.1")]
    internal class Scanner
    {
        /// <summary>
        /// The end-of-line character.
        /// </summary>
        private const char EndOfLine = '\n';

        /// <summary>
        /// The end-of-file symbol.
        /// </summary>
        private const int EofSymbol = 0;

        /// <summary>
        /// Maps first token character to start state.
        /// </summary>
        private static readonly Dictionary<int, int> Start;
        /// <summary>
        /// The maximum token.
        /// </summary>
        const int MaxToken = 67;

        /// <summary>
        /// The no-symbol token.
        /// </summary>
        const int NoSymbol = 67;

        /// <summary>
        /// The current input character (for token.Val).
        /// </summary>
        char valCh;
        /// <summary>
        /// The current token.
        /// </summary>
        private Token t;

        /// <summary>
        /// The token index.
        /// </summary>
        private int index = 0;

        /// <summary>
        /// The current input character.
        /// </summary>
        private int ch;

        /// <summary>
        /// The byte position of current character.
        /// </summary>
        private int pos;

        /// <summary>
        /// The position by unicode characters starting with 0.
        /// </summary>
        private int charPos;

        /// <summary>
        /// The column number of current character.
        /// </summary>
        private int col;

        /// <summary>
        /// The line number of current character.
        /// </summary>
        private int line;

        /// <summary>
        /// EOLs that appeared in a comment.
        /// </summary>
        private int oldEols;

        /// <summary>
        /// The list of tokens already peeked (first token is a dummy).
        /// </summary>
        private Token tokens;

        /// <summary>
        /// The current peek token.
        /// </summary>
        private Token pt;

        /// <summary>
        /// The text of current token.
        /// </summary>
        private char[] tval = new char[128]; // 

        /// <summary>
        /// The length of current token.
        /// </summary>
        private int tlen;

        /// <summary>
        /// Initializes the static state of the <see cref="Scanner" /> class.
        /// </summary>
        static Scanner()
        {
            Scanner.Start = new Dictionary<int, int>(128);
            for (int i = 48; i <= 57; ++i) Scanner.Start[i] = 3;
            for (int i = 95; i <= 95; ++i) Scanner.Start[i] = 8;
            for (int i = 97; i <= 122; ++i) Scanner.Start[i] = 8;
            Scanner.Start[39] = 1;
            Scanner.Start[91] = 6;
            Scanner.Start[64] = 9;
            Scanner.Start[61] = 13;
            Scanner.Start[44] = 14;
            Scanner.Start[40] = 15;
            Scanner.Start[41] = 16;
            Scanner.Start[62] = 28;
            Scanner.Start[60] = 29;
            Scanner.Start[43] = 20;
            Scanner.Start[45] = 21;
            Scanner.Start[42] = 22;
            Scanner.Start[47] = 23;
            Scanner.Start[37] = 24;
            Scanner.Start[94] = 25;
            Scanner.Start[33] = 26;
            Scanner.Start[46] = 27;
            Scanner.Start[Buffer.Eof] = -1;

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Scanner" /> class.
        /// </summary>
        /// <param name="s">
        /// The stream to scan for tokens.
        /// </param>
        public Scanner(Stream s)
        {
            this.Buffer = new Buffer(s, true);
            this.Init();
        }

        /// <summary>
        /// Gets or sets the scanner buffer.
        /// </summary>
        public Buffer Buffer { get; set; }

        /// <summary>
        /// Gets or sets indicating whether to emit comments.
        /// </summary>
        public bool EmitComments { get; set; }

        /// <summary>
        /// Gets the current token.
        /// </summary>
        public Token Current => this.t;

        /// <summary>
        /// Initializes the scanner.
        /// </summary>
        void Init()
        {
            this.pos = -1;
            this.line = 1;
            this.col = 0;
            this.charPos = -1;
            this.oldEols = 0;
            this.NextCh();
            if (this.ch == 0xEF)
            { // check optional byte order mark for UTF-8
                this.NextCh(); int ch1 = this.ch;
                this.NextCh(); int ch2 = this.ch;
                if (ch1 != 0xBB || ch2 != 0xBF)
                {
                    throw new FatalError(String.Format("illegal byte order mark: EF {0,2:X} {1,2:X}", ch1, ch2));
                }
                this.Buffer = new UTF8Buffer(this.Buffer);
                this.col = 0;
                this.charPos = -1;
                this.NextCh();
            }
            else
            {
                this.Buffer = new UTF8Buffer(this.Buffer);
            }
            this.pt = this.tokens = new Token();  // first token is a dummy
        }

        /// <summary>
        /// Retrieves the next character.
        /// </summary>
        void NextCh()
        {
            if (this.oldEols > 0) {
                this.ch = Scanner.EndOfLine;
                this.oldEols--; }
            else
            {
                this.pos = this.Buffer.Pos;
                // Buffer reads unicode chars, if UTF8 has been detected
                this.ch = this.Buffer.Read();
                this.col++;
                this.charPos++;
                // replace isolated '\r' by '\n' in order to make
                // eol handling uniform across Windows, Unix and Mac
                if (this.ch == '\r' && this.Buffer.Peek() != '\n') this.ch = Scanner.EndOfLine;
                if (this.ch == Scanner.EndOfLine) {
                    this.line++;
                    this.col = 0; }
            }
            if (this.ch != Buffer.Eof)
            {
                this.valCh = (char) this.ch;
                this.ch = char.ToLower((char) this.ch);
            }

        }

        /// <summary>
        /// Adds a character to the current token.
        /// </summary>
        void AddCh()
        {
            if (this.tlen >= this.tval.Length)
            {
                char[] newBuf = new char[2 * this.tval.Length];
                Array.Copy(this.tval, 0, newBuf, 0, this.tval.Length);
                this.tval = newBuf;
            }
            if (this.ch != Buffer.Eof)
            {
                this.tval[this.tlen++] = this.valCh;
                this.NextCh();
            }
        }
        bool Comment0()
        {
            int level = 1, pos0 = this.pos, line0 = this.line, col0 = this.col, charPos0 = this.charPos;
            this.AddCh();
            if (this.ch == '-')
            {
                this.AddCh();
                for (; ; )
                {
                    if (this.ch == 10)
                    {
                        level--;
                        if (level == 0) {
                            this.oldEols = this.line - line0;
                            this.AddCh(); if (this.EmitComments) {
                                this.t.Val = new String(this.tval, 0, this.tlen);
                                this.tlen = 0;
                                this.t.Kind = 6; } return true; }
                        this.AddCh();
                    }
                    else if (this.ch == Buffer.Eof) return false;
                    else this.AddCh();
                }
            }
            else
            {
                this.Buffer.Pos = pos0;
                this.AddCh();
                this.line = line0;
                this.col = col0;
                this.charPos = charPos0;
            }
            return false;
        }

        bool Comment1()
        {
            int level = 1, pos0 = this.pos, line0 = this.line, col0 = this.col, charPos0 = this.charPos;
            this.AddCh();
            if (this.ch == '/')
            {
                this.AddCh();
                for (; ; )
                {
                    if (this.ch == 10)
                    {
                        level--;
                        if (level == 0) {
                            this.oldEols = this.line - line0;
                            this.AddCh(); if (this.EmitComments) {
                                this.t.Val = new String(this.tval, 0, this.tlen);
                                this.tlen = 0;
                                this.t.Kind = 7; } return true; }
                        this.AddCh();
                    }
                    else if (this.ch == Buffer.Eof) return false;
                    else this.AddCh();
                }
            }
            else
            {
                this.Buffer.Pos = pos0;
                this.AddCh();
                this.line = line0;
                this.col = col0;
                this.charPos = charPos0;
            }
            return false;
        }

        bool Comment2()
        {
            int level = 1, pos0 = this.pos, line0 = this.line, col0 = this.col, charPos0 = this.charPos;
            this.AddCh();
            if (this.ch == '*')
            {
                this.AddCh();
                for (; ; )
                {
                    if (this.ch == '*')
                    {
                        this.AddCh();
                        if (this.ch == '/')
                        {
                            level--;
                            if (level == 0) {
                                this.oldEols = this.line - line0;
                                this.AddCh(); if (this.EmitComments) {
                                    this.t.Val = new String(this.tval, 0, this.tlen);
                                    this.tlen = 0;
                                    this.t.Kind = 8; } return true; }
                            this.AddCh();
                        }
                    }
                    else if (this.ch == Buffer.Eof) return false;
                    else this.AddCh();
                }
            }
            else
            {
                this.Buffer.Pos = pos0;
                this.AddCh();
                this.line = line0;
                this.col = col0;
                this.charPos = charPos0;
            }
            return false;
        }
        /// <summary>
        /// Checks for literals.
        /// </summary>
        void CheckLiteral()
        {
            switch (this.t.Val.ToLower())
            {
                case "use":
                    this.t.Kind = 9; break;
                case "default":
                    this.t.Kind = 10; break;
                case "for":
                    this.t.Kind = 11; break;
                case "declare":
                    this.t.Kind = 13; break;
                case "job":
                    this.t.Kind = 15; break;
                case "triggered":
                    this.t.Kind = 16; break;
                case "every":
                    this.t.Kind = 17; break;
                case "after":
                    this.t.Kind = 18; break;
                case "by":
                    this.t.Kind = 19; break;
                case "or":
                    this.t.Kind = 20; break;
                case "begin":
                    this.t.Kind = 21; break;
                case "end":
                    this.t.Kind = 22; break;
                case "trigger":
                    this.t.Kind = 23; break;
                case "import":
                    this.t.Kind = 24; break;
                case "plugin":
                    this.t.Kind = 25; break;
                case "insert":
                    this.t.Kind = 26; break;
                case "upsert":
                    this.t.Kind = 27; break;
                case "into":
                    this.t.Kind = 28; break;
                case "union":
                    this.t.Kind = 29; break;
                case "select":
                    this.t.Kind = 32; break;
                case "from":
                    this.t.Kind = 33; break;
                case "where":
                    this.t.Kind = 34; break;
                case "group":
                    this.t.Kind = 35; break;
                case "having":
                    this.t.Kind = 36; break;
                case "order":
                    this.t.Kind = 37; break;
                case "asc":
                    this.t.Kind = 38; break;
                case "desc":
                    this.t.Kind = 39; break;
                case "join":
                    this.t.Kind = 40; break;
                case "inner":
                    this.t.Kind = 41; break;
                case "left":
                    this.t.Kind = 42; break;
                case "on":
                    this.t.Kind = 43; break;
                case "cross":
                    this.t.Kind = 44; break;
                case "apply":
                    this.t.Kind = 45; break;
                case "outer":
                    this.t.Kind = 46; break;
                case "sequential":
                    this.t.Kind = 47; break;
                case "as":
                    this.t.Kind = 48; break;
                case "and":
                    this.t.Kind = 49; break;
                case "not":
                    this.t.Kind = 50; break;
                case "true":
                    this.t.Kind = 63; break;
                case "false":
                    this.t.Kind = 64; break;
                case "null":
                    this.t.Kind = 65; break;
                default: break;
            }
        }

        /// <summary>
        /// Gets the next token.
        /// </summary>
        /// <returns>
        /// The token.
        /// </returns>
        Token NextToken()
        {
            while (this.ch == ' ' || this.ch >= 9 && this.ch <= 10 || this.ch == 13 || this.ch == ' '
            ) this.NextCh();
            if (this.EmitComments)
            {
                this.t = new Token();
                this.t.Pos = this.pos;
                this.t.Col = this.col;
                this.t.Line = this.line;
                this.t.CharPos = this.charPos;
                this.tlen = 0;
            }
            if (this.ch == '-' && this.Comment0() || this.ch == '/' && this.Comment1() || this.ch == '/' && this.Comment2()) return this.EmitComments ? this.t : this.NextToken();
            int recKind = Scanner.NoSymbol;
            int recEnd = this.pos;
            this.t = new Token();
            this.t.IsComment = false;
            this.t.Pos = this.pos;
            this.t.Col = this.col;
            this.t.Line = this.line;
            this.t.CharPos = this.charPos;
            int state;
            state = Scanner.Start.ContainsKey(this.ch) ? Scanner.Start[this.ch] : 0;
            this.tlen = 0;
            this.AddCh();

            switch (state)
            {
                case -1: {
                    this.t.Kind = Scanner.EofSymbol; break; } // NextCh already done
                case 0:
                    {
                        if (recKind != Scanner.NoSymbol)
                        {
                            this.tlen = recEnd - this.t.Pos;
                            this.SetScannerBehindT();
                        }
                        this.t.Kind = recKind; break;
                    } // NextCh already done
                case 1:
                    if (this.ch <= 9 || this.ch >= 11 && this.ch <= 12 || this.ch >= 14 && this.ch <= '&' || this.ch >= '(' && this.ch <= 65535) {
                        this.AddCh(); goto case 1; }
                    else if (this.ch == 39) {
                        this.AddCh(); goto case 11; }
                    else { goto case 0; }
                case 2:
                    if (this.ch <= 9 || this.ch >= 11 && this.ch <= 12 || this.ch >= 14 && this.ch <= '&' || this.ch >= '(' && this.ch <= 65535) {
                        this.AddCh(); goto case 2; }
                    else if (this.ch == 39) {
                        this.AddCh(); goto case 12; }
                    else { goto case 0; }
                case 3:
                    recEnd = this.pos; recKind = 2;
                    if (this.ch >= '0' && this.ch <= '9') {
                        this.AddCh(); goto case 3; }
                    else if (this.ch == '.') {
                        this.AddCh(); goto case 4; }
                    else {
                        this.t.Kind = 2; break; }
                case 4:
                    if (this.ch >= '0' && this.ch <= '9') {
                        this.AddCh(); goto case 5; }
                    else { goto case 0; }
                case 5:
                    recEnd = this.pos; recKind = 2;
                    if (this.ch >= '0' && this.ch <= '9') {
                        this.AddCh(); goto case 5; }
                    else {
                        this.t.Kind = 2; break; }
                case 6:
                    if (this.ch <= 9 || this.ch >= 11 && this.ch <= 12 || this.ch >= 14 && this.ch <= 92 || this.ch >= '^' && this.ch <= 65535) {
                        this.AddCh(); goto case 6; }
                    else if (this.ch == ']') {
                        this.AddCh(); goto case 7; }
                    else { goto case 0; }
                case 7:
                    {
                        this.t.Kind = 3; break; }
                case 8:
                    recEnd = this.pos; recKind = 4;
                    if (this.ch >= '0' && this.ch <= '9' || this.ch == '_' || this.ch >= 'a' && this.ch <= 'z') {
                        this.AddCh(); goto case 8; }
                    else {
                        this.t.Kind = 4;
                        this.t.Val = new String(this.tval, 0, this.tlen);
                        this.CheckLiteral(); return this.t; }
                case 9:
                    if (this.ch == '_' || this.ch >= 'a' && this.ch <= 'z') {
                        this.AddCh(); goto case 10; }
                    else { goto case 0; }
                case 10:
                    recEnd = this.pos; recKind = 5;
                    if (this.ch >= '0' && this.ch <= '9' || this.ch == '_' || this.ch >= 'a' && this.ch <= 'z') {
                        this.AddCh(); goto case 10; }
                    else {
                        this.t.Kind = 5; break; }
                case 11:
                    recEnd = this.pos; recKind = 1;
                    if (this.ch == 39) {
                        this.AddCh(); goto case 2; }
                    else {
                        this.t.Kind = 1; break; }
                case 12:
                    recEnd = this.pos; recKind = 1;
                    if (this.ch == 39) {
                        this.AddCh(); goto case 2; }
                    else {
                        this.t.Kind = 1; break; }
                case 13:
                    {
                        this.t.Kind = 12; break; }
                case 14:
                    {
                        this.t.Kind = 14; break; }
                case 15:
                    {
                        this.t.Kind = 30; break; }
                case 16:
                    {
                        this.t.Kind = 31; break; }
                case 17:
                    {
                        this.t.Kind = 52; break; }
                case 18:
                    {
                        this.t.Kind = 53; break; }
                case 19:
                    {
                        this.t.Kind = 55; break; }
                case 20:
                    {
                        this.t.Kind = 56; break; }
                case 21:
                    {
                        this.t.Kind = 57; break; }
                case 22:
                    {
                        this.t.Kind = 58; break; }
                case 23:
                    {
                        this.t.Kind = 59; break; }
                case 24:
                    {
                        this.t.Kind = 60; break; }
                case 25:
                    {
                        this.t.Kind = 61; break; }
                case 26:
                    {
                        this.t.Kind = 62; break; }
                case 27:
                    {
                        this.t.Kind = 66; break; }
                case 28:
                    recEnd = this.pos; recKind = 51;
                    if (this.ch == '=') {
                        this.AddCh(); goto case 17; }
                    else {
                        this.t.Kind = 51; break; }
                case 29:
                    recEnd = this.pos; recKind = 54;
                    if (this.ch == '=') {
                        this.AddCh(); goto case 18; }
                    else if (this.ch == '>') {
                        this.AddCh(); goto case 19; }
                    else {
                        this.t.Kind = 54; break; }

            }
            this.t.Val = new String(this.tval, 0, this.tlen);
            return this.t;
        }

        /// <summary>
        /// Positions the scannert to after the currenttoken.
        /// </summary>
        private void SetScannerBehindT()
        {
            this.Buffer.Pos = this.t.Pos;
            this.NextCh();
            this.line = this.t.Line;
            this.col = this.t.Col;
            this.charPos = this.t.CharPos;
            for (int i = 0; i < this.tlen; i++) this.NextCh();
        }

        /// <summary>
        /// Gets the next token (possibly a token already seen during peeking)
        /// </summary>
        /// <returns>
        /// The token.
        /// </returns>
        public Token Scan()
        {
            Token result;
            if (this.tokens.Next == null)
            {
                result = this.NextToken();
                result.Index = ++this.index;
            }
            else
            {
                this.pt = this.tokens = this.tokens.Next;
                result = this.tokens;
            }

            return result;
        }

        /// <summary>
        /// Peeks for the next token, ignore pragmas.
        /// </summary>
        /// <returns>
        /// The next token.
        /// </returns>
        public Token Peek()
        {
            do
            {
                if (this.pt.Next == null)
                {
                    this.pt.Next = this.NextToken();
                }
                this.pt = this.pt.Next;
            } while (this.pt.Kind > Scanner.MaxToken); // skip pragmas

            this.pt.Index = ++this.index;
            return this.pt;
        }

        /// <summary>
        /// make sure that peeking starts at the current scan position
        /// </summary>
        public void ResetPeek()
        {
            this.pt = this.tokens;
        }
    }
}