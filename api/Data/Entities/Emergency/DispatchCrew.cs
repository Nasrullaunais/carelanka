namespace CareLanka.Api.Data.Entities.Emergency;

public class DispatchCrew : Entity
{
    public Guid DispatchId { get; set; }
    public Dispatch Dispatch { get; set; } = null!;
    public Guid StaffMemberId { get; set; }
}
