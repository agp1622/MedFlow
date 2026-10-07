# Data Model

**DoctorAvailability** (BaseEntity): `DoctorId` (string, FK to Doctor.UserId), `DayOfWeek` (enum string), `StartTime` (TimeOnly), `EndTime` (TimeOnly). Rules: End > Start, both multiples of 30 minutes, no overlap within the same doctor and day. Index (DoctorId, DayOfWeek). Replaced wholesale on save.

**DoctorBlockedDate** (BaseEntity): `DoctorId`, `Date` (DateOnly), `Label` (string?, max 200). Unique per (DoctorId, Date).

**Appointment**: unchanged. Online booking sets Status=Pending, Type=Consultation, DurationMinutes=30, Reason (<=500), PatientId from token, DoctorId from the patient.

Open slot = 30-minute unit inside a window, on a non-blocked date, start >= now+60min and <= today+60d, with no non-cancelled appointment of the doctor overlapping it, and no non-cancelled appointment of the patient overlapping it.
