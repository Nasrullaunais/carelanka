import type { ReactNode } from 'react';
import {
  Label,
  ListBox,
  ListBoxItem,
  Select,
  SelectIndicator,
  SelectPopover,
  SelectTrigger,
  SelectValue,
} from '@heroui/react';

const EMPTY_VALUE_KEY = '__carelanka_empty_value__';

export type AppSelectOption = {
  value: string;
  label: ReactNode;
  textValue?: string;
  isDisabled?: boolean;
};

export type AppSelectProps = {
  value: string;
  options: readonly AppSelectOption[];
  onValueChange: (value: string) => void;
  label?: ReactNode;
  'aria-label'?: string;
  id?: string;
  name?: string;
  className?: string;
  isDisabled?: boolean;
  isRequired?: boolean;
  isInvalid?: boolean;
};

function optionKey(value: string) {
  return value === '' ? EMPTY_VALUE_KEY : value;
}

function optionValue(key: string) {
  return key === EMPTY_VALUE_KEY ? '' : key;
}

/**
 * The shared single-choice field for CareLanka. It keeps HeroUI's accessible
 * keyboard and popover behaviour behind the same small API throughout the app.
 */
export function AppSelect({
  value,
  options,
  onValueChange,
  label,
  id,
  name,
  className,
  isDisabled,
  isRequired,
  isInvalid,
  'aria-label': ariaLabel,
}: AppSelectProps) {
  return (
    <Select
      id={id}
      name={name}
      className={className}
      selectedKey={value === '' && !options.some((option) => option.value === '') ? null : optionKey(value)}
      onSelectionChange={(key) => {
        if (key !== undefined && key !== null) onValueChange(optionValue(String(key)));
      }}
      aria-label={ariaLabel}
      isDisabled={isDisabled}
      isRequired={isRequired}
      isInvalid={isInvalid}
      fullWidth
    >
      {label !== undefined && <Label>{label}</Label>}
      <SelectTrigger>
        <SelectValue />
        <SelectIndicator />
      </SelectTrigger>
      <SelectPopover>
        <ListBox>
          {options.map((option) => (
            <ListBoxItem
              key={optionKey(option.value)}
              id={optionKey(option.value)}
              textValue={option.textValue ?? (typeof option.label === 'string' ? option.label : option.value)}
              isDisabled={option.isDisabled}
            >
              {option.label}
            </ListBoxItem>
          ))}
        </ListBox>
      </SelectPopover>
    </Select>
  );
}
