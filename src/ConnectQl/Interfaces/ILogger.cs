// MIT License
//
// Copyright (c) 2017 Maarten van Sambeek.
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

namespace ConnectQl.Interfaces
{
    using System;

    using JetBrains.Annotations;

    /// <summary>
    /// The Logger interface.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Writes a message to the logger.
        /// </summary>C:\Users\Maarten\Source\Repos\ConnectQl\src\ConnectQl\Interfaces\ILogger.cs
        /// <param name="logLevel">The log level to write.</param>
        /// <param name="exception">Optinonal. The <see cref="Exception"/> if there was one, <c>null</c> otherwise.</param>
        /// <param name="format">The format string.</param>
        /// <param name="args">The arguments.</param>
        [StringFormatMethod("format")]
        void Write(LogLevel logLevel, Exception exception, string format = "", params object[] args);
    }
}