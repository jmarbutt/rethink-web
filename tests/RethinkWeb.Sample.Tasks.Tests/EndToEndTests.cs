using System.IO.Pipelines;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RethinkWeb.Mcp;

namespace RethinkWeb.Sample.Tasks.Tests;

/// <summary>
/// End-to-end framework integration tests against the Tasks sample.
/// Covers manifest discovery, HTMX form post, MCP via real McpClient.
/// </summary>
public class EndToEndTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public EndToEndTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Manifest_endpoint_returns_todo_entity_and_mark_complete_action()
    {
        var client = _factory.CreateClient();

        var json = await client.GetStringAsync("/_framework/manifest");
        using var doc = JsonDocument.Parse(json);

        var entities = doc.RootElement.GetProperty("entities");
        entities.EnumerateArray().Should().Contain(e => e.GetProperty("slug").GetString() == "tasks");

        var tasks = entities.EnumerateArray().Single(e => e.GetProperty("slug").GetString() == "tasks");
        tasks.GetProperty("actions").EnumerateArray()
            .Should().Contain(a => a.GetProperty("name").GetString() == "mark-complete");
    }

    [Fact]
    public async Task HTMX_form_post_returns_fragment_only_no_layout()
    {
        var client = _factory.CreateClient();
        var todoId = await GetFirstTodoId(client);

        var form = new FormUrlEncodedContent(
        [
            new("Title", "Updated title"),
        ]);
        var request = new HttpRequestMessage(HttpMethod.Post, $"/tasks/{todoId}")
        {
            Content = form,
        };
        request.Headers.Add("HX-Request", "true");

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("edit-form-").And.NotContain("<!DOCTYPE html>",
            "HTMX requests must receive fragments, not full layouts");
        html.Should().Contain("value=\"Updated title\"");
    }

    [Fact]
    public async Task MCP_tools_call_invokes_mark_complete_via_real_McpClient()
    {
        var httpClient = _factory.CreateClient();
        var todoId = await GetFirstTodoId(httpClient);

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
        tools.Should().Contain(t => t.Name == "tasks.mark-complete");

        var markComplete = tools.Single(t => t.Name == "tasks.mark-complete");
        var result = await markComplete.CallAsync(new Dictionary<string, object?>
        {
            ["entityId"] = todoId.ToString(),
            ["input"] = new Dictionary<string, object?>(),
        });

        var resultText = string.Join(" | ", result.Content.OfType<TextContentBlock>().Select(c => c.Text));
        resultText.Should().NotContain("ERROR", "tool returned an error: {0}", resultText);
        result.IsError.Should().NotBe(true);

        // Confirm via HTTP that the task is now Completed (and CompletedAt was stamped
        // by the EntitySaved subscriber that runs whenever the task saves).
        var html = await httpClient.GetStringAsync($"/tasks/{todoId}");
        html.Should().Contain("name=\"Completed\" value=\"true\" checked");
        html.Should().Contain("name=\"CompletedAt\""); // present, with a value (don't pin the time)
    }

    private static async Task<Guid> GetFirstTodoId(HttpClient client)
    {
        var html = await client.GetStringAsync("/tasks");
        var match = System.Text.RegularExpressions.Regex.Match(html, @"tasks/([a-f0-9\-]{36})");
        match.Success.Should().BeTrue("seed data should produce at least one task row");
        return Guid.Parse(match.Groups[1].Value);
    }
}
