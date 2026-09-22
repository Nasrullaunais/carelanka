using System.ComponentModel;
using CareLanka.Api.Common.Persistence;

namespace CareLanka.Api.Data.Enums;

[TypeConverter(typeof(SnakeCaseEnumTypeConverter<WarningType>))]
public enum WarningType
{
    LowStock,
    MedicineExpiring,
    MaintenanceOverdue,
    EquipmentFaulty
}
