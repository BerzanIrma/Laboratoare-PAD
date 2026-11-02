using Consul;
using Ocelot.Logging;
using Ocelot.Provider.Consul;
using Ocelot.Provider.Consul.Interfaces;

namespace Gateway;

// Ocelot foloseste implicit numele nodului Consul ca host.
// Noi vrem IP-ul cu care s-a inregistrat fiecare instanta de API.
public class IpConsulServiceBuilder : DefaultConsulServiceBuilder
{
public IpConsulServiceBuilder(IHttpContextAccessor contextAccessor,
                              IConsulClientFactory clientFactory,
                              IOcelotLoggerFactory loggerFactory)
    : base(contextAccessor, clientFactory, loggerFactory) { }

    protected override string GetDownstreamHost(ServiceEntry entry, Node node)
        => entry.Service.Address;
}