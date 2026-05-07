using System.Net;

namespace RethinkWeb.Sample.Donor.Tests;

/// <summary>
/// Tests covering the bugs Codex caught in the Codex review:
///   #1 Actions did not publish EntitySaved (now: PublishingEntityStore covers all paths)
///   #2 POST /{slug}/{id}/actions/{name} was documented but not routed
///   #3 Required was not server-enforced
/// </summary>
public class HttpFixesTests : IClassFixture<RecordingFactory>
{
    private readonly RecordingFactory _factory;

    public HttpFixesTests(RecordingFactory factory) => _factory = factory;

    [Fact]
    public async Task Action_endpoint_dispatches_via_HTTP_form_post_and_persists()
    {
        // Fix #2: POST /{slug}/{id}/actions/{name} now exists.
        var client = _factory.CreateClient();
        var donorId = await GetFirstDonorId(client);

        var form = new FormUrlEncodedContent(
        [
            new("Address1", "999 Action Way"),
            new("Address2", ""),
            new("City", "ActionCity"),
            new("State", "AC"),
            new("PostalCode", "00001"),
        ]);

        var response = await client.PostAsync(
            $"/donors/{donorId}/actions/update-address", form);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Persistence check via the read endpoint
        var html = await client.GetStringAsync($"/donors/{donorId}");
        html.Should().Contain("999 Action Way").And.Contain("ActionCity");
    }

    [Fact]
    public async Task Action_invocation_publishes_EntitySaved()
    {
        // Fix #1: actions used to skip EntitySaved publishing; now PublishingEntityStore
        // covers every save path. Recorder is registered via RecordingFactory so we
        // share one host across tests (avoids two factories fighting over the SQLite file).
        _factory.Recorder.Clear();
        var client = _factory.CreateClient();
        var donorId = await GetFirstDonorId(client);

        var form = new FormUrlEncodedContent(
        [
            new("Address1", "Pub Test"),
            new("Address2", ""),
            new("City", "PubCity"),
            new("State", "PB"),
            new("PostalCode", "11111"),
        ]);

        await client.PostAsync($"/donors/{donorId}/actions/update-address", form);

        _factory.Recorder.Received.Should().NotBeEmpty(
            "EntitySaved<Donor> must fire for action-driven writes, not just HTML form posts");
        _factory.Recorder.Received.Should().Contain(d => d.Address1 == "Pub Test");
    }

    [Fact]
    public async Task Save_with_missing_required_field_returns_422()
    {
        // Fix #3: FormBinder now collects validation errors for missing Required fields.
        var client = _factory.CreateClient();
        var donorId = await GetFirstDonorId(client);

        // Donor.FirstName is Required = true. Send an empty value.
        var form = new FormUrlEncodedContent(
        [
            new("FirstName", ""),
            new("LastName", "Whatever"),
        ]);
        var request = new HttpRequestMessage(HttpMethod.Post, $"/donors/{donorId}")
        {
            Content = form,
        };
        request.Headers.Add("HX-Request", "true");

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("First Name is required");
    }

    private static async Task<Guid> GetFirstDonorId(HttpClient client)
    {
        var html = await client.GetStringAsync("/donors");
        var match = System.Text.RegularExpressions.Regex.Match(html, @"donors/([a-f0-9\-]{36})");
        match.Success.Should().BeTrue("seed data should produce at least one donor row");
        return Guid.Parse(match.Groups[1].Value);
    }
}
