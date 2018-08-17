using System;
using Dolittle.Applications.Configuration;
using Dolittle.Logging;
using Dolittle.Serialization.Json;

namespace Dolittle.Build.Topology
{
    internal class TopologyConfigurationHandler
    {
        readonly BoundedContextConfigurationManager _configurationManager;
        
        internal TopologyConfigurationHandler(ISerializer configurationSerializer)
        {
            _configurationManager = new BoundedContextConfigurationManager(configurationSerializer);
        }

        internal BoundedContextConfiguration Build(Type[] types, ILogger logger)
        {
            var boundedContextConfiguration = _configurationManager.Load();
            return new TopologyBuilder(types, boundedContextConfiguration, logger).Build();
        }
    }
}