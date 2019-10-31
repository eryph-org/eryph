// original source and license:
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
// based on https://raw.githubusercontent.com/aspnet/AspNetCore/2.1.3/src/Mvc/Mvc.Testing/src/WebApplicationFactoryClientOptions.cs


using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;

namespace Haipa.TestUtils
{
    /// <summary>
    /// The default options to use to when creating
    /// <see cref="HttpClient"/> instances by calling
    /// <see cref="WebModuleFactory{TModule}.CreateClient(WebModuleFactoryClientOptions)"/>.
    /// </summary>
    public class WebModuleFactoryClientOptions
    {
        /// <summary>
        /// Initializes a new instance of <see cref="WebModuleFactoryClientOptions"/>.
        /// </summary>
        public WebModuleFactoryClientOptions()
        {
        }

        // Copy constructor
        internal WebModuleFactoryClientOptions(WebModuleFactoryClientOptions clientOptions)
        {
            BaseAddress = clientOptions.BaseAddress;
            AllowAutoRedirect = clientOptions.AllowAutoRedirect;
            MaxAutomaticRedirections = clientOptions.MaxAutomaticRedirections;
            HandleCookies = clientOptions.HandleCookies;
        }

        /// <summary>
        /// Gets or sets the base address of <see cref="HttpClient"/> instances created by calling
        /// <see cref="WebModuleFactory{TModule}.CreateClient(WebModuleFactoryClientOptions)"/>.
        /// The default is <c>http://localhost</c>.
        /// </summary>
        public Uri BaseAddress { get; set; } = new Uri("http://localhost");

        /// <summary>
        /// Gets or sets whether or not <see cref="HttpClient"/> instances created by calling
        /// <see cref="WebModuleFactory{TModule}.CreateClient(WebModuleFactoryClientOptions)"/>
        /// should automatically follow redirect responses.
        /// The default is <c>true</c>.
        /// /// </summary>
        public bool AllowAutoRedirect { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of redirect responses that <see cref="HttpClient"/> instances
        /// created by calling <see cref="WebModuleFactory{TModule}.CreateClient(WebModuleFactoryClientOptions)"/>
        /// should follow.
        /// The default is <c>7</c>.
        /// </summary>
        public int MaxAutomaticRedirections { get; set; } = 7;

        /// <summary>
        /// Gets or sets whether <see cref="HttpClient"/> instances created by calling 
        /// <see cref="WebModuleFactory{TModule}.CreateClient(WebModuleFactoryClientOptions)"/>
        /// should handle cookies.
        /// The default is <c>true</c>.
        /// </summary>
        public bool HandleCookies { get; set; } = true;

        internal DelegatingHandler[] CreateHandlers()
        {
            return CreateHandlersCore().ToArray();

            IEnumerable<DelegatingHandler> CreateHandlersCore()
            {
                if (AllowAutoRedirect)
                {
                    yield return new RedirectHandler(MaxAutomaticRedirections);
                }
                if (HandleCookies)
                {
                    yield return new CookieContainerHandler();
                }
            }
        }
    }
}