using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Services.Common;

public abstract record Recipients
{
    private Recipients() { }

    public static Recipients Staff(Guid staffMemberId) => new StaffRecipient(staffMemberId);

    public static Recipients Patient(Guid patientId) => new PatientRecipient(patientId);

    public static Recipients Role(StaffRole role, Guid? wardId = null) => new RoleRecipient(role, wardId);

    public sealed record StaffRecipient(Guid StaffMemberId) : Recipients;

    public sealed record PatientRecipient(Guid PatientId) : Recipients;

    public sealed record RoleRecipient(StaffRole StaffRole, Guid? WardId) : Recipients;
}
