using MedFlow.Api.Authorization;
using MedFlow.Api.Localization;
using MedFlow.Core;
using MedFlow.Core.DTOs;
using MedFlow.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace MedFlow.Api.Controllers;

/// <summary>The caller's own clinic. Readable by every active staff member; renaming is an Owner action.</summary>
[ApiController]
[Route("api/clinic")]
[HasPermission(Permission.ClinicRead)]
public class ClinicController : ControllerBase
{
    private readonly IClinicService _clinics;
    public ClinicController(IClinicService clinics) => _clinics = clinics;

    [HttpGet]
    public async Task<ActionResult<ClinicDto>> Get()
    {
        var dto = await _clinics.GetClinicAsync(this.GetScope());
        return dto == null ? NotFound() : Ok(dto);
    }

    [HttpPut]
    [HasPermission(Permission.StaffManage)]
    public async Task<ActionResult<ClinicDto>> Rename([FromBody] RenameClinicRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || req.Name.Trim().Length > 200)
            return BadRequest(new { errors = new[] { this.T("Clinic.NameRequired") } });
        var dto = await _clinics.RenameAsync(this.GetScope(), req.Name);
        return dto == null ? NotFound() : Ok(dto);
    }

    /// <summary>Active Owners and Doctors that can be the treating doctor of a patient or appointment.</summary>
    [HttpGet("doctors")]
    public async Task<ActionResult<IReadOnlyList<ClinicDoctorDto>>> Doctors() =>
        Ok(await _clinics.GetDoctorsAsync(this.GetScope()));
}
