namespace CareLanka.Api.Data.Enums;

public enum NotificationType
{
    DispatchAssigned,

    AppointmentBooked,
    AppointmentRescheduled,
    AppointmentCancelled,
    AppointmentReminder,
    AdmissionApproved,
    BedAssigned,
    DischargeReady,
    BillRaised,
    BillSettled,
    CareReplyReady,
    PrescriptionReady,
    PrescriptionDelivered,
    LabReportReady,
    AmbulanceOnTheWay,
    AmbulanceArrived,
    CancellationAnswered,

    EmergencyCallReceived,
    CancellationRequestWaiting,
    DispatchProposalWaiting,
    AdmissionAwaitingApproval,
    CareQueryFlagged,
    CareReplyWaiting,
    EquipmentWarningRaised,
    MaintenanceDue,
    PharmacyStockLow,
    LabTestRequested,
    LeaveRequested,
    LeaveApproved,
    LeaveRejected,
    ShiftChanged,
    RosterProposalWaiting
}
