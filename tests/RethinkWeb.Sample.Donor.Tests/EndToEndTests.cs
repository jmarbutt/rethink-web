using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

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

    [Fact]
    public async Task MCP_tools_call_invokes_update_address_action()
    {
        var client = _factory.CreateClient();
        var donorId = await GetFirstDonorId(client);

        var payload = new
        {
            name = "donors.update-address",
            arguments = new
            {
                entityId = donorId.ToString(),
                address1 = "1 Test Way",
                address2 = (string?)null,
                city = "Testopolis",
                state = "CA",
                postalCode = "90210",
            },
        };

        var response = await client.PostAsJsonAsync("/mcp/tools/call", payload);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Confirm by reloading the entity edit page.
        var html = await client.GetStringAsync($"/donors/{donorId}");
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
