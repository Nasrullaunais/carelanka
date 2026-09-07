namespace CareLanka.Api.Common.Auth;

/// <summary>
/// Every authorization policy in CareLanka, registered once in <c>Program.cs</c>.
/// <para>
/// <strong>Write <c>[Authorize(Policy = Policies.DutyManager)]</c>, never
/// <c>[Authorize(Roles = "duty_manager")]</c>.</strong> A typo in a policy name fails at
/// startup. A typo in a magic role string fails silently, at 3am on demo day, by letting
/// the wrong person in.
/// </para>
/// <para>
/// If your component needs a combination that is not here, ask — do not invent a role
/// string in a controller.
/// </para>
/// </summary>
public static class Policies
{
    // ---------- one per StaffRole ----------
    public const string WardNurse = nameof(WardNurse);
    public const string Doctor = nameof(Doctor);
    public const string AmbulanceCrew = nameof(AmbulanceCrew);
    public const string GeneralStaff = nameof(GeneralStaff);
    public const string DutyManager = nameof(DutyManager);
    public const string HospitalAdministrator = nameof(HospitalAdministrator);
    public const string EquipmentManager = nameof(EquipmentManager);

    // ---------- by principal kind ----------

    /// <summary>Any of the seven internal roles. Excludes patients.</summary>
    public const string AnyStaff = nameof(AnyStaff);

    /// <summary>A patient account only. Their own record, read-only.</summary>
    public const string PatientOnly = nameof(PatientOnly);

    // ---------- combinations the specs actually use ----------

    /// <summary>Reads the agent workflow monitoring surface.</summary>
    public const string WorkflowReader = nameof(WorkflowReader);

    /// <summary>Starts a coordinated workflow.</summary>
    public const string WorkflowStarter = nameof(WorkflowStarter);
}
