using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Patient;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Testcontainers.PostgreSql;
using Xunit;

namespace CareLanka.Api.Tests;

public sealed class PatientDatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("carelanka_patient_db_tests")
        .WithUsername("carelanka_tests")
        .WithPassword(Guid.NewGuid().ToString("N"))
        .Build();

    public async Task InitializeAsync()
    {
        await _database.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _database.DisposeAsync().AsTask();

    public CareLankaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CareLankaDbContext>();
        options.UseCareLankaDatabase(_database.GetConnectionString());
        return new CareLankaDbContext(options.Options);
    }
}

public sealed class PatientDatabaseTests : IClassFixture<PatientDatabaseFixture>
{
    private const string UniqueViolation = "23505";
    private const string CheckViolation = "23514";
    private const string ForeignKeyViolation = "23503";

    private const string NicIndex = "ux_patients_nic";
    private const string OpenAdmissionIndex = "ux_admissions_open_patient";
    private const string LiveBedIndex = "ux_bed_assignments_live_bed";
    private const string LiveAdmissionIndex = "ux_bed_assignments_live_admission";
    private const string PatientIdentifierCheck = "ck_patients_identifier";
    private const string BillOwnerCheck = "ck_bills_one_owner";
    private const string BillAdmissionIndex = "ux_bills_admission_id";

    private readonly PatientDatabaseFixture _fixture;

    public PatientDatabaseTests(PatientDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    [Trait("id", "PT-DB-01")]
    public async Task Two_active_patients_cannot_share_a_nic()
    {
        await using var db = _fixture.CreateContext();
        var nic = NewNic();
        db.Patients.Add(NewPatient(nic));
        await db.SaveChangesAsync();

        db.Patients.Add(NewPatient(nic));

        await AssertViolation(() => db.SaveChangesAsync(), UniqueViolation, NicIndex);
    }

    [Fact]
    [Trait("id", "PT-DB-02")]
    public async Task A_nic_can_be_reused_once_its_patient_is_deactivated()
    {
        await using var db = _fixture.CreateContext();
        var nic = NewNic();
        var first = NewPatient(nic);
        db.Patients.Add(first);
        await db.SaveChangesAsync();

        first.IsActive = false;
        first.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        db.Patients.Add(NewPatient(nic));
        await db.SaveChangesAsync();

        await using var check = _fixture.CreateContext();
        Assert.Equal(2, await check.Patients.IgnoreQueryFilters().CountAsync(p => p.Nic == nic));
    }

    [Fact]
    [Trait("id", "PT-DB-13")]
    public async Task Any_number_of_patients_may_have_no_nic()
    {
        await using var db = _fixture.CreateContext();
        var first = NewPatient(nic: null);
        var second = NewPatient(nic: null);
        db.Patients.AddRange(first, second);

        await db.SaveChangesAsync();

        await using var check = _fixture.CreateContext();
        Assert.Equal(2, await check.Patients
            .CountAsync(p => p.Id == first.Id || p.Id == second.Id));
    }

    [Fact]
    [Trait("id", "PT-DB-03")]
    public async Task A_patient_cannot_have_two_open_admissions()
    {
        await using var db = _fixture.CreateContext();
        var patient = NewPatient(NewNic());
        db.Patients.Add(patient);
        db.Admissions.Add(NewAdmission(patient.Id, AdmissionStatus.AwaitingBed));
        await db.SaveChangesAsync();

        db.Admissions.Add(NewAdmission(patient.Id, AdmissionStatus.Admitted));

        await AssertViolation(() => db.SaveChangesAsync(), UniqueViolation, OpenAdmissionIndex);
    }

    [Theory]
    [Trait("id", "PT-DB-03b")]
    [InlineData(AdmissionStatus.Discharged)]
    [InlineData(AdmissionStatus.Cancelled)]
    public async Task A_new_admission_is_allowed_once_the_earlier_ones_are_finished(AdmissionStatus finished)
    {
        await using var db = _fixture.CreateContext();
        var patient = NewPatient(NewNic());
        db.Patients.Add(patient);
        db.Admissions.Add(NewAdmission(patient.Id, finished));
        await db.SaveChangesAsync();

        db.Admissions.Add(NewAdmission(patient.Id, AdmissionStatus.AwaitingBed));
        db.Admissions.Add(NewAdmission(patient.Id, finished));
        await db.SaveChangesAsync();

        await using var check = _fixture.CreateContext();
        Assert.Equal(3, await check.Admissions.CountAsync(a => a.PatientId == patient.Id));
    }

    [Fact]
    [Trait("id", "PT-DB-03c")]
    public async Task Different_patients_can_each_have_an_open_admission()
    {
        await using var db = _fixture.CreateContext();
        var first = NewPatient(NewNic());
        var second = NewPatient(NewNic());
        db.Patients.AddRange(first, second);
        db.Admissions.Add(NewAdmission(first.Id, AdmissionStatus.AwaitingBed));
        db.Admissions.Add(NewAdmission(second.Id, AdmissionStatus.AwaitingBed));

        await db.SaveChangesAsync();

        await using var check = _fixture.CreateContext();
        Assert.Equal(2, await check.Admissions
            .CountAsync(a => a.PatientId == first.Id || a.PatientId == second.Id));
    }

    [Theory]
    [Trait("id", "PT-DB-09")]
    [InlineData("awaiting_bed")]
    [InlineData("awaiting_approval")]
    [InlineData("bed_reserved")]
    [InlineData("admitted")]
    [InlineData("ready_for_discharge")]
    [InlineData("discharged")]
    [InlineData("cancelled")]
    public async Task The_database_accepts_each_of_the_seven_admission_states(string status)
    {
        await using var db = _fixture.CreateContext();
        var patient = NewPatient(NewNic());
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        await InsertAdmissionRaw(db, patient.Id, status: status);

        Assert.Equal(1, await db.Admissions.CountAsync(a => a.PatientId == patient.Id));
    }

    [Theory]
    [Trait("id", "PT-DB-09b")]
    [InlineData("status", "bogus", "ck_admissions_status")]
    [InlineData("status", "Admitted", "ck_admissions_status")]
    [InlineData("status", "", "ck_admissions_status")]
    [InlineData("source", "bogus", "ck_admissions_source")]
    [InlineData("urgency", "bogus", "ck_admissions_urgency")]
    [InlineData("category", "bogus", "ck_admissions_category")]
    [InlineData("cancel_reason", "bogus", "ck_admissions_cancel_reason")]
    public async Task The_database_rejects_a_made_up_value_in_a_constrained_admission_column(
        string column, string value, string constraint)
    {
        await using var db = _fixture.CreateContext();
        var patient = NewPatient(NewNic());
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        await AssertViolation(() => InsertAdmissionRaw(
            db,
            patient.Id,
            status: column == "status" ? value : "awaiting_bed",
            source: column == "source" ? value : "walk_in",
            urgency: column == "urgency" ? value : "routine",
            category: column == "category" ? value : null,
            cancelReason: column == "cancel_reason" ? value : null),
            CheckViolation, constraint);
    }

    [Fact]
    [Trait("id", "PT-DB-08")]
    public async Task A_patient_with_admissions_cannot_be_deleted()
    {
        await using var db = _fixture.CreateContext();
        var patient = NewPatient(NewNic());
        db.Patients.Add(patient);
        db.Admissions.Add(NewAdmission(patient.Id, AdmissionStatus.Discharged));
        await db.SaveChangesAsync();

        await AssertViolation(
            () => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM patients WHERE id = {patient.Id}"),
            ForeignKeyViolation, "fk_admissions_patients_patient_id");
    }

    [Fact]
    [Trait("id", "PT-DB-10")]
    public async Task A_rolled_back_transaction_leaves_no_patient_or_admission_behind()
    {
        var patient = NewPatient(NewNic());
        var admission = NewAdmission(patient.Id, AdmissionStatus.AwaitingBed);

        await using (var db = _fixture.CreateContext())
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            db.Patients.Add(patient);
            db.Admissions.Add(admission);
            await db.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        await using var check = _fixture.CreateContext();
        Assert.False(await check.Patients.AnyAsync(p => p.Id == patient.Id));
        Assert.False(await check.Admissions.AnyAsync(a => a.Id == admission.Id));
    }

    [Theory]
    [Trait("id", "PT-DB-04")]
    [InlineData(AssignmentStatus.Reserved, AssignmentStatus.Reserved)]
    [InlineData(AssignmentStatus.Reserved, AssignmentStatus.Occupied)]
    [InlineData(AssignmentStatus.Occupied, AssignmentStatus.Occupied)]
    public async Task One_bed_cannot_have_two_live_assignments(AssignmentStatus first, AssignmentStatus second)
    {
        await using var db = _fixture.CreateContext();
        var bedId = Guid.NewGuid();
        var firstAdmission = await SaveAdmission(db);
        var secondAdmission = await SaveAdmission(db);
        db.BedAssignments.Add(NewBedAssignment(firstAdmission.Id, bedId, first));
        await db.SaveChangesAsync();

        db.BedAssignments.Add(NewBedAssignment(secondAdmission.Id, bedId, second));

        await AssertViolation(() => db.SaveChangesAsync(), UniqueViolation, LiveBedIndex);
    }

    [Fact]
    [Trait("id", "PT-DB-04b")]
    public async Task A_released_bed_can_be_given_out_again()
    {
        await using var db = _fixture.CreateContext();
        var bedId = Guid.NewGuid();
        var admissions = new[] { await SaveAdmission(db), await SaveAdmission(db), await SaveAdmission(db) };
        db.BedAssignments.Add(NewBedAssignment(admissions[0].Id, bedId, AssignmentStatus.Released));
        db.BedAssignments.Add(NewBedAssignment(admissions[1].Id, bedId, AssignmentStatus.Released));
        db.BedAssignments.Add(NewBedAssignment(admissions[2].Id, bedId, AssignmentStatus.Reserved));

        await db.SaveChangesAsync();

        await using var check = _fixture.CreateContext();
        Assert.Equal(3, await check.BedAssignments.CountAsync(b => b.BedId == bedId));
    }

    [Fact]
    [Trait("id", "PT-DB-12")]
    public async Task One_admission_cannot_hold_two_live_beds()
    {
        await using var db = _fixture.CreateContext();
        var admission = await SaveAdmission(db);
        db.BedAssignments.Add(NewBedAssignment(admission.Id, Guid.NewGuid(), AssignmentStatus.Reserved));
        await db.SaveChangesAsync();

        db.BedAssignments.Add(NewBedAssignment(admission.Id, Guid.NewGuid(), AssignmentStatus.Occupied));

        await AssertViolation(() => db.SaveChangesAsync(), UniqueViolation, LiveAdmissionIndex);
    }

    [Fact]
    [Trait("id", "PT-DB-12b")]
    public async Task An_admission_can_move_to_another_bed_after_releasing_the_first()
    {
        await using var db = _fixture.CreateContext();
        var admission = await SaveAdmission(db);
        db.BedAssignments.Add(NewBedAssignment(admission.Id, Guid.NewGuid(), AssignmentStatus.Released));
        db.BedAssignments.Add(NewBedAssignment(admission.Id, Guid.NewGuid(), AssignmentStatus.Occupied));

        await db.SaveChangesAsync();

        await using var check = _fixture.CreateContext();
        Assert.Equal(2, await check.BedAssignments.CountAsync(b => b.AdmissionId == admission.Id));
    }

    [Fact]
    [Trait("id", "PT-DB-05")]
    public async Task A_patient_needs_a_nic_a_phone_or_a_temporary_reference()
    {
        await using var db = _fixture.CreateContext();
        db.Patients.Add(NewPatient(nic: null, phone: null, tempReference: null));

        await AssertViolation(() => db.SaveChangesAsync(), CheckViolation, PatientIdentifierCheck);
    }

    [Fact]
    [Trait("id", "PT-DB-05b")]
    public async Task Any_one_identifier_is_enough_for_a_patient()
    {
        await using var db = _fixture.CreateContext();
        var nicOnly = NewPatient(NewNic(), phone: null);
        var phoneOnly = NewPatient(nic: null, phone: "0771111111");
        var referenceOnly = NewPatient(nic: null, phone: null, tempReference: NewNic());
        db.Patients.AddRange(nicOnly, phoneOnly, referenceOnly);

        await db.SaveChangesAsync();

        await using var check = _fixture.CreateContext();
        Assert.Equal(3, await check.Patients.CountAsync(p =>
            p.Id == nicOnly.Id || p.Id == phoneOnly.Id || p.Id == referenceOnly.Id));
    }

    [Theory]
    [Trait("id", "PT-DB-06")]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task A_bill_must_belong_to_exactly_one_admission_or_appointment(
        bool withAdmission, bool withAppointment)
    {
        await using var db = _fixture.CreateContext();
        var admission = await SaveAdmission(db);
        var appointment = await SaveAppointment(db);
        db.Bills.Add(NewBill(
            withAdmission ? admission.Id : null,
            withAppointment ? appointment.Id : null));

        await AssertViolation(() => db.SaveChangesAsync(), CheckViolation, BillOwnerCheck);
    }

    [Theory]
    [Trait("id", "PT-DB-06b")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task A_bill_with_exactly_one_owner_is_accepted(bool withAdmission, bool withAppointment)
    {
        await using var db = _fixture.CreateContext();
        var admission = await SaveAdmission(db);
        var appointment = await SaveAppointment(db);
        var bill = NewBill(
            withAdmission ? admission.Id : null,
            withAppointment ? appointment.Id : null);
        db.Bills.Add(bill);

        await db.SaveChangesAsync();

        await using var check = _fixture.CreateContext();
        Assert.True(await check.Bills.AnyAsync(b => b.Id == bill.Id));
    }

    [Fact]
    [Trait("id", "PT-DB-14")]
    public async Task An_admission_can_only_be_billed_once()
    {
        await using var db = _fixture.CreateContext();
        var admission = await SaveAdmission(db);
        db.Bills.Add(NewBill(admission.Id, null));
        await db.SaveChangesAsync();

        // A second bill added in the same context makes EF detach the first bill's admission
        // before any SQL runs, so the unique index would never be the thing that refuses it.
        await using var second = _fixture.CreateContext();
        second.Bills.Add(NewBill(admission.Id, null));

        await AssertViolation(() => second.SaveChangesAsync(), UniqueViolation, BillAdmissionIndex);
    }

    [Theory]
    [Trait("id", "PT-DB-07")]
    [InlineData("0", "10", "ck_bill_line_items_quantity")]
    [InlineData("-1", "10", "ck_bill_line_items_quantity")]
    [InlineData("1", "-0.01", "ck_bill_line_items_unit_price")]
    public async Task A_bill_line_must_have_a_positive_quantity_and_a_price_of_at_least_zero(
        string quantity, string unitPrice, string constraint)
    {
        await using var db = _fixture.CreateContext();
        var bill = await SaveBill(db);
        db.BillLineItems.Add(NewLineItem(bill.Id, quantity, unitPrice));

        await AssertViolation(() => db.SaveChangesAsync(), CheckViolation, constraint);
    }

    [Theory]
    [Trait("id", "PT-DB-07b")]
    [InlineData("1", "10")]
    [InlineData("0.01", "10")]
    [InlineData("1", "0")]
    public async Task A_bill_line_accepts_the_smallest_quantity_and_a_free_price(string quantity, string unitPrice)
    {
        await using var db = _fixture.CreateContext();
        var bill = await SaveBill(db);
        var line = NewLineItem(bill.Id, quantity, unitPrice);
        db.BillLineItems.Add(line);

        await db.SaveChangesAsync();

        await using var check = _fixture.CreateContext();
        Assert.True(await check.BillLineItems.AnyAsync(i => i.Id == line.Id));
    }

    [Fact]
    [Trait("id", "PT-DB-11")]
    public async Task Every_migration_is_applied_including_the_patient_ones()
    {
        await using var db = _fixture.CreateContext();

        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(db.Database.GetMigrations().Count(), applied.Count);
        Assert.Contains(applied, name => name.EndsWith("_Patient_AddAdmission", StringComparison.Ordinal));
        Assert.Contains(applied, name => name.EndsWith("_Patient_AddBilling", StringComparison.Ordinal));
    }

    private static async Task AssertViolation(Func<Task> action, string sqlState, string constraint)
    {
        var error = await Record.ExceptionAsync(action);
        var postgres = error as PostgresException ?? error?.InnerException as PostgresException;
        Assert.NotNull(postgres);
        Assert.Equal(sqlState, postgres.SqlState);
        Assert.Equal(constraint, postgres.ConstraintName);
    }

    private static string NewNic() => Guid.NewGuid().ToString("N")[..12];

    private static Admission NewAdmission(Guid patientId, AdmissionStatus status)
    {
        var now = DateTimeOffset.UtcNow;
        return new Admission
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            Source = AdmissionSource.WalkIn,
            Urgency = AdmissionUrgency.Routine,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static async Task<Admission> SaveAdmission(CareLankaDbContext db)
    {
        var patient = NewPatient(NewNic());
        var admission = NewAdmission(patient.Id, AdmissionStatus.Admitted);
        db.Patients.Add(patient);
        db.Admissions.Add(admission);
        await db.SaveChangesAsync();
        return admission;
    }

    private static async Task<Appointment> SaveAppointment(CareLankaDbContext db)
    {
        var patient = NewPatient(NewNic());
        var now = DateTimeOffset.UtcNow;
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            ScheduledAt = now.AddDays(1),
            Status = AppointmentStatus.Scheduled,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Patients.Add(patient);
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        return appointment;
    }

    private static async Task<Bill> SaveBill(CareLankaDbContext db)
    {
        var admission = await SaveAdmission(db);
        var bill = NewBill(admission.Id, null);
        db.Bills.Add(bill);
        await db.SaveChangesAsync();
        return bill;
    }

    private static BedAssignment NewBedAssignment(Guid admissionId, Guid bedId, AssignmentStatus status)
    {
        var now = DateTimeOffset.UtcNow;
        return new BedAssignment
        {
            Id = Guid.NewGuid(),
            AdmissionId = admissionId,
            BedId = bedId,
            Status = status,
            ReleasedAt = status == AssignmentStatus.Released ? now : null,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static Bill NewBill(Guid? admissionId, Guid? appointmentId)
    {
        var now = DateTimeOffset.UtcNow;
        return new Bill
        {
            Id = Guid.NewGuid(),
            AdmissionId = admissionId,
            AppointmentId = appointmentId,
            BillNumber = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static BillLineItem NewLineItem(Guid billId, string quantity, string unitPrice)
    {
        var now = DateTimeOffset.UtcNow;
        return new BillLineItem
        {
            Id = Guid.NewGuid(),
            BillId = billId,
            Source = BillLineSource.Manual,
            Description = "QM Test line",
            Quantity = decimal.Parse(quantity, System.Globalization.CultureInfo.InvariantCulture),
            UnitPrice = decimal.Parse(unitPrice, System.Globalization.CultureInfo.InvariantCulture),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static Task InsertAdmissionRaw(
        CareLankaDbContext db,
        Guid patientId,
        string status = "awaiting_bed",
        string source = "walk_in",
        string urgency = "routine",
        string? category = null,
        string? cancelReason = null)
        => db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO admissions (id, patient_id, source, category, urgency, status, cancel_reason, created_at, updated_at)
            VALUES (@id, @patient, @source, @category, @urgency, @status, @cancelReason, now(), now())
            """,
            new NpgsqlParameter("id", Guid.NewGuid()),
            new NpgsqlParameter("patient", patientId),
            new NpgsqlParameter("source", source),
            Text("category", category),
            new NpgsqlParameter("urgency", urgency),
            new NpgsqlParameter("status", status),
            Text("cancelReason", cancelReason));

    private static NpgsqlParameter Text(string name, string? value)
        => new(name, NpgsqlDbType.Text) { Value = (object?)value ?? DBNull.Value };

    private static Patient NewPatient(string? nic, string? phone = "0770000000", string? tempReference = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new Patient
        {
            Id = Guid.NewGuid(),
            PatientCode = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            FullName = "QM Test PT-DB",
            Nic = nic,
            Phone = phone,
            TempReference = tempReference,
            Gender = Gender.Unknown,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
