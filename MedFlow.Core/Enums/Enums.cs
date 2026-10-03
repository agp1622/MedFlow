namespace MedFlow.Core.Enums;

public enum PatientStatus { Active, Inactive, Deceased }
public enum AppointmentStatus { Pending, Confirmed, Completed, Cancelled, NoShow }
public enum AppointmentType { NewPatient, FollowUp, CheckUp, Consultation, LabReview, Emergency }
public enum PrescriptionStatus { Active, Expired, Cancelled, ExpiringSoon }
public enum InvoiceStatus { Draft, Pending, Paid, Overdue, Cancelled }
public enum Gender { Male, Female, NonBinary, PreferNotToSay }
public enum BloodType { APos, ANeg, BPos, BNeg, ABPos, ABNeg, OPos, ONeg, Unknown }
public enum AllergySeverity { Mild, Moderate, Severe, LifeThreatening }
public enum ProblemStatus { Active, Resolved }
public enum IntakeStatus { Pending, Accepted, Rejected }

public enum ReminderStatus { Pending, Sent, Failed, Skipped }
public enum ReminderOutcome { Sent, Failed, Skipped }
public enum ReminderResponse { None, Confirmed, Cancelled }
public enum ReminderAction { Confirm, Cancel }
public enum AuditAction { View, Change }
public enum AuditItemKind { Patient, Appointment, Prescription, Invoice, VitalSign, Note, Attachment, PortalAccess, AuditLog, LabOrder }
public enum LabOrderStatus { Ordered, Completed, Cancelled }
public enum LabFlag { None, Low, High }
