# Data Model

No persistent changes. Reads `VitalSignDto` (existing).

Derived client-side types:

- `TrendPoint { t: number (epoch ms), ...values }`
- Series definitions: bloodPressure (systolic, diastolic), heartRate (bpm), weight (kg), bmi, temperature (C), oxygenSaturation (%).
- `DateRange { from?: string (yyyy-mm-dd), to?: string }`; valid when both present implies `from <= to`.
