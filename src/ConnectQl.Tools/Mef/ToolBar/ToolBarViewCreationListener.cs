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

namespace ConnectQl.Tools.Mef.ToolBar
{
    using System.ComponentModel.Composition;

    using ConnectQl.Tools.Interfaces;
    using ConnectQl.Tools.Mef.Results;

    using Microsoft.VisualStudio.Editor;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.TextManager.Interop;
    using Microsoft.VisualStudio.Utilities;

    /// <summary>
    /// The completion source view creation listener.
    /// </summary>
    [Export(typeof(IVsTextViewCreationListener))]
    [Name("ConnectQl Toolbar handler")]
    [ContentType("ConnectQl")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal class ToolBarViewCreationListener : IVsTextViewCreationListener
    {
        /// <summary>
        /// Gets or sets the adapter service.
        /// </summary>
        [Import]
        internal IVsEditorAdaptersFactoryService AdapterService { get; set; }

        /// <summary>
        /// Gets or sets the document provider.
        /// </summary>
        [Import]
        internal IDocumentProvider DocumentProvider { get; set; }

        /// <summary>
        /// Called when a <see cref="T:Microsoft.VisualStudio.TextManager.Interop.IVsTextView"/> adapter has been created
        ///     and initialized.
        /// </summary>
        /// <param name="textViewAdapter">
        /// The newly created and initialized text view adapter.
        /// </param>
        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            ITextView textView = this.AdapterService.GetWpfTextView(textViewAdapter);

            textView?.Properties.GetOrCreateSingletonProperty(() => new ToolBarCommandTarget(textViewAdapter, textView, this));
        }
    }
}
