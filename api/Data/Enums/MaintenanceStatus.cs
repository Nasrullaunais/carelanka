using System.ComponentModel;
using CareLanka.Api.Common.Persistence;

namespace CareLanka.Api.Data.Enums;

[TypeConverter(typeof(SnakeCaseEnumTypeConverter<MaintenanceStatus>))]
public enum MaintenanceStatus
{
    Scheduled,
    InProgress,
    Completed,
    Overdue,
    Cancelled
}
