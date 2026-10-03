# Contract (unchanged)

`GET /api/vitalsigns/patient/{patientId}` - role Doctor; 404 if the patient is not the caller's (audited View); returns `VitalSignDto[]` newest first. This feature only consumes it.
