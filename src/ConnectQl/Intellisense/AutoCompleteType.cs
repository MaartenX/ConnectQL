﻿// MIT License
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

using System;

namespace ConnectQl.Intellisense
{
    /// <summary>
    /// Specifies what type of auto completions are available.
    /// </summary>
    [Flags]
    public enum AutoCompleteType
    {
        /// <summary>
        /// No auto complete possible.
        /// </summary>
        None = 0,

        /// <summary>
        /// Can be completed using the literals that were provided in the AutoCompleteOptions.
        /// </summary>
        Literal = 1,

        /// <summary>
        /// Can be completed by an expression.
        /// </summary>
        Expression = 2,

        /// <summary>
        /// Can be completed by a source alias.
        /// </summary>
        SourceAlias = 4,

        /// <summary>
        /// Can be completed by a source field.
        /// </summary>
        Field = 8,

        /// <summary>
        /// Can be completed by a plugin name.
        /// </summary>
        Plugin = 16,
        Operator = 32,
        Source = 64,
        Target = 128,
    }
}