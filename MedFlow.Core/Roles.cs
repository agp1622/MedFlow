namespace MedFlow.Core;

/// <summary>Identity role names. Staff authorization does not use these: it comes from the clinic membership.</summary>
public static class Roles
{
    public const string Doctor = "Doctor";
    public const string Patient = "Patient";
}

/// <summary>A staff member's role inside their clinic.</summary>
public enum ClinicRole { Owner, Doctor, Nurse, Receptionist }

/// <summary>One thing a staff member may be allowed to do. Mapped to roles only in <see cref="PermissionMatrix"/>.</summary>
public enum Permission
{
    ClinicRead,
    DashboardRead,
    PatientsRead,
    PatientsWrite,
    PatientsDelete,
    PatientClinicalFields,
    AppointmentsRead,
    AppointmentsWrite,
    WaitlistManage,
    AvailabilityManage,
    IntakeLinkSend,
    IntakeReview,
    PortalInvite,
    PrescriptionsRead,
    PrescriptionsWrite,
    InvoicesRead,
    InvoicesWrite,
    VitalsRead,
    VitalsWrite,
    NotesRead,
    NotesWrite,
    NotesManage,
    NoteTemplates,
    AttachmentsRead,
    AttachmentsWrite,
    ClinicalListsRead,
    ClinicalListsWrite,
    LabsRead,
    LabsWrite,
    AuditLogRead,
    ReportsRead,
    StaffManage
}

/// <summary>
/// The single source of truth for what each clinic role may do (documented in
/// specs/045-clinic-roles/contracts/permission-matrix.md). A permission missing here is denied to everyone.
/// </summary>
public static class PermissionMatrix
{
    private const ClinicRole O = ClinicRole.Owner, D = ClinicRole.Doctor, N = ClinicRole.Nurse, R = ClinicRole.Receptionist;

    private static readonly Dictionary<Permission, ClinicRole[]> Grants = new()
    {
        [Permission.ClinicRead] = new[] { O, D, N, R },
        [Permission.DashboardRead] = new[] { O, D, N, R },
        [Permission.PatientsRead] = new[] { O, D, N, R },
        [Permission.PatientsWrite] = new[] { O, D, R },
        [Permission.PatientsDelete] = new[] { O, D },
        [Permission.PatientClinicalFields] = new[] { O, D, N },
        [Permission.AppointmentsRead] = new[] { O, D, N, R },
        [Permission.AppointmentsWrite] = new[] { O, D, R },
        [Permission.WaitlistManage] = new[] { O, D, R },
        [Permission.AvailabilityManage] = new[] { O, D },
        [Permission.IntakeLinkSend] = new[] { O, D, R },
        [Permission.IntakeReview] = new[] { O, D },
        [Permission.PortalInvite] = new[] { O, D, R },
        [Permission.PrescriptionsRead] = new[] { O, D, N },
        [Permission.PrescriptionsWrite] = new[] { O, D },
        [Permission.InvoicesRead] = new[] { O, D, R },
        [Permission.InvoicesWrite] = new[] { O, D, R },
        [Permission.VitalsRead] = new[] { O, D, N },
        [Permission.VitalsWrite] = new[] { O, D, N },
        [Permission.NotesRead] = new[] { O, D, N },
        [Permission.NotesWrite] = new[] { O, D, N },
        [Permission.NotesManage] = new[] { O, D },
        [Permission.NoteTemplates] = new[] { O, D, N },
        [Permission.AttachmentsRead] = new[] { O, D, N },
        [Permission.AttachmentsWrite] = new[] { O, D },
        [Permission.ClinicalListsRead] = new[] { O, D, N },
        [Permission.ClinicalListsWrite] = new[] { O, D },
        [Permission.LabsRead] = new[] { O, D, N },
        [Permission.LabsWrite] = new[] { O, D },
        [Permission.AuditLogRead] = new[] { O, D },
        [Permission.ReportsRead] = new[] { O, D },
        [Permission.StaffManage] = new[] { O },
    };

    public static bool Has(ClinicRole role, Permission permission) =>
        Grants.TryGetValue(permission, out var roles) && roles.Contains(role);

    public static IReadOnlyCollection<Permission> For(ClinicRole role) =>
        Grants.Where(g => g.Value.Contains(role)).Select(g => g.Key).ToList();

    public static IReadOnlyCollection<Permission> All => Grants.Keys;
}

/// <summary>
/// Who is asking and for which clinic. Produced only by the central authorization handler from the database
/// (never from the token or a request body) and passed to repositories, which filter every query by it.
/// </summary>
public sealed record ClinicScope(int ClinicId, string UserId, ClinicRole Role)
{
    public bool Has(Permission permission) => PermissionMatrix.Has(Role, permission);

    /// <summary>Owners see clinic-wide figures; everyone else is limited to their own doctor-linked records.</summary>
    public bool OwnDataOnly => Role != ClinicRole.Owner;

    /// <summary>True for roles that can be the treating doctor of a patient or appointment.</summary>
    public bool IsClinician => Role is ClinicRole.Owner or ClinicRole.Doctor;
}
