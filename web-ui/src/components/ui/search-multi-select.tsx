import { useId } from 'react';

export interface SearchOption {
  id: string;
  label: string;
  description?: string;
}

export function SearchMultiSelect({ label, placeholder, search, onSearchChange, options, selected, onChange, isLoading = false, error, onRetry, isDisabled = false }: {
  label: string;
  placeholder: string;
  search: string;
  onSearchChange: (value: string) => void;
  options: SearchOption[];
  selected: SearchOption[];
  onChange: (options: SearchOption[]) => void;
  isLoading?: boolean;
  error?: string;
  onRetry?: () => void;
  isDisabled?: boolean;
}) {
  const inputId = useId();
  const selectedIds = new Set(selected.map((option) => option.id));
  const available = options.filter((option) => !selectedIds.has(option.id));

  return (
    <div className="flex min-w-0 flex-1 flex-col gap-2">
      <label htmlFor={inputId} className="mb-0 text-sm">{label}</label>
      <input
        id={inputId}
        type="search"
        value={search}
        onChange={(event) => onSearchChange(event.target.value)}
        onKeyDown={(event) => { if (event.key === 'Enter') event.preventDefault(); }}
        placeholder={placeholder}
        disabled={isDisabled}
        autoComplete="off"
      />
      {selected.length > 0 && (
        <div className="flex flex-wrap gap-2" aria-label="Selected people">
          {selected.map((option) => (
            <button
              key={option.id}
              type="button"
              className="rounded-full border border-default-300 bg-default-100 px-3 py-1 text-sm text-foreground hover:bg-default-200"
              disabled={isDisabled}
              onClick={() => onChange(selected.filter((item) => item.id !== option.id))}
              aria-label={`Remove ${option.label}`}
            >
              {option.label} <span aria-hidden="true">×</span>
            </button>
          ))}
        </div>
      )}
      <div className="max-h-48 overflow-y-auto rounded-lg border border-default-200 bg-surface p-1" aria-label={`Search results for ${label}`}>
        {isLoading ? <p className="px-3 py-2 text-sm text-muted" role="status">Searching…</p>
          : error ? <div className="flex items-center justify-between gap-2 px-3 py-2 text-sm text-danger" role="alert"><span>{error}</span>{onRetry && <button type="button" className="rounded-md px-2 py-1 text-sm" onClick={onRetry}>Retry</button>}</div>
            : available.length === 0 ? <p className="px-3 py-2 text-sm text-muted">No available matches. Try another name.</p>
              : available.map((option) => (
                <button
                  key={option.id}
                  type="button"
                  disabled={isDisabled}
                  className="flex w-full flex-col rounded-md border-0 bg-transparent px-3 py-2 text-left text-sm text-foreground hover:bg-default-100 focus-visible:outline-2 focus-visible:outline-accent"
                  onClick={() => { onChange([...selected, option]); onSearchChange(''); }}
                >
                  <span className="font-semibold">{option.label}</span>
                  {option.description && <span className="text-xs text-muted">{option.description}</span>}
                </button>
              ))}
      </div>
      <p className="m-0 text-xs text-muted">Select people from the list, then assign them with one action. Click a selected name to remove it.</p>
    </div>
  );
}
