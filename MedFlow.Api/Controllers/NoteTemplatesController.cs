using MedFlow.Api.Extensions;
using MedFlow.Core;
using MedFlow.Core.DTOs;
using MedFlow.Core.Entities;
using MedFlow.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Doctor)]
public class NoteTemplatesController : ControllerBase
{
    public const int MaxNameLength = 100;
    public const int MaxBodyLength = 5000;
    private const string BuiltInName = "SOAP";
    private const string BuiltInBody =
        "Subjective:\n\n\nObjective:\n\n\nAssessment:\n\n\nPlan:\n";

    private readonly INoteTemplateRepository _templates;
    public NoteTemplatesController(INoteTemplateRepository templates) => _templates = templates;

    [HttpGet]
    public async Task<ActionResult<PagedResult<NoteTemplateDto>>> GetAll([FromQuery] QueryParams q)
        => Ok(await _templates.GetPagedAsync(User.GetUserId(), q));

    [HttpGet("builtin")]
    public ActionResult<IEnumerable<NoteTemplateDto>> GetBuiltIn()
        => Ok(new[] { new NoteTemplateDto(0, BuiltInName, BuiltInBody, true, null) });

    [HttpPost]
    public async Task<ActionResult<NoteTemplateDto>> Create([FromBody] CreateNoteTemplateRequest req)
    {
        var doctorId = User.GetUserId();
        var (name, error) = Validate(req.Name, req.Body);
        if (error != null) return BadRequest(new { message = error });
        if (await IsDuplicate(doctorId, name!, null)) return Conflict(new { message = "A template with this name already exists." });

        var created = await _templates.AddAsync(new NoteTemplate { DoctorId = doctorId, Name = name!, Body = req.Body });
        return Ok(ToDto(created));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<NoteTemplateDto>> Update(int id, [FromBody] UpdateNoteTemplateRequest req)
    {
        var doctorId = User.GetUserId();
        var template = await _templates.GetWithOwnerCheckAsync(id, doctorId);
        if (template == null) return NotFound();
        var (name, error) = Validate(req.Name, req.Body);
        if (error != null) return BadRequest(new { message = error });
        if (await IsDuplicate(doctorId, name!, id)) return Conflict(new { message = "A template with this name already exists." });

        template.Name = name!;
        template.Body = req.Body;
        template.UpdatedAt = DateTime.UtcNow;
        await _templates.UpdateAsync(template);
        return Ok(ToDto(template));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var template = await _templates.GetWithOwnerCheckAsync(id, User.GetUserId());
        if (template == null) return NotFound();
        await _templates.DeleteAsync(id);
        return NoContent();
    }

    private async Task<bool> IsDuplicate(string doctorId, string name, int? excludeId)
        => name.Equals(BuiltInName, StringComparison.OrdinalIgnoreCase)
           || await _templates.NameExistsAsync(doctorId, name, excludeId);

    private static (string? Name, string? Error) Validate(string? name, string? body)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return (null, "Name is required.");
        if (trimmed.Length > MaxNameLength) return (null, $"Name must be at most {MaxNameLength} characters.");
        if (string.IsNullOrWhiteSpace(body)) return (null, "Body is required.");
        if (body.Length > MaxBodyLength) return (null, $"Body must be at most {MaxBodyLength} characters.");
        return (trimmed, null);
    }

    private static NoteTemplateDto ToDto(NoteTemplate t) => new(t.Id, t.Name, t.Body, false, t.UpdatedAt);
}
