import { InputGroup, InputGroupInput, Label, TextField } from '@heroui/react';

export interface StaffPickerOption {
  id: string;
  label: string;
}

export function StaffPickerField({ value, onChange, lookup }: {
  value: string;
  onChange: (value: string) => void;
  lookup?: (query: string) => StaffPickerOption[];
}) {
  const options = lookup?.(value) ?? [];

  return (
    <div className="flex flex-col gap-2">
      <TextField value={value} onChange={onChange}>
        <Label>Staff member ID</Label>
        <InputGroup><InputGroupInput placeholder="Staff UUID" required /></InputGroup>
      </TextField>
      {options.length > 0 && (
        <div role="listbox" aria-label="Matching staff" className="flex flex-col gap-1">
          {options.map((option) => (
            <button key={option.id} type="button" role="option" className="text-left text-sm" onClick={() => onChange(option.id)}>
              {option.label}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
