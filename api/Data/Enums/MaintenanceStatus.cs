namespace CareLanka.Api.Data.Enums;

// Overdue is never stored. It is computed at read time from
// scheduled_date < today AND status = scheduled, so there is nothing to drift.
public enum MaintenanceStatus
{
    Scheduled,
    InProgress,
    Completed,
    Overdue,
    Cancelled
}
