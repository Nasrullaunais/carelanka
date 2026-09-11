using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Patient;

/// <summary>
/// What one thing costs in one kind of ward. One row is one cell of the price grid the
/// administrator edits: "meals, in a surgical ward, LKR 1,800".
/// </summary>
/// <remarks>
/// <b>Why this is a table and <see cref="Services.Patient.BillingRates"/> said it should not
/// be.</b> That file argued a price list is edited once a year and a table would be a
/// migration, a screen and a role for nothing. That held while the rates were invented numbers
/// nobody could change. It stopped holding the moment the hospital administrator was given the
/// job of setting them — a number only a developer can change is not a number the
/// administrator owns.
///
/// The static table is still here, as the <b>defaults</b> this table is seeded from and as the
/// fallback when a row is missing. A pricing screen that can leave a bill unpriceable is worse
/// than one that cannot be edited.
///
/// <b>The price is still copied onto the line when the line is written</b>, exactly as before.
/// Editing a rate here never rewrites a bill already raised — see
/// <see cref="BillLineItem"/>. That is the whole reason the price is a column on the line and
/// not a lookup at read time.
/// </remarks>
public class BillingRate : SoftDeletableEntity
{
    /// <summary>The kind of ward this price applies in. The ward sets the price.</summary>
    public WardType WardType { get; set; }

    /// <summary>
    /// Which expense, as a stable key — <c>bed_day</c>, <c>meals</c>, <c>therapy</c> and the
    /// rest. A string rather than an enum because the charge list is a product decision that
    /// reception argues about, not a state machine, and adding one should not be a migration.
    /// </summary>
    public string ExpenseKey { get; set; } = null!;

    /// <summary>
    /// The price in rupees. <c>0</c> means "no suggestion" — medicine has no fixed price and
    /// never will, so the desk types what the pharmacy slip said.
    /// </summary>
    public decimal Amount { get; set; }
}
