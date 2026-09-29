namespace CareLanka.Api.Data.Enums;

public enum NotificationType
{
    DispatchAssigned,
    DispatchCancelled,
    DispatchReassigned,

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
    DispatchProposalFailed,
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
