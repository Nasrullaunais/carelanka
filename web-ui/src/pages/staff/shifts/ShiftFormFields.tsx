import { useQuery } from '@tanstack/react-query';
import { listSkillsOptions } from '../../../services/api/generated/@tanstack/react-query.gen';
import type { StaffRole } from '../../../services/api/generated';
import { staffRoleLabels, staffRoles } from '../../../types/staff';

/**
 * Shared form field group for shift time, role, skill, and headcount.
 * Reused by CreateShiftModal and EditShiftModal.
 */
export function ShiftFormFields({
  idPrefix,
  wardId,
  onWardChange,
  wards,
  date,
  onDateChange,
  startTime,
  onStartTimeChange,
  endTime,
  onEndTimeChange,
  requiredRole,
  onRequiredRoleChange,
  requiredSkillId,
  onRequiredSkillIdChange,
  headcountNeeded,
  onHeadcountNeededChange,
  minimumHeadcount,
  onMinimumHeadcountChange,
  fieldErrors,
}: {
  idPrefix: string;
  wardId: string;
  onWardChange: (v: string) => void;
  wards: Array<{ id: string; name: string }>;
  date: string;
  onDateChange: (v: string) => void;
  startTime: string;
  onStartTimeChange: (v: string) => void;
  endTime: string;
  onEndTimeChange: (v: string) => void;
  requiredRole: StaffRole;
  onRequiredRoleChange: (v: StaffRole) => void;
  requiredSkillId: string;
  onRequiredSkillIdChange: (v: string) => void;
  headcountNeeded: number;
  onHeadcountNeededChange: (v: number) => void;
  minimumHeadcount: number;
  onMinimumHeadcountChange: (v: number) => void;
  fieldErrors: Record<string, string>;
}) {
  const skillsQuery = useQuery(listSkillsOptions());
  const skills = skillsQuery.data ?? [];

  const errorStyle = { color: 'var(--danger, #b91c1c)', fontSize: '0.8rem' };

  return (
    <>
      <div>
        <label htmlFor={`${idPrefix}-ward`}>Ward *</label>
        <select
          id={`${idPrefix}-ward`}
          value={wardId}
          onChange={(e) => onWardChange(e.target.value)}
          required
        >
          {wards.map((w) => (
            <option key={w.id} value={w.id}>
              {w.name}
            </option>
          ))}
        </select>
        {fieldErrors.wardId && <p className="field-error" style={errorStyle}>{fieldErrors.wardId}</p>}
      </div>

      <div>
        <label htmlFor={`${idPrefix}-date`}>Shift date *</label>
        <input
          id={`${idPrefix}-date`}
          type="date"
          value={date}
          onChange={(e) => onDateChange(e.target.value)}
          required
        />
        {fieldErrors.date && <p className="field-error" style={errorStyle}>{fieldErrors.date}</p>}
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
        <div>
          <label htmlFor={`${idPrefix}-start`}>Start time (HH:mm) *</label>
          <input
            id={`${idPrefix}-start`}
            type="time"
            value={startTime}
            onChange={(e) => onStartTimeChange(e.target.value)}
            required
          />
          {fieldErrors.startTime && <p className="field-error" style={errorStyle}>{fieldErrors.startTime}</p>}
        </div>
        <div>
          <label htmlFor={`${idPrefix}-end`}>End time (HH:mm) *</label>
          <input
            id={`${idPrefix}-end`}
            type="time"
            value={endTime}
            onChange={(e) => onEndTimeChange(e.target.value)}
            required
          />
          {fieldErrors.endTime && <p className="field-error" style={errorStyle}>{fieldErrors.endTime}</p>}
        </div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
        <div>
          <label htmlFor={`${idPrefix}-role`}>Required role *</label>
          <select
            id={`${idPrefix}-role`}
            value={requiredRole}
            onChange={(e) => onRequiredRoleChange(e.target.value as StaffRole)}
            required
          >
            {staffRoles.map((r) => (
              <option key={r} value={r}>
                {staffRoleLabels[r]}
              </option>
            ))}
          </select>
        </div>
        <div>
          <label htmlFor={`${idPrefix}-skill`}>Required skill (optional)</label>
          <select
            id={`${idPrefix}-skill`}
            value={requiredSkillId}
            onChange={(e) => onRequiredSkillIdChange(e.target.value)}
          >
            <option value="">None (any qualified {staffRoleLabels[requiredRole]})</option>
            {skills.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
        <div>
          <label htmlFor={`${idPrefix}-headcount`}>Headcount needed *</label>
          <input
            id={`${idPrefix}-headcount`}
            type="number"
            min="1"
            max="50"
            value={headcountNeeded}
            onChange={(e) => onHeadcountNeededChange(Number(e.target.value))}
            required
          />
          {fieldErrors.headcountNeeded && <p className="field-error" style={errorStyle}>{fieldErrors.headcountNeeded}</p>}
        </div>
        <div>
          <label htmlFor={`${idPrefix}-min-headcount`}>Minimum headcount *</label>
          <input
            id={`${idPrefix}-min-headcount`}
            type="number"
            min="1"
            max={headcountNeeded}
            value={minimumHeadcount}
            onChange={(e) => onMinimumHeadcountChange(Number(e.target.value))}
            required
          />
          {fieldErrors.minimumHeadcount && <p className="field-error" style={errorStyle}>{fieldErrors.minimumHeadcount}</p>}
        </div>
      </div>
    </>
  );
}

/** Validates the shared shift fields. Returns a map of field name → error message. */
export function validateShiftFields({
  wardId,
  date,
  startTime,
  endTime,
  headcountNeeded,
  minimumHeadcount,
}: {
  wardId: string;
  date: string;
  startTime: string;
  endTime: string;
  headcountNeeded: number;
  minimumHeadcount: number;
}): Record<string, string> {
  const errors: Record<string, string> = {};
  if (!wardId) errors.wardId = 'Ward is required';
  if (!date) errors.date = 'Date is required';
  if (!startTime) errors.startTime = 'Start time is required';
  if (!endTime) errors.endTime = 'End time is required';
  if (startTime && endTime && startTime === endTime) {
    errors.endTime = 'Start time and end time cannot be identical';
  }
  if (headcountNeeded < 1) errors.headcountNeeded = 'Must be at least 1';
  if (minimumHeadcount < 1) errors.minimumHeadcount = 'Must be at least 1';
  if (minimumHeadcount > headcountNeeded) {
    errors.minimumHeadcount = 'Minimum headcount cannot exceed headcount needed';
  }
  return errors;
}
