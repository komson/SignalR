﻿using System;

namespace SignalR
{
    /// <summary>
    /// Provides access to default host information.
    /// </summary>
    public static class GlobalHost
    {
        private static readonly Lazy<IDependencyResolver> _defaultResolver = new Lazy<IDependencyResolver>(() => new DefaultDependencyResolver());

        /// <summary>
        /// Gets the the default <see cref="IDependencyResolver"/>
        /// </summary>
        public static IDependencyResolver DependencyResolver
        {
            get
            {
                return _defaultResolver.Value;
            }
        }

        /// <summary>
        /// Gets the default <see cref="IConfigurationManager"/>
        /// </summary>
        public static IConfigurationManager Configuration
        {
            get
            {
                return DependencyResolver.Resolve<IConfigurationManager>();
            }
        }

        /// <summary>
        /// Gets the default <see cref="IConnectionManager"/>
        /// </summary>
        public static IConnectionManager ConnectionManager
        {
            get
            {
                return DependencyResolver.Resolve<IConnectionManager>();
            }
        }
    }
}
