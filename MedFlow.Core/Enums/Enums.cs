namespace MedFlow.Core.Enums;

public enum PatientStatus { Active, Inactive, Deceased }
public enum AppointmentStatus { Pending, Confirmed, Completed, Cancelled, NoShow }
public enum AppointmentType { NewPatient, FollowUp, CheckUp, Consultation, LabReview, Emergency }
public enum PrescriptionStatus { Active, Expired, Cancelled, ExpiringSoon }
public enum InvoiceStatus { Draft, Pending, Paid, Overdue, Cancelled }
public enum Gender { Male, Female, NonBinary, PreferNotToSay }
public enum BloodType { APos, ANeg, BPos, BNeg, ABPos, ABNeg, OPos, ONeg, Unknown }
