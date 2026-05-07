using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModelContextProtocol.Server;

namespace RethinkWeb.Mcp;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Wires the official ModelContextProtocol server (Streamable HTTP transport) into the app.
    /// Registered actions automatically appear as MCP tools at the route mapped via
    /// <c>app.MapMcp("/mcp")</c>.
    /// </summary>
    /// <param name="builder">The RethinkWeb DI builder.</param>
    /// <param name="stateless">
    /// Recommended for servers that don't need server-to-client requests
    /// (sampling/elicitation). Enables horizontal scaling. Default: true.
    /// </param>
    public static RethinkWebBuilder AddRethinkWebMcpServer(
        this RethinkWebBuilder builder,
        bool stateless = true)
    {
        builder.Services.TryAddSingleton<RethinkWebMcpToolCollection>();

        builder.Services
            .AddMcpServer()
            .WithHttpTransport(o => o.Stateless = stateless);

        // Wire our dynamically-built tool collection into the SDK's options.
        builder.Services
            .AddOptions<McpServerOptions>()
            .Configure<RethinkWebMcpToolCollection>((options, collection) =>
            {
                options.ToolCollection = collection.Tools;
            });

        return builder;
    }
}
