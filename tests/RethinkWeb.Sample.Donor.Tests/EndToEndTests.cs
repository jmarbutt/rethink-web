using System.IO.Pipelines;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RethinkWeb.Mcp;

namespace RethinkWeb.Sample.Donor.Tests;

public class EndToEndTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public EndToEndTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Manifest_endpoint_returns_donor_entity_and_update_address_action()
    {
        var client = _factory.CreateClient();

        var json = await client.GetStringAsync("/_framework/manifest");
        using var doc = JsonDocument.Parse(json);

        var entities = doc.RootElement.GetProperty("entities");
        entities.EnumerateArray().Should().Contain(e => e.GetProperty("slug").GetString() == "donors");

        var donors = entities.EnumerateArray().Single(e => e.GetProperty("slug").GetString() == "donors");
        donors.GetProperty("actions").EnumerateArray()
            .Should().Contain(a => a.GetProperty("name").GetString() == "update-address");
    }

    [Fact]
    public async Task HTMX_form_post_returns_fragment_only_no_layout()
    {
        var client = _factory.CreateClient();
        var donorId = await GetFirstDonorId(client);

        var form = new FormUrlEncodedContent(
        [
            new("FirstName", "Updated"),
            new("LastName", "Person"),
        ]);
        var request = new HttpRequestMessage(HttpMethod.Post, $"/donors/{donorId}")
        {
            Content = form,
        };
        request.Headers.Add("HX-Request", "true");

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("edit-form-").And.NotContain("<!DOCTYPE html>",
            "HTMX requests must receive fragments, not full layouts");
        html.Should().Contain("value=\"Updated\"");
    }

    /// <summary>
    /// Verifies the unification end-to-end via a real MCP client+server pair.
    /// We connect a real <see cref="McpClient"/> to a real <see cref="McpServer"/> via
    /// in-memory pipes, where the server's tool collection is the framework's
    /// <see cref="RethinkWebMcpToolCollection"/> resolved from the test host's DI.
    /// This proves: ActionRegistry → McpServerTool → SDK dispatch → IActionDispatcher
    /// → entity persisted, all over the standards-compliant SDK path.
    /// </summary>
    [Fact]
    public async Task MCP_tools_call_invokes_update_address_via_real_McpClient()
    {
        var httpClient = _factory.CreateClient();
        var donorId = await GetFirstDonorId(httpClient);

        await using var scope = _factory.Services.CreateAsyncScope();
        var collection = scope.ServiceProvider.GetRequiredService<RethinkWebMcpToolCollection>();

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        await using var server = McpServer.Create(
            new StreamServerTransport(
                clientToServer.Reader.AsStream(),
                serverToClient.Writer.AsStream()),
            new McpServerOptions { ToolCollection = collection.Tools },
            serviceProvider: scope.ServiceProvider);
        _ = server.RunAsync();

        await using var mcpClient = await McpClient.CreateAsync(
            new StreamClientTransport(
                clientToServer.Writer.AsStream(),
                serverToClient.Reader.AsStream()));

        var tools = await mcpClient.ListToolsAsync();
        tools.Should().Contain(t => t.Name == "donors.update-address");

        var updateAddress = tools.Single(t => t.Name == "donors.update-address");
        var result = await updateAddress.CallAsync(new Dictionary<string, object?>
        {
            ["entityId"] = donorId.ToString(),
            ["input"] = new Dictionary<string, object?>
            {
                ["address1"] = "1 Test Way",
                ["address2"] = null,
                ["city"] = "Testopolis",
                ["state"] = "CA",
                ["postalCode"] = "90210",
            },
        });

        var resultText = string.Join(" | ", result.Content.OfType<TextContentBlock>().Select(c => c.Text));
        resultText.Should().NotContain("ERROR", "tool returned an error: {0}", resultText);
        result.IsError.Should().NotBe(true, "tool call returned error: {0}", resultText);

        // Confirm by re-loading the entity edit page over HTTP — the MCP-driven
        // change must show up in the same store the form-post reads.
        var html = await httpClient.GetStringAsync($"/donors/{donorId}");
        html.Should().Contain("1 Test Way").And.Contain("Testopolis").And.Contain("90210");
    }

    private static async Task<Guid> GetFirstDonorId(HttpClient client)
    {
        var html = await client.GetStringAsync("/donors");
        var match = System.Text.RegularExpressions.Regex.Match(html, @"donors/([a-f0-9\-]{36})");
        match.Success.Should().BeTrue("seed data should produce at least one donor row");
        return Guid.Parse(match.Groups[1].Value);
    }
}
