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

namespace ConnectQl.Intellisense
{
    using System;

    using ConnectQl.Intellisense.Protocol;
    using ConnectQl.Interfaces;

    using JetBrains.Annotations;

    /// <summary>
    /// Event arguments for the <see cref="IIntellisenseSession.DocumentUpdated" /> event.
    /// </summary>
    [PublicAPI]
    public class DocumentUpdatedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentUpdatedEventArgs"/> class.
        /// </summary>
        /// <param name="document">The document.</param>
        public DocumentUpdatedEventArgs(IDocumentDescriptor document)
        {
            this.Document = document;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentUpdatedEventArgs"/> class.
        /// </summary>
        /// <param name="serializedDocument">The serialized document.</param>
        public DocumentUpdatedEventArgs(byte[] serializedDocument)
        {
            this.Document = ProtocolSerializer.Deserialize<SerializableDocumentDescriptor>(serializedDocument);
        }

        /// <summary>
        /// Gets the document.
        /// </summary>
        public IDocumentDescriptor Document { get; }
    }
}