// Copyright (c) Umbraco.
// See LICENSE for more details.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Runtime;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.DependencyInjection;

namespace Umbraco.Cms.Infrastructure.HostedServices
{
    /// <summary>
    /// Hosted service implementation for scheduled publishing feature.
    /// </summary>
    /// <remarks>
    /// Runs only on non-replica servers.</remarks>
    public class ScheduledPublishing : RecurringHostedServiceBase
    {
        private readonly IContentService _contentService;
        private readonly ILogger<ScheduledPublishing> _logger;
        private readonly IMainDom _mainDom;
        private readonly IRuntimeState _runtimeState;
        private readonly IServerMessenger _serverMessenger;
        private readonly IScopeProvider _scopeProvider;
        private readonly IServerRoleAccessor _serverRegistrar;
        private readonly IUmbracoContextFactory _umbracoContextFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScheduledPublishing"/> class.
        /// </summary>
        // Note: Ignoring the two version notice rule as this class should probably be internal.
        // We don't expect anyone downstream to be instantiating a HostedService
        [Obsolete("This constructor will be removed in version 10, please use an alternative constructor.")]
        public ScheduledPublishing(
            IRuntimeState runtimeState,
            IMainDom mainDom,
            IServerRoleAccessor serverRegistrar,
            IContentService contentService,
            IUmbracoContextFactory umbracoContextFactory,
            ILogger<ScheduledPublishing> logger,
            IServerMessenger serverMessenger)
            : this(
                runtimeState,
                mainDom,
                serverRegistrar,
                contentService,
                umbracoContextFactory,
                logger,
                serverMessenger,
                StaticServiceProvider.Instance.GetRequiredService<IScopeProvider>())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ScheduledPublishing"/> class.
        /// </summary>
        public ScheduledPublishing(
            IRuntimeState runtimeState,
            IMainDom mainDom,
            IServerRoleAccessor serverRegistrar,
            IContentService contentService,
            IUmbracoContextFactory umbracoContextFactory,
            ILogger<ScheduledPublishing> logger,
            IServerMessenger serverMessenger,
            IScopeProvider scopeProvider)
            : base(TimeSpan.FromMinutes(1), DefaultDelay)
        {
            _runtimeState = runtimeState;
            _mainDom = mainDom;
            _serverRegistrar = serverRegistrar;
            _contentService = contentService;
            _umbracoContextFactory = umbracoContextFactory;
            _logger = logger;
            _serverMessenger = serverMessenger;
            _scopeProvider = scopeProvider;
        }

        public override Task PerformExecuteAsync(object state)
        {
            if (Suspendable.ScheduledPublishing.CanRun == false)
            {
                return Task.CompletedTask;
            }

            switch (_serverRegistrar.CurrentServerRole)
            {
                case ServerRole.Subscriber:
                    _logger.LogDebug("Does not run on subscriber servers.");
                    return Task.CompletedTask;
                case ServerRole.Unknown:
                    _logger.LogDebug("Does not run on servers with unknown role.");
                    return Task.CompletedTask;
            }

            // Ensure we do not run if not main domain, but do NOT lock it
            if (_mainDom.IsMainDom == false)
            {
                _logger.LogDebug("Does not run if not MainDom.");
                return Task.CompletedTask;
            }

            // Do NOT run publishing if not properly running
            if (_runtimeState.Level != RuntimeLevel.Run)
            {
                _logger.LogDebug("Does not run if run level is not Run.");
                return Task.CompletedTask;
            }

            try
            {
                // Ensure we run with an UmbracoContext, because this will run in a background task,
                // and developers may be using the UmbracoContext in the event handlers.

                // TODO: or maybe not, CacheRefresherComponent already ensures a context when handling events
                // - UmbracoContext 'current' needs to be refactored and cleaned up
                // - batched messenger should not depend on a current HttpContext
                //    but then what should be its "scope"? could we attach it to scopes?
                // - and we should definitively *not* have to flush it here (should be auto)

                using UmbracoContextReference contextReference = _umbracoContextFactory.EnsureUmbracoContext();
                using IScope scope = _scopeProvider.CreateScope(autoComplete: true);

                /* We used to assume that there will never be two instances running concurrently where (IsMainDom && ServerRole == SchedulingPublisher)
                 * However this is possible during an azure deployment slot swap for the SchedulingPublisher instance when trying to achieve zero downtime deployments.
                 * If we take a distributed write lock, we are certain that the multiple instances of the job will not run in parallel.
                 * It's possible that during the swapping process we may run this job more frequently than intended but this is not of great concern and it's
                 * only until the old SchedulingPublisher shuts down. */
                scope.EagerWriteLock(Constants.Locks.ScheduledPublishing);
                try
                {
                    // Run
                    IEnumerable<PublishResult> result = _contentService.PerformScheduledPublish(DateTime.Now);
                    foreach (IGrouping<PublishResultType, PublishResult> grouped in result.GroupBy(x => x.Result))
                    {
                        _logger.LogInformation(
                            "Scheduled publishing result: '{StatusCount}' items with status {Status}",
                            grouped.Count(),
                            grouped.Key);
                    }
                }
                finally
                {
                    // If running on a temp context, we have to flush the messenger
                    if (contextReference.IsRoot)
                    {
                        _serverMessenger.SendMessages();
                    }
                }
            }
            catch (Exception ex)
            {
                // important to catch *everything* to ensure the task repeats
                _logger.LogError(ex, "Failed.");
            }

            return Task.CompletedTask;
        }
    }
}
