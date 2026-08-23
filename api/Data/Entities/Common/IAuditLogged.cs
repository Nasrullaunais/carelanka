namespace CareLanka.Api.Data.Entities.Common;

/// <summary>
/// Marker: this entity gets an AuditLog row on every insert, update and soft delete.
/// entity_diagram.md names the entities that should carry it — the aggregate roots plus
/// Allocation, BedAssignment and BedReservation. Add it to your own entities; you do not
/// need to touch any shared file to opt in.
/// </summary>
public interface IAuditLogged;
