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
    using System.Collections.Generic;

    /// <summary>
    /// The DocumentDescriptor interface.
    /// </summary>
    public interface IDocumentDescriptor
    {
        /// <summary>
        /// Gets the filename.
        /// </summary>
        string Filename { get; }

        /// <summary>
        /// Gets the document version number.
        /// </summary>
        int Version { get; }

        /// <summary>
        /// Gets the functions.
        /// </summary>
        IReadOnlyList<IFunctionDescriptor> Functions { get; }

        /// <summary>
        /// Gets the tokens.
        /// </summary>
        IReadOnlyList<IClassifiedToken> Tokens { get; }

        /// <summary>
        /// Gets the messages.
        /// </summary>
        IReadOnlyList<IMessage> Messages { get; }

        /// <summary>
        /// Gets the variables.
        /// </summary>
        IReadOnlyList<IVariableDescriptorRange> Variables { get; }

        /// <summary>
        /// Gets the sources.
        /// </summary>
        IReadOnlyList<IDataSourceDescriptorRange> Sources { get; }

        /// <summary>
        /// Gets the plugins.
        /// </summary>
        IReadOnlyList<string> Plugins { get; }
    }
}