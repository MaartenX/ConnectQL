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

namespace ConnectQl.Tools.Mef.Errors
{
    using System;
    using System.ComponentModel.Composition;
    using Interfaces;
    using Microsoft.VisualStudio.Shell;

    /// <summary>
    /// The internal error list provider.
    /// </summary>
    [Export(typeof(IErrorListProvider))]
    internal class InternalErrorListProvider : IErrorListProvider, IServiceProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InternalErrorListProvider"/> class.
        /// </summary>
        public InternalErrorListProvider()
        {
            this.ErrorList = new UpdatedErrorListProvider(this)
            {
                ProviderGuid = new Guid("D20DEBF8-C5D9-42C2-9E9D-C6C6B214CD5B"),
                ProviderName = "ConnectQl Errors"
            };
        }

        /// <summary>
        /// Gets or sets the error list.
        /// </summary>
        public UpdatedErrorListProvider ErrorList { get; set; }

        /// <summary>
        /// Gets the service object of the specified type.
        /// </summary>
        /// <param name="serviceType">An object that specifies the type of service object to get.</param>
        /// <returns>
        /// A service object of type <paramref name="serviceType" />.-or- null if there is no service object of type <paramref name="serviceType" />.
        /// </returns>
        public object GetService(Type serviceType)
        {
            return Package.GetGlobalService(serviceType);
        }
    }
}
