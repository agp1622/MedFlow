using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MedFlow.Api.Tests;

/// <summary>The vitals trends view reads GET /api/vitalsigns/patient/{id}; lock down who may call it.</summary>
public class VitalsAccessTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _f;
    public VitalsAccessTests(TestApiFactory f) => _f = f;

    private async Task<(AuthResult owner, int pid)> SetupAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var owner = await _f.RegisterDoctorAsync($"doc-{tag}@x.com");
        return (owner, await _f.CreatePatientAsync(owner.Token, $"pat-{tag}@x.com"));
    }

    private async Task RecordAsync(string token, int pid, string bp, int hr)
    {
        var res = await _f.ClientFor(token).PostAsJsonAsync("/api/vitalsigns",
            new { patientId = pid, bloodPressure = bp, heartRate = hr, weight = 80, height = 180 });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Owner_lists_own_patients_vitals_newest_first()
    {
        var (owner, pid) = await SetupAsync();
        await RecordAsync(owner.Token, pid, "120/80", 70);
        await Task.Delay(20);
        await RecordAsync(owner.Token, pid, "130/85", 75);

        var list = await _f.ClientFor(owner.Token).GetFromJsonAsync<JsonElement>($"/api/vitalsigns/patient/{pid}");
        Assert.Equal(2, list.GetArrayLength());
        Assert.Equal("130/85", list[0].GetProperty("bloodPressure").GetString());
        Assert.Equal("120/80", list[1].GetProperty("bloodPressure").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("/latest")]
    public async Task Other_doctor_cannot_read_vitals_of_a_patient_they_do_not_own(string tail)
    {
        var (owner, pid) = await SetupAsync();
        await RecordAsync(owner.Token, pid, "120/80", 70);
        var other = await _f.RegisterDoctorAsync($"other-{Guid.NewGuid():N}@x.com");

        var res = await _f.ClientFor(other.Token).GetAsync($"/api/vitalsigns/patient/{pid}{tail}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Other_doctor_cannot_record_vitals_for_a_patient_they_do_not_own()
    {
        var (_, pid) = await SetupAsync();
        var other = await _f.RegisterDoctorAsync($"other-{Guid.NewGuid():N}@x.com");

        var res = await _f.ClientFor(other.Token).PostAsJsonAsync("/api/vitalsigns",
            new { patientId = pid, bloodPressure = "120/80" });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
