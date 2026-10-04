namespace MedFlow.Api.Localization;

/// <summary>
/// User-facing API messages by id, in every supported language. Spanish is the default.
/// Positional arguments use string.Format placeholders such as {0}.
/// </summary>
public static class Messages
{
    public const string DefaultLanguage = "es";
    public static readonly string[] SupportedLanguages = { "es", "en" };

    public static readonly IReadOnlyDictionary<string, (string Es, string En)> Catalog = new Dictionary<string, (string, string)>
    {
        // Generic
        ["Error.Unexpected"] = ("Ocurrió un error inesperado.", "An unexpected error occurred."),
        ["Error.AccessDenied"] = ("Acceso denegado.", "Access denied."),
        ["Error.BadRequest"] = ("La solicitud no es válida.", "The request is not valid."),
        ["Error.NotFound"] = ("No se encontró el recurso solicitado.", "The requested resource was not found."),
        ["Error.NotAuthenticated"] = ("Usuario no autenticado.", "User not authenticated."),
        ["Error.TooManyRequests"] = ("Demasiadas solicitudes. Inténtelo de nuevo más tarde.", "Too many requests. Please try again later."),
        ["Error.Validation"] = ("Se han producido uno o más errores de validación.", "One or more validation errors occurred."),
        ["Error.ReasonMax"] = ("El motivo debe tener como máximo {0} caracteres.", "Reason must be at most {0} characters."),

        // Identity
        ["Identity.PasswordTooShort"] = ("La contraseña debe tener al menos {0} caracteres.", "Passwords must be at least {0} characters."),
        ["Identity.PasswordRequiresDigit"] = ("La contraseña debe contener al menos un dígito.", "Passwords must have at least one digit."),
        ["Identity.PasswordRequiresUpper"] = ("La contraseña debe contener al menos una mayúscula.", "Passwords must have at least one uppercase letter."),
        ["Identity.PasswordRequiresLower"] = ("La contraseña debe contener al menos una minúscula.", "Passwords must have at least one lowercase letter."),
        ["Identity.PasswordRequiresNonAlphanumeric"] = ("La contraseña debe contener al menos un carácter especial.", "Passwords must have at least one non alphanumeric character."),
        ["Identity.PasswordRequiresUniqueChars"] = ("La contraseña debe contener más caracteres distintos.", "Passwords must use more unique characters."),
        ["Identity.DuplicateEmail"] = ("Ese correo electrónico ya está registrado.", "That email is already registered."),
        ["Identity.InvalidEmail"] = ("El correo electrónico no es válido.", "The email is not valid."),
        ["Identity.Default"] = ("No se pudo completar la operación.", "The operation could not be completed."),

        // Auth
        ["Auth.EmailRegistered"] = ("El correo electrónico ya está registrado.", "Email already registered."),
        ["Auth.InvalidCredentials"] = ("Credenciales no válidas.", "Invalid credentials."),
        ["Auth.AccountLocked"] = ("Cuenta bloqueada. Inténtelo de nuevo más tarde.", "Account locked. Try again later."),
        ["Auth.PatientAccountGoogle"] = (
            "Las cuentas de paciente inician sesión con correo y contraseña. Use la pestaña Paciente y las credenciales que creó con su invitación.",
            "Patient accounts sign in with email and password. Use the Patient tab and the credentials you set from your invitation."),
        ["Auth.InvalidGoogleToken"] = ("Token de Google no válido.", "Invalid Google token."),
        ["TwoFactor.InvalidCode"] = ("Código no válido.", "Invalid code."),
        ["TwoFactor.InvalidConfirmation"] = ("Credenciales o código no válidos.", "Invalid credentials or code."),
        ["TwoFactor.AlreadyEnabled"] = ("La verificación en dos pasos ya está activada.", "Two-factor authentication is already enabled."),
        ["TwoFactor.NotEnabled"] = ("La verificación en dos pasos no está activada.", "Two-factor authentication is not enabled."),
        ["Auth.InvalidInvitation"] = ("Esta invitación no es válida o ha caducado.", "This invitation is invalid or has expired."),
        ["Auth.PasswordsMismatch"] = ("Las contraseñas no coinciden.", "Passwords do not match."),
        ["Auth.ValidEmailRequired"] = ("Se requiere un correo electrónico válido.", "A valid email is required."),
        ["Auth.ResetRequested"] = (
            "Si ese correo está registrado, se ha enviado un enlace para restablecer la contraseña.",
            "If that email is registered, a password reset link has been sent."),
        ["Auth.InvalidResetLink"] = (
            "Este enlace de restablecimiento no es válido o ha caducado. Solicite uno nuevo.",
            "This reset link is invalid or has expired. Please request a new one."),
        ["Auth.PasswordReset"] = ("Su contraseña se ha restablecido. Ya puede iniciar sesión.", "Your password has been reset. You can now sign in."),

        // Clinic and staff
        ["Staff.Invited"] = (
            "Si el correo puede ser invitado, se ha enviado una invitación.",
            "If the address can be invited, an invitation has been sent."),
        ["Staff.EmailFailed"] = ("No se pudo enviar la invitación por correo. Inténtelo de nuevo.", "The invitation could not be emailed. Please try again."),
        ["Staff.InvalidRole"] = ("El rol de la invitación debe ser Médico, Enfermería o Recepción.", "The invitation role must be Doctor, Nurse or Receptionist."),
        ["Staff.LastOwner"] = ("Debe quedar al menos un propietario activo en la clínica.", "The clinic must keep at least one active owner."),
        ["Staff.NameRequired"] = ("Indique nombre y apellido.", "Provide a first and last name."),
        ["Clinic.NameRequired"] = ("Indique el nombre de la clínica (máximo 200 caracteres).", "Provide the clinic name (at most 200 characters)."),

        // Availability
        ["Availability.MaxWindows"] = ("Indique como máximo {0} franjas.", "Provide at most {0} windows."),
        ["Availability.WindowInvalid"] = ("Las franjas necesitan un día válido y horas de inicio y fin en formato HH:mm.", "Windows need a valid day and HH:mm start and end times."),
        ["Availability.EndAfterStart"] = ("La hora de fin debe ser posterior a la de inicio.", "End time must be after start time."),
        ["Availability.Boundaries"] = ("Las horas deben ser múltiplos de 30 minutos.", "Times must be on 30-minute boundaries."),
        ["Availability.Overlap"] = ("Las franjas del mismo día no pueden solaparse.", "Windows on the same day must not overlap."),
        ["Availability.PastDate"] = ("La fecha bloqueada no puede estar en el pasado.", "Blocked date cannot be in the past."),
        ["Availability.LabelMax"] = ("La etiqueta debe tener como máximo {0} caracteres.", "Label must be at most {0} characters."),
        ["Availability.AlreadyBlocked"] = ("Esa fecha ya está bloqueada.", "That date is already blocked."),

        // Invitations and intake links (doctor side)
        ["Invite.NoEmail"] = ("El paciente no tiene un correo electrónico registrado.", "Patient has no email on file."),
        ["Invite.OnlyActive"] = ("Solo se puede invitar a pacientes activos.", "Only active patients can be invited."),
        ["Invite.HasAccess"] = ("El paciente ya tiene acceso al portal.", "Patient already has portal access."),
        ["Invite.EmailFailed"] = ("No se pudo enviar la invitación por correo. Inténtelo de nuevo.", "The invitation could not be emailed. Please try again."),
        ["Invite.Sent"] = ("Invitación enviada.", "Invitation sent."),
        ["IntakeLink.OnlyActive"] = ("Solo se puede enviar el formulario de ingreso a pacientes activos.", "Only active patients can be sent an intake form."),
        ["IntakeLink.EmailFailed"] = ("No se pudo enviar por correo el enlace del formulario de ingreso. Inténtelo de nuevo.", "The intake form link could not be emailed. Please try again."),
        ["IntakeLink.Sent"] = ("Enlace del formulario de ingreso enviado.", "Intake form link sent."),
        ["IntakeLink.AlreadyDecided"] = ("Este envío ya fue resuelto.", "This submission has already been decided."),

        // Public intake form
        ["Intake.InvalidLink"] = ("Este enlace no es válido o ha caducado.", "This link is invalid or has expired."),
        ["Intake.Thanks"] = ("Gracias. Su formulario se ha enviado.", "Thank you. Your form has been submitted."),
        ["Intake.Required"] = ("{0} es obligatorio.", "{0} is required."),
        ["Intake.MaxLength"] = ("{0} debe tener como máximo {1} caracteres.", "{0} must be at most {1} characters."),
        ["Intake.DobRequired"] = ("La fecha de nacimiento es obligatoria.", "Date of birth is required."),
        ["Intake.DobInvalid"] = ("La fecha de nacimiento no es válida.", "Date of birth is not valid."),
        ["Intake.GenderRequired"] = ("El género es obligatorio.", "Gender is required."),
        ["Intake.ConsentRequired"] = ("Debe aceptar la declaración de consentimiento.", "You must agree to the consent statement."),
        ["Intake.Field.FirstName"] = ("El nombre", "First name"),
        ["Intake.Field.LastName"] = ("El apellido", "Last name"),
        ["Intake.Field.Phone"] = ("El teléfono", "Phone"),
        ["Intake.Field.Address"] = ("La dirección", "Address"),
        ["Intake.Field.City"] = ("La ciudad", "City"),
        ["Intake.Field.State"] = ("El estado o provincia", "State"),
        ["Intake.Field.ZipCode"] = ("El código postal", "ZIP code"),
        ["Intake.Field.InsuranceProvider"] = ("La aseguradora", "Insurance provider"),
        ["Intake.Field.InsurancePolicyNumber"] = ("El número de póliza", "Policy number"),
        ["Intake.Field.PrimaryCondition"] = ("La afección principal", "Primary condition"),
        ["Intake.Field.Allergies"] = ("Las alergias", "Allergies"),
        ["Intake.Field.CurrentMedications"] = ("Los medicamentos actuales", "Current medications"),
        ["Intake.Field.PastHistory"] = ("Los antecedentes", "Past history"),
        ["Intake.Field.AdditionalNotes"] = ("Las notas adicionales", "Additional notes"),
        ["Intake.Field.Signature"] = ("La firma", "Signature"),

        // Patient insurance
        ["Patient.Insurance.Field.Group"] = ("El número de grupo", "Group number"),
        ["Patient.Insurance.Field.PayerId"] = ("El ID del pagador", "Payer ID"),
        ["Patient.Insurance.Field.Subscriber"] = ("El nombre del titular", "Subscriber name"),
        ["Patient.Insurance.MaxLength"] = ("{0} debe tener como máximo {1} caracteres.", "{0} must be at most {1} characters."),
        ["Patient.Insurance.SubscriberDobInvalid"] = ("La fecha de nacimiento del titular no es válida.", "The subscriber's date of birth is not valid."),
        ["Patient.Insurance.RelationshipInvalid"] = ("La relación con el titular no es válida.", "The relationship to the subscriber is not valid."),

        // Claim export draft
        ["Claim.UnsupportedFormat"] = ("Formato no admitido. Use json o csv.", "Unsupported format. Use json or csv."),
        ["Claim.Disclaimer"] = (
            "BORRADOR de datos para reclamación con la numeración de casillas del CMS-1500 (02/12). NO es el formulario oficial CMS-1500, NO es un archivo X12 837 y NO ha sido validado por ningún pagador ni cámara de compensación. MedFlow no almacena códigos CPT/HCPCS, CIE-10, NPI ni identificación fiscal: esas casillas quedan vacías y figuran como faltantes. Revise y complete los datos antes de usarlos.",
            "DRAFT claim data using CMS-1500 (02/12) item numbers. This is NOT the official CMS-1500 form, NOT an X12 837 file, and has NOT been validated by any payer or clearinghouse. MedFlow does not store CPT/HCPCS, ICD-10, NPI or tax ID data: those items are left blank and listed as missing. Review and complete the data before using it."),
        ["Claim.Label.status"] = ("Estado", "Status"),
        ["Claim.Label.payer_name"] = ("Aseguradora (pagador)", "Payer (insurance carrier)"),
        ["Claim.Label.payer_id"] = ("ID del pagador", "Payer ID"),
        ["Claim.Label.insured_id_number"] = ("Número de póliza o ID del asegurado", "Insured's ID / policy number"),
        ["Claim.Label.patient_name"] = ("Nombre del paciente (apellido, nombre)", "Patient name (last, first)"),
        ["Claim.Label.patient_birth_date"] = ("Fecha de nacimiento del paciente", "Patient date of birth"),
        ["Claim.Label.patient_sex"] = ("Sexo del paciente (M/F)", "Patient sex (M/F)"),
        ["Claim.Label.patient_address"] = ("Dirección del paciente", "Patient address"),
        ["Claim.Label.patient_city"] = ("Ciudad del paciente", "Patient city"),
        ["Claim.Label.patient_state"] = ("Estado del paciente", "Patient state"),
        ["Claim.Label.patient_zip"] = ("Código postal del paciente", "Patient ZIP code"),
        ["Claim.Label.patient_phone"] = ("Teléfono del paciente", "Patient phone"),
        ["Claim.Label.patient_relationship_to_insured"] = ("Relación del paciente con el asegurado", "Patient relationship to insured"),
        ["Claim.Label.insured_name"] = ("Nombre del asegurado", "Insured's name"),
        ["Claim.Label.insured_policy_group"] = ("Grupo o número de grupo de la póliza", "Insured's policy group or FECA number"),
        ["Claim.Label.insured_birth_date"] = ("Fecha de nacimiento del asegurado", "Insured's date of birth"),
        ["Claim.Label.diagnosis_icd10"] = ("Diagnóstico (CIE-10 / ICD-10)", "Diagnosis (ICD-10)"),
        ["Claim.Label.service_date"] = ("Fecha del servicio", "Date of service"),
        ["Claim.Label.procedure_cpt_hcpcs"] = ("Procedimiento (CPT/HCPCS)", "Procedure (CPT/HCPCS)"),
        ["Claim.Label.service_charge"] = ("Cargo del servicio", "Service charge"),
        ["Claim.Label.service_units"] = ("Unidades", "Units"),
        ["Claim.Label.provider_npi"] = ("NPI del proveedor", "Provider NPI"),
        ["Claim.Label.federal_tax_id"] = ("Identificación fiscal federal", "Federal tax ID"),
        ["Claim.Label.total_charge"] = ("Cargo total", "Total charge"),
        ["Claim.Label.billing_provider_name"] = ("Proveedor de facturación", "Billing provider"),
        ["Claim.Label.billing_provider_phone"] = ("Teléfono del proveedor de facturación", "Billing provider phone"),
        ["Claim.Label.service_description"] = ("Descripción del servicio (factura)", "Service description (invoice)"),
        ["Claim.Label.invoice_number"] = ("Número de factura", "Invoice number"),
        ["Claim.Label.invoice_status"] = ("Estado de la factura", "Invoice status"),

        // Attachments
        ["Attachment.NoFile"] = ("No se subió ningún archivo.", "No file uploaded."),
        ["Attachment.TooLarge"] = ("El archivo supera el límite de 50 MB.", "File exceeds the 50 MB size limit."),
        ["Attachment.TypeNotAllowed"] = ("El tipo de archivo '{0}' no está permitido.", "File type '{0}' is not allowed."),
        ["Attachment.Missing"] = ("No se encontró el archivo en el disco.", "File not found on disk."),

        // Note templates
        ["Template.Duplicate"] = ("Ya existe una plantilla con este nombre.", "A template with this name already exists."),
        ["Template.NameRequired"] = ("El nombre es obligatorio.", "Name is required."),
        ["Template.NameMax"] = ("El nombre debe tener como máximo {0} caracteres.", "Name must be at most {0} characters."),
        ["Template.BodyRequired"] = ("El contenido es obligatorio.", "Body is required."),
        ["Template.BodyMax"] = ("El contenido debe tener como máximo {0} caracteres.", "Body must be at most {0} characters."),

        // Lab orders and results
        ["Lab.DateFuture"] = ("La fecha del pedido no puede ser futura.", "The order date cannot be in the future."),
        ["Lab.RangeOrder"] = ("El límite inferior del rango de referencia no puede ser mayor que el superior.", "The reference range low cannot be greater than the high."),
        ["Lab.OrderLimit"] = ("Un paciente puede tener como máximo {0} pedidos de laboratorio.", "A patient can have at most {0} lab orders."),
        ["Lab.ResultLimit"] = ("Un pedido puede tener como máximo {0} resultados.", "An order can have at most {0} results."),
        ["Lab.Cancelled"] = ("Este pedido está cancelado y no se puede modificar.", "This order is cancelled and cannot be changed."),

        // Appointment response (public)
        ["Response.Invalid"] = ("Este enlace no es válido.", "This link is not valid."),
        ["Response.Closed"] = ("Esta cita ya no se puede modificar.", "This appointment can no longer be changed."),

        // Patient portal
        ["Portal.Unavailable"] = ("El acceso al portal no está disponible.", "Portal access is unavailable."),
        ["Portal.RangeMax"] = ("Elija un intervalo de fechas de como máximo 31 días.", "Choose a date range of at most 31 days."),
        ["Portal.NotAvailable"] = ("Ese horario no está disponible para reservar.", "That time is not available for booking."),
        ["Portal.LimitReached"] = ("Ha alcanzado el límite de citas próximas.", "You have reached the limit of upcoming appointments."),
        // Waitlist
        ["Waitlist.NotActive"] = ("Solo los pacientes activos pueden estar en la lista de espera.", "Only active patients can join the waitlist."),
        ["Waitlist.AlreadyWaiting"] = ("El paciente ya está en la lista de espera.", "The patient is already on the waitlist."),
        ["Waitlist.NotOnList"] = ("No está en la lista de espera.", "You are not on the waitlist."),
        ["Waitlist.OfferInvalid"] = ("Este enlace no es válido o ha caducado.", "This link is not valid or has expired."),
        ["Waitlist.SlotGone"] = ("Ese horario ya no está disponible.", "That time is no longer available."),
        ["Portal.SlotTaken"] = ("Ese horario ya no está disponible.", "That time is no longer available."),

        // Audit log
        ["Audit.PageRange"] = ("La página debe ser al menos 1 y el tamaño de página entre 1 y {0}.", "Page must be at least 1 and page size between 1 and {0}."),
        ["Audit.DateRange"] = ("La fecha de inicio no puede ser posterior a la fecha de fin.", "The start date must not be after the end date."),
        ["Audit.UserTooLong"] = ("El filtro de usuario es demasiado largo.", "User filter is too long."),

        // Reports
        ["Reports.DateRange"] = ("La fecha de inicio no puede ser posterior a la fecha de fin.", "The start date must not be after the end date."),
        ["Reports.RangeTooLong"] = ("El rango de fechas no puede superar {0} días.", "The date range cannot exceed {0} days."),
        ["Reports.PageRange"] = ("La página debe ser al menos 1 y el tamaño de página entre 1 y {0}.", "Page must be at least 1 and page size between 1 and {0}."),
        ["Reports.Col.Period"] = ("Periodo", "Period"),
        ["Reports.Col.Revenue"] = ("Ingresos", "Revenue"),
        ["Reports.Col.InvoicesPaid"] = ("Facturas pagadas", "Invoices paid"),
        ["Reports.Col.Visits"] = ("Visitas", "Visits"),
        ["Reports.Col.Completed"] = ("Completadas", "Completed"),
        ["Reports.Col.NoShows"] = ("No asistió", "No-shows"),
        ["Reports.Col.NoShowRate"] = ("Tasa de inasistencia (%)", "No-show rate (%)"),
        ["Reports.Col.Invoice"] = ("Factura", "Invoice"),
        ["Reports.Col.Patient"] = ("Paciente", "Patient"),
        ["Reports.Col.InvoiceDate"] = ("Fecha de factura", "Invoice date"),
        ["Reports.Col.DueDate"] = ("Vencimiento", "Due date"),
        ["Reports.Col.DaysPastDue"] = ("Días de atraso", "Days past due"),
        ["Reports.Col.Bucket"] = ("Tramo", "Bucket"),
        ["Reports.Col.Balance"] = ("Saldo", "Balance"),
    };
}
