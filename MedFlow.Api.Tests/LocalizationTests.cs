using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Text.Json;
using MedFlow.Api.Localization;

namespace MedFlow.Api.Tests;

/// <summary>Spec 040: API messages follow Accept-Language (Spanish default); nothing else about a response changes.</summary>
public class LocalizationTests : IClassFixture<TestApiFactory>
{
    private const string InvalidCredentialsEs = "Credenciales no válidas.";
    private const string InvalidCredentialsEn = "Invalid credentials.";

    private readonly TestApiFactory _f;
    public LocalizationTests(TestApiFactory f) => _f = f;

    private HttpClient Client(string? acceptLanguage, string? token = null)
    {
        var c = _f.ClientFor(token);
        if (acceptLanguage != null) c.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", acceptLanguage);
        return c;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    [Theory]
    [InlineData(null, InvalidCredentialsEs)]
    [InlineData("es", InvalidCredentialsEs)]
    [InlineData("es-MX", InvalidCredentialsEs)]
    [InlineData("en", InvalidCredentialsEn)]
    [InlineData("en-US,en;q=0.9", InvalidCredentialsEn)]
    [InlineData("fr", InvalidCredentialsEs)]
    [InlineData("fr-FR, en;q=0.8", InvalidCredentialsEn)]
    [InlineData("en;q=0.5, es;q=0.9", InvalidCredentialsEs)]
    [InlineData("*", InvalidCredentialsEs)]
    [InlineData(";;;not a language;;;", InvalidCredentialsEs)]
    public async Task Server_message_follows_the_requested_language_with_Spanish_fallback(string? header, string expected)
    {
        var res = await Client(header).PostAsJsonAsync("/api/auth/login", new { email = "nobody@x.com", password = "Whatever1" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.Equal(expected, (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Language_never_changes_status_code_or_response_shape()
    {
        foreach (var path in new[] { "/api/auth/login" })
        {
            var es = await Client("es").PostAsJsonAsync(path, new { email = "nobody@x.com", password = "Whatever1" });
            var en = await Client("en").PostAsJsonAsync(path, new { email = "nobody@x.com", password = "Whatever1" });
            Assert.Equal(es.StatusCode, en.StatusCode);
            Assert.Equal(
                (await Json(es)).EnumerateObject().Select(p => p.Name).OrderBy(n => n),
                (await Json(en)).EnumerateObject().Select(p => p.Name).OrderBy(n => n));
        }
    }

    [Theory]
    [InlineData("es", "Si ese correo está registrado")]
    [InlineData("en", "If that email is registered")]
    public async Task Password_reset_request_is_uniform_for_known_and_unknown_emails_in_both_languages(string lang, string expectedStart)
    {
        await _f.RegisterDoctorAsync($"loc-reset-{lang}@x.com");
        var known = await Client(lang).PostAsJsonAsync("/api/auth/forgot-password", new { email = $"loc-reset-{lang}@x.com" });
        var unknown = await Client(lang).PostAsJsonAsync("/api/auth/forgot-password", new { email = $"loc-nobody-{lang}@x.com" });
        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(known.StatusCode, unknown.StatusCode);
        var a = (await Json(known)).GetProperty("message").GetString()!;
        var b = (await Json(unknown)).GetProperty("message").GetString()!;
        Assert.Equal(a, b);
        Assert.StartsWith(expectedStart, a);
    }

    [Theory]
    [InlineData("es", "Este enlace no es válido o ha caducado.")]
    [InlineData("en", "This link is invalid or has expired.")]
    public async Task Public_intake_page_errors_follow_the_language(string lang, string expected)
    {
        var res = await Client(lang).GetAsync("/api/intake/not-a-real-token");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal(expected, (await Json(res)).GetProperty("error").GetString());
    }

    [Theory]
    [InlineData("es", "Este enlace no es válido.")]
    [InlineData("en", "This link is not valid.")]
    public async Task Public_appointment_response_errors_follow_the_language(string lang, string expected)
    {
        var res = await Client(lang).PostAsJsonAsync("/api/appointment-response/lookup", new { token = "nope" });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal(expected, (await Json(res)).GetProperty("error").GetString());
    }

    [Theory]
    [InlineData("es", "Esta invitación no es válida o ha caducado.")]
    [InlineData("en", "This invitation is invalid or has expired.")]
    public async Task Invitation_errors_follow_the_language(string lang, string expected)
    {
        var res = await Client(lang).PostAsJsonAsync("/api/auth/accept-invitation",
            new { token = "nope", email = "x@x.com", password = TestApiFactory.Password, confirmPassword = TestApiFactory.Password });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal(expected, (await Json(res)).GetProperty("error").GetString());
    }

    [Theory]
    [InlineData("es", "La contraseña debe tener al menos 8 caracteres.")]
    [InlineData("en", "Passwords must be at least 8 characters.")]
    public async Task Identity_password_errors_are_localized_and_never_raw(string lang, string expected)
    {
        var res = await Client(lang).PostAsJsonAsync("/api/auth/register",
            new { email = $"loc-weak-{lang}@x.com", password = "Ab1", firstName = "A", lastName = "B", specialty = "GP" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var errors = (await Json(res)).GetProperty("errors").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains(expected, errors);
    }

    [Theory]
    [InlineData("es", "Se han producido uno o más errores de validación.")]
    [InlineData("en", "One or more validation errors occurred.")]
    public async Task Automatic_model_validation_title_follows_the_language_and_keeps_the_problem_shape(string lang, string title)
    {
        var res = await Client(lang).PostAsJsonAsync("/api/auth/login", new { });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var j = await Json(res);
        Assert.Equal(title, j.GetProperty("title").GetString());
        Assert.Equal(400, j.GetProperty("status").GetInt32());
        Assert.True(j.TryGetProperty("errors", out _));
    }

    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    public async Task Doctor_only_routes_still_reject_patient_tokens_in_every_language(string lang)
    {
        var doctor = await _f.RegisterDoctorAsync($"loc-iso-{lang}@x.com");
        var pid = await _f.CreatePatientAsync(doctor.Token, $"loc-pat-{lang}@x.com");
        var patient = await _f.OnboardPatientAsync(doctor.Token, pid, $"loc-pat-{lang}@x.com");
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(lang, patient.Token).GetAsync("/api/patients")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client(lang).GetAsync("/api/patients")).StatusCode);
    }

    [Fact]
    public async Task Doctor_entered_text_is_stored_and_returned_unchanged_whatever_the_language()
    {
        var d = await _f.RegisterDoctorAsync("loc-content@x.com");
        const string name = "Plantilla cardio / Cardio template";
        const string body = "Subjective: dolor de pecho\nObjective: BP 120/80\nPlan: seguimiento en 2 semanas";

        foreach (var lang in new[] { "es", "en" })
        {
            var res = await Client(lang, d.Token).PostAsJsonAsync("/api/notetemplates", new { name = name + lang, body });
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }
        foreach (var lang in new[] { "es", "en" })
        {
            var list = await Client(lang, d.Token).GetFromJsonAsync<JsonElement>("/api/notetemplates");
            var items = list.GetProperty("items").EnumerateArray().ToList();
            foreach (var l in new[] { "es", "en" })
            {
                var t = items.Single(i => i.GetProperty("name").GetString() == name + l);
                Assert.Equal(body, t.GetProperty("body").GetString());
            }
        }

        var pid = await _f.CreatePatientAsync(d.Token, "loc-content-pat@x.com", "José");
        foreach (var lang in new[] { "es", "en" })
        {
            var note = await Client(lang, d.Token).PostAsJsonAsync("/api/medicalnotes",
                new { patientId = pid, content = "Paciente refiere cefalea. Patient reports headache.", visitType = "Seguimiento" });
            Assert.Equal(HttpStatusCode.OK, note.StatusCode);
            var notes = await Client(lang, d.Token).GetFromJsonAsync<JsonElement>($"/api/medicalnotes/patient/{pid}");
            Assert.All(notes.EnumerateArray(), n =>
            {
                Assert.Equal("Paciente refiere cefalea. Patient reports headache.", n.GetProperty("content").GetString());
                Assert.Equal("Seguimiento", n.GetProperty("visitType").GetString());
            });
        }
        var patient = await Client("en", d.Token).GetFromJsonAsync<JsonElement>($"/api/patients/{pid}");
        Assert.Equal("José", patient.GetProperty("firstName").GetString());
    }

    [Fact]
    public async Task Template_validation_messages_are_localized()
    {
        var d = await _f.RegisterDoctorAsync("loc-tplval@x.com");
        var es = await Client("es", d.Token).PostAsJsonAsync("/api/notetemplates", new { name = " ", body = "x" });
        var en = await Client("en", d.Token).PostAsJsonAsync("/api/notetemplates", new { name = " ", body = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, es.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, en.StatusCode);
        Assert.Equal("El nombre es obligatorio.", (await Json(es)).GetProperty("message").GetString());
        Assert.Equal("Name is required.", (await Json(en)).GetProperty("message").GetString());
    }

    [Fact]
    public void Catalog_has_both_languages_with_the_same_placeholders_for_every_message()
    {
        var placeholders = new Regex(@"\{\d+\}");
        foreach (var (key, (es, en)) in Messages.Catalog)
        {
            Assert.False(string.IsNullOrWhiteSpace(es), $"{key} has no Spanish text");
            Assert.False(string.IsNullOrWhiteSpace(en), $"{key} has no English text");
            Assert.Equal(
                placeholders.Matches(es).Select(m => m.Value).OrderBy(x => x),
                placeholders.Matches(en).Select(m => m.Value).OrderBy(x => x));
        }
    }

    [Fact]
    public void Unknown_message_key_falls_back_to_a_generic_localized_message()
    {
        Assert.Equal("Ocurrió un error inesperado.", Localizer.Get("es", "No.Such.Key"));
        Assert.Equal("An unexpected error occurred.", Localizer.Get("en", "No.Such.Key"));
    }
}
