using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MedFlow.Core.Entities;
using MedFlow.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace MedFlow.Api.Tests;

/// <summary>Spec 043: lab orders and manually entered results with abnormal flags. Doctor-private and audited.</summary>
public class LabOrderTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _f;
    public LabOrderTests(TestApiFactory f) => _f = f;

    private static string Url(int pid, string tail = "") => $"/api/patients/{pid}/labs{tail}";

    private async Task<(AuthResult doctor, int pid)> SetupAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var doctor = await _f.RegisterDoctorAsync($"doc-{tag}@x.com");
        return (doctor, await _f.CreatePatientAsync(doctor.Token, $"pat-{tag}@x.com"));
    }

    private static object Order(string name = "Lipid panel", string? notes = "Fasting") => new { testName = name, notes };
    private static object Result(string analyte = "LDL", decimal value = 160, decimal? low = 0, decimal? high = 100) =>
        new { analyteName = analyte, value, unit = "mg/dL", referenceLow = low, referenceHigh = high };

    private async Task<int> CreateOrderAsync(string token, int pid, object? body = null)
    {
        var res = await _f.ClientFor(token).PostAsJsonAsync(Url(pid), body ?? Order());
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    private async Task<JsonElement> AddResultAsync(string token, int pid, int orderId, object body)
    {
        var res = await _f.ClientFor(token).PostAsJsonAsync(Url(pid, $"/{orderId}/results"), body);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static string FlagOf(JsonElement order, int index) => order.GetProperty("results")[index].GetProperty("flag").GetString()!;

    // ── Orders ────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Order_crud_and_listing()
    {
        var (d, pid) = await SetupAsync();
        var c = _f.ClientFor(d.Token);

        var id = await CreateOrderAsync(d.Token, pid, Order("  HbA1c  ", "  "));
        var list = await c.GetFromJsonAsync<JsonElement>(Url(pid));
        var o = list.GetProperty("orders")[0];
        Assert.Equal("HbA1c", o.GetProperty("testName").GetString());
        Assert.Equal(JsonValueKind.Null, o.GetProperty("notes").ValueKind);
        Assert.Equal("Ordered", o.GetProperty("status").GetString());

        var put = await c.PutAsJsonAsync(Url(pid, $"/{id}"), Order("HbA1c repeat", "Recheck"));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal("HbA1c repeat", (await put.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("testName").GetString());

        var cancel = await c.PostAsync(Url(pid, $"/{id}/cancel"), null);
        Assert.Equal("Cancelled", (await cancel.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsync(Url(pid, $"/{id}/cancel"), null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync(Url(pid, $"/{id}"), Order())).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync(Url(pid, $"/{id}"))).StatusCode);
        list = await c.GetFromJsonAsync<JsonElement>(Url(pid));
        Assert.Equal(0, list.GetProperty("orders").GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await c.DeleteAsync(Url(pid, $"/{id}"))).StatusCode);
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("   ", null)]
    public async Task Blank_test_name_is_rejected(string name, string? notes)
    {
        var (d, pid) = await SetupAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await _f.ClientFor(d.Token).PostAsJsonAsync(Url(pid), Order(name, notes))).StatusCode);
    }

    [Fact]
    public async Task Order_length_and_future_date_limits_are_enforced()
    {
        var (d, pid) = await SetupAsync();
        var c = _f.ClientFor(d.Token);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Url(pid), Order(new string('x', 151)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Url(pid), Order("ok", new string('n', 1001)))).StatusCode);
        var future = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2).ToString("yyyy-MM-dd");
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Url(pid), new { testName = "x", orderedDate = future })).StatusCode);
        Assert.Equal(0, (await c.GetFromJsonAsync<JsonElement>(Url(pid))).GetProperty("orders").GetArrayLength());
        // exactly at the limits is accepted
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync(Url(pid), Order(new string('x', 150), new string('n', 1000)))).StatusCode);
    }

    [Fact]
    public async Task A_patient_can_have_at_most_100_orders()
    {
        var (d, pid) = await SetupAsync();
        var c = _f.ClientFor(d.Token);
        for (var i = 0; i < 100; i++)
            Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync(Url(pid), Order($"T{i}"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Url(pid), Order("one too many"))).StatusCode);
    }

    // ── Results and flags ─────────────────────────────────────────────────────
    [Fact]
    public async Task Values_outside_the_entered_range_are_flagged_and_bounds_are_inclusive()
    {
        var (d, pid) = await SetupAsync();
        var id = await CreateOrderAsync(d.Token, pid);

        var o = await AddResultAsync(d.Token, pid, id, Result("LDL", 160, 0, 100));
        Assert.Equal("Completed", o.GetProperty("status").GetString());
        Assert.Equal("High", FlagOf(o, 0));
        Assert.Equal(1, o.GetProperty("abnormalCount").GetInt32());

        o = await AddResultAsync(d.Token, pid, id, Result("HDL", 30, 40, 60));
        Assert.Equal("Low", FlagOf(o, 1));
        o = await AddResultAsync(d.Token, pid, id, Result("TG", 100, 0, 100)); // equal to high bound
        Assert.Equal("None", FlagOf(o, 2));
        o = await AddResultAsync(d.Token, pid, id, Result("Glu", 40, 40, 60)); // equal to low bound
        Assert.Equal("None", FlagOf(o, 3));
        o = await AddResultAsync(d.Token, pid, id, Result("Na", 999, null, null)); // no range: never flagged
        Assert.Equal("None", FlagOf(o, 4));
        o = await AddResultAsync(d.Token, pid, id, Result("K", 9, null, 5)); // only a high bound
        Assert.Equal("High", FlagOf(o, 5));
        o = await AddResultAsync(d.Token, pid, id, Result("Ca", -3, 0, null)); // only a low bound, negative value
        Assert.Equal("Low", FlagOf(o, 6));

        var list = await _f.ClientFor(d.Token).GetFromJsonAsync<JsonElement>(Url(pid));
        Assert.Equal(4, list.GetProperty("abnormalCount").GetInt32());
    }

    [Fact]
    public async Task Correcting_a_result_recomputes_the_flag_and_removing_the_last_result_reopens_the_order()
    {
        var (d, pid) = await SetupAsync();
        var c = _f.ClientFor(d.Token);
        var id = await CreateOrderAsync(d.Token, pid);
        var o = await AddResultAsync(d.Token, pid, id, Result("LDL", 160, 0, 100));
        var rid = o.GetProperty("results")[0].GetProperty("id").GetInt32();

        var put = await c.PutAsJsonAsync(Url(pid, $"/{id}/results/{rid}"), Result("LDL", 90, 0, 100));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal("None", FlagOf(await put.Content.ReadFromJsonAsync<JsonElement>(), 0));

        var del = await c.DeleteAsync(Url(pid, $"/{id}/results/{rid}"));
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);
        var after = await del.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Ordered", after.GetProperty("status").GetString());
        Assert.Equal(0, after.GetProperty("results").GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await c.DeleteAsync(Url(pid, $"/{id}/results/{rid}"))).StatusCode);
    }

    [Fact]
    public async Task Reference_low_greater_than_high_is_rejected()
    {
        var (d, pid) = await SetupAsync();
        var id = await CreateOrderAsync(d.Token, pid);
        var res = await _f.ClientFor(d.Token).PostAsJsonAsync(Url(pid, $"/{id}/results"), Result("LDL", 5, 10, 1));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var list = await _f.ClientFor(d.Token).GetFromJsonAsync<JsonElement>(Url(pid));
        Assert.Equal(0, list.GetProperty("orders")[0].GetProperty("results").GetArrayLength());
    }

    [Fact]
    public async Task Result_field_limits_and_missing_value_are_rejected()
    {
        var (d, pid) = await SetupAsync();
        var c = _f.ClientFor(d.Token);
        var id = await CreateOrderAsync(d.Token, pid);
        var url = Url(pid, $"/{id}/results");
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(url, new { analyteName = "", value = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(url, new { analyteName = new string('a', 101), value = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(url, new { analyteName = "A", value = 1, unit = new string('u', 31) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(url, new { analyteName = "A" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(url, new { analyteName = "A", value = 1_000_000_001m })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync(url, new { analyteName = new string('a', 100), value = 1, unit = new string('u', 30) })).StatusCode);
    }

    [Fact]
    public async Task Cancelled_orders_reject_result_changes_and_do_not_count_as_abnormal()
    {
        var (d, pid) = await SetupAsync();
        var c = _f.ClientFor(d.Token);
        var id = await CreateOrderAsync(d.Token, pid);
        var o = await AddResultAsync(d.Token, pid, id, Result("LDL", 160, 0, 100));
        var rid = o.GetProperty("results")[0].GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync(Url(pid, $"/{id}/cancel"), null)).StatusCode);

        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(Url(pid, $"/{id}/results"), Result())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync(Url(pid, $"/{id}/results/{rid}"), Result())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.DeleteAsync(Url(pid, $"/{id}/results/{rid}"))).StatusCode);
        var list = await c.GetFromJsonAsync<JsonElement>(Url(pid));
        Assert.Equal(0, list.GetProperty("abnormalCount").GetInt32());
        Assert.Equal("Cancelled", list.GetProperty("orders")[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task An_order_can_have_at_most_50_results()
    {
        var (d, pid) = await SetupAsync();
        var c = _f.ClientFor(d.Token);
        var id = await CreateOrderAsync(d.Token, pid);
        for (var i = 0; i < 50; i++)
            Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync(Url(pid, $"/{id}/results"), Result($"A{i}", 1, null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Url(pid, $"/{id}/results"), Result("extra"))).StatusCode);
    }

    // ── Access control and isolation ──────────────────────────────────────────
    [Fact]
    public async Task Another_doctor_gets_404_on_every_route_and_changes_nothing()
    {
        var (d, pid) = await SetupAsync();
        var other = await _f.RegisterDoctorAsync($"other-{Guid.NewGuid():N}@x.com");
        var id = await CreateOrderAsync(d.Token, pid);
        var o = await AddResultAsync(d.Token, pid, id, Result());
        var rid = o.GetProperty("results")[0].GetProperty("id").GetInt32();
        var x = _f.ClientFor(other.Token);

        Assert.Equal(HttpStatusCode.NotFound, (await x.GetAsync(Url(pid))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await x.PostAsJsonAsync(Url(pid), Order())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await x.PutAsJsonAsync(Url(pid, $"/{id}"), Order("hijack"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await x.PostAsync(Url(pid, $"/{id}/cancel"), null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await x.DeleteAsync(Url(pid, $"/{id}"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await x.PostAsJsonAsync(Url(pid, $"/{id}/results"), Result())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await x.PutAsJsonAsync(Url(pid, $"/{id}/results/{rid}"), Result())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await x.DeleteAsync(Url(pid, $"/{id}/results/{rid}"))).StatusCode);

        // Same answer as for a patient that does not exist, and nothing changed or was audited for the owner
        Assert.Equal(HttpStatusCode.NotFound, (await x.GetAsync(Url(999999))).StatusCode);
        var list = await _f.ClientFor(d.Token).GetFromJsonAsync<JsonElement>(Url(pid));
        Assert.Equal("Lipid panel", list.GetProperty("orders")[0].GetProperty("testName").GetString());
        Assert.Equal(1, list.GetProperty("orders")[0].GetProperty("results").GetArrayLength());
        var foreign = await _f.WithDbAsync(db => db.AuditEvents.CountAsync(e => e.PatientId == pid && e.ActorUserId == other.UserId));
        Assert.Equal(0, foreign);
    }

    [Fact]
    public async Task An_order_id_from_another_patient_of_the_same_doctor_is_404()
    {
        var (d, pid) = await SetupAsync();
        var pid2 = await _f.CreatePatientAsync(d.Token, $"pat2-{Guid.NewGuid():N}@x.com");
        var id = await CreateOrderAsync(d.Token, pid);
        var c = _f.ClientFor(d.Token);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PutAsJsonAsync(Url(pid2, $"/{id}"), Order())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsJsonAsync(Url(pid2, $"/{id}/results"), Result())).StatusCode);
    }

    [Fact]
    public async Task Patient_portal_tokens_and_anonymous_callers_are_refused()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var d = await _f.RegisterDoctorAsync($"doc-{tag}@x.com");
        var pid = await _f.CreatePatientAsync(d.Token, $"portal-{tag}@x.com");
        var id = await CreateOrderAsync(d.Token, pid);
        await AddResultAsync(d.Token, pid, id, Result());
        var patient = await _f.OnboardPatientAsync(d.Token, pid, $"portal-{tag}@x.com");
        var pc = _f.ClientFor(patient.Token);

        Assert.Equal(HttpStatusCode.Forbidden, (await pc.GetAsync(Url(pid))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await pc.PostAsJsonAsync(Url(pid), Order())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await pc.PostAsJsonAsync(Url(pid, $"/{id}/results"), Result())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await pc.DeleteAsync(Url(pid, $"/{id}"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.ClientFor().GetAsync(Url(pid))).StatusCode);

        // Lab data does not leak into any portal section
        foreach (var section in new[] { "me", "appointments", "prescriptions", "invoices", "attachments", "notes" })
        {
            var res = await pc.GetAsync($"/api/portal/{section}");
            if (res.IsSuccessStatusCode) Assert.DoesNotContain("Lipid panel", await res.Content.ReadAsStringAsync());
        }
    }

    // ── Audit ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Views_and_changes_are_audited_without_values()
    {
        var (d, pid) = await SetupAsync();
        var c = _f.ClientFor(d.Token);
        var id = await CreateOrderAsync(d.Token, pid, Order("SECRET-TEST-NAME", "SECRET-NOTE"));
        var o = await AddResultAsync(d.Token, pid, id, Result("SECRET-ANALYTE", 123456, 0, 100));
        var rid = o.GetProperty("results")[0].GetProperty("id").GetInt32();
        await c.PutAsJsonAsync(Url(pid, $"/{id}/results/{rid}"), Result("SECRET-ANALYTE", 98765, 0, 100));
        await c.GetAsync(Url(pid));
        await c.DeleteAsync(Url(pid, $"/{id}/results/{rid}"));

        var events = await _f.WithDbAsync(db => db.AuditEvents
            .Where(e => e.PatientId == pid && e.ItemKind == AuditItemKind.LabOrder).OrderBy(e => e.Id).ToListAsync());
        Assert.Equal(5, events.Count);
        Assert.Equal(new[] { AuditAction.Change, AuditAction.Change, AuditAction.Change, AuditAction.View, AuditAction.Change },
            events.Select(e => e.Action));
        Assert.All(events, e => Assert.Equal(d.UserId, e.ActorUserId));
        Assert.Contains("Value", events[2].ChangedFields);

        var log = await c.GetStringAsync($"/api/patients/{pid}/audit-log");
        Assert.Contains("LabOrder", log);
        foreach (var secret in new[] { "SECRET-TEST-NAME", "SECRET-NOTE", "SECRET-ANALYTE", "123456", "98765" })
            Assert.DoesNotContain(secret, log);
    }
}
