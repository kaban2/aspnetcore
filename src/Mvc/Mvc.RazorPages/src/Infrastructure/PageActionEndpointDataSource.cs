// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure
{
    internal class PageActionEndpointDataSource : ActionEndpointDataSourceBase
    {
        private readonly ActionEndpointFactory _endpointFactory;
        private readonly OrderedEndpointsSequenceProvider _orderSequence;
        private readonly int _dataSourceId;

        public PageActionEndpointDataSource(
            PageActionEndpointDataSourceIdProvider dataSourceIdProvider,
            IActionDescriptorCollectionProvider actions,
            ActionEndpointFactory endpointFactory,
            OrderedEndpointsSequenceProvider orderedEndpoints)
            : base(actions)
        {
            _dataSourceId = dataSourceIdProvider.CreateId();
            _endpointFactory = endpointFactory;
            _orderSequence = orderedEndpoints;
            DefaultBuilder = new PageActionEndpointConventionBuilder(Lock, Conventions);

            // IMPORTANT: this needs to be the last thing we do in the constructor.
            // Change notifications can happen immediately!
            Subscribe();
        }

        public int DataSourceId => _dataSourceId;

        public PageActionEndpointConventionBuilder DefaultBuilder { get; }

        // Used to control whether we create 'inert' (non-routable) endpoints for use in dynamic
        // selection. Set to true by builder methods that do dynamic/fallback selection.
        public bool CreateInertEndpoints { get; set; }

        protected override List<Endpoint> CreateEndpoints(IReadOnlyList<ActionDescriptor> actions, IReadOnlyList<Action<EndpointBuilder>> conventions)
        {
            var endpoints = new List<Endpoint>();
            var routeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < actions.Count; i++)
            {
                if (actions[i] is PageActionDescriptor action)
                {
                    _endpointFactory.AddEndpoints(endpoints, routeNames, action, Array.Empty<ConventionalRouteEntry>(), conventions, CreateInertEndpoints);
                }
            }

            return endpoints;
        }

        internal void AddDynamicPageEndpoint(IEndpointRouteBuilder endpoints, string pattern, Type transformerType, object state)
        {
            CreateInertEndpoints = true;
            lock (Lock)
            {
                var order = _orderSequence.GetNext();

                endpoints.Map(
                    pattern,
                    context =>
                    {
                        throw new InvalidOperationException("This endpoint is not expected to be executed directly.");
                    })
                    .Add(b =>
                    {
                        ((RouteEndpointBuilder)b).Order = order;
                        b.Metadata.Add(new DynamicPageRouteValueTransformerMetadata(transformerType, state));
                        b.Metadata.Add(new PageEndpointDataSourceIdMetadata(_dataSourceId));
                    });
            }
        }
    }
}

