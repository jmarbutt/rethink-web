using System.Net;

namespace RethinkWeb.Sample.Tasks.Tests;

/// <summary>
/// Regression tests for the five gaps Codex's PR review caught:
///   #1 Actions publish EntitySaved (PublishingEntityStore covers all paths)
///   #2 POST /{slug}/{id}/actions/{name} is routed
///   #3 Required is server-enforced
/// (Fix #4 entity/field permissions and #5 schema required-ness are covered by
/// AuthBoundariesTests and ManifestSchemaTests in the Core test project.)
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
        var todoId = await GetFirstUncompletedTodoId(client);

        // mark-complete has empty input — no form fields needed
        var form = new FormUrlEncodedContent([]);

        var response = await client.PostAsync(
            $"/tasks/{todoId}/actions/mark-complete", form);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Persistence check: re-fetch and confirm Completed flipped true
        var html = await client.GetStringAsync($"/tasks/{todoId}");
        html.Should().Contain("name=\"Completed\" value=\"true\" checked");
    }

    [Fact]
    public async Task Action_invocation_publishes_EntitySaved()
    {
        // Fix #1: actions used to skip EntitySaved publishing; now PublishingEntityStore
        // covers every save path.
        _factory.Recorder.Clear();
        var client = _factory.CreateClient();
        var todoId = await GetFirstUncompletedTodoId(client);

        var form = new FormUrlEncodedContent([]);
        await client.PostAsync($"/tasks/{todoId}/actions/mark-complete", form);

        _factory.Recorder.Received.Should().NotBeEmpty(
            "EntitySaved<Todo> must fire for action-driven writes");
        // Exactly one publish: the action's save fires it, the StampCompletedAtSubscriber
        // saves again to add CompletedAt but PublishingEntityStore's recursion guard
        // prevents re-publication within the same async call chain.
        _factory.Recorder.Received.Should().ContainSingle(
            "the recursion guard in PublishingEntityStore prevents subscriber re-saves from re-publishing");
        _factory.Recorder.Received[0].Completed.Should().BeTrue();
    }

    [Fact]
    public async Task Save_with_missing_required_field_returns_422()
    {
        // Fix #3: FormBinder collects validation errors for missing Required fields.
        var client = _factory.CreateClient();
        var todoId = await GetFirstUncompletedTodoId(client);

        // Todo.Title is Required = true. Send an empty value.
        var form = new FormUrlEncodedContent(
        [
            new("Title", ""),
        ]);
        var request = new HttpRequestMessage(HttpMethod.Post, $"/tasks/{todoId}")
        {
            Content = form,
        };
        request.Headers.Add("HX-Request", "true");

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Title is required");
    }

    private static async Task<Guid> GetFirstUncompletedTodoId(HttpClient client)
    {
        var html = await client.GetStringAsync("/tasks");
        var match = System.Text.RegularExpressions.Regex.Match(html, @"tasks/([a-f0-9\-]{36})");
        match.Success.Should().BeTrue("seed data should produce at least one task row");
        return Guid.Parse(match.Groups[1].Value);
    }
}
