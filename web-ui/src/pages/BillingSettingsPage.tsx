import { Table } from '../components/Table';
import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  getBillingRatesOptions,
  updateBillingRatesMutation,
} from '../services/api/generated/@tanstack/react-query.gen';
import type { AdmissionCategory, WardType } from '../services/api/generated';
import { useSession } from '../services/auth/useSession';
import { canSetBillingRates } from '../types/permissions';
import { expenseHints, expenseLabel, isUnpriceable, money } from '../types/billing';
import { wardTypeLabels } from '../types/wards';
import { admissionCategoryLabels } from '../types/patients';

type Draft = Record<string, string>;

function expenseCell(wardType: WardType, expenseKey: string): string {
  return `${wardType}::${expenseKey}`;
}

function feeCell(category: AdmissionCategory): string {
  return `fee::${category}`;
}

export function BillingSettingsPage() {
  const session = useSession();
  const role = session?.principal.role;
  const queryClient = useQueryClient();

  const mayEdit = canSetBillingRates(role);

  const rates = useQuery({ ...getBillingRatesOptions(), enabled: mayEdit });

  const [draft, setDraft] = useState<Draft>({});

  useEffect(() => {
    if (!rates.data) {
      return;
    }

    const next: Draft = {};

    for (const ward of rates.data.wards) {
      for (const expense of ward.expenses) {
        next[expenseCell(ward.ward_type, expense.expense_key)] = String(expense.amount);
      }
    }

    for (const fee of rates.data.admission_fees) {
      next[feeCell(fee.category)] = String(fee.amount);
    }

    setDraft(next);
  }, [rates.data]);

  const save = useMutation({
    ...updateBillingRatesMutation(),
    onSuccess: (book) => {
      toast.success('Prices saved. Bills already raised are untouched.');

      queryClient.invalidateQueries({
        predicate: (query) =>
          (query.queryKey[0] as { _id?: string } | undefined)?._id === 'getBillingRates',
      });

      queryClient.setQueryData(getBillingRatesOptions().queryKey, book);
    },
  });

  if (!mayEdit) {
    return (
      <>
        <h1>Billing settings</h1>
        <p className="empty">
          Your role cannot set prices. Only the hospital administrator can — setting rates is a
          separate responsibility from taking payment.
        </p>
      </>
    );
  }

  function changed() {
    const expenses: { ward_type: WardType; expense_key: string; amount: number }[] = [];
    const admission_fees: { category: AdmissionCategory; amount: number }[] = [];

    for (const ward of rates.data?.wards ?? []) {
      for (const expense of ward.expenses) {
        const typed = draft[expenseCell(ward.ward_type, expense.expense_key)];
        const value = Number(typed);
        const usable = typed !== undefined && typed.trim() !== '' && Number.isFinite(value);

        if (usable && value >= 0 && value !== expense.amount) {
          expenses.push({
            ward_type: ward.ward_type,
            expense_key: expense.expense_key,
            amount: value,
          });
        }
      }
    }

    for (const fee of rates.data?.admission_fees ?? []) {
      const typed = draft[feeCell(fee.category)];
      const value = Number(typed);
      const usable = typed !== undefined && typed.trim() !== '' && Number.isFinite(value);

      if (usable && value >= 0 && value !== fee.amount) {
        admission_fees.push({ category: fee.category, amount: value });
      }
    }

    return { expenses, admission_fees };
  }

  const pending = changed();
  const pendingCount = pending.expenses.length + pending.admission_fees.length;

  function submit(event: FormEvent) {
    event.preventDefault();

    if (pendingCount === 0) {
      return;
    }

    save.mutate({ body: pending });
  }

  const currency = rates.data?.currency ?? 'LKR';

  return (
    <>
      <h1>Billing settings</h1>
      <p className="muted">
        What the hospital charges, in {currency}. Every expense, for every type of ward.
      </p>

      <div className="card">
        <p>
          <strong>Changing a price never changes a bill already raised.</strong> The price is
          copied onto the line the moment the line is written, so an edit here prices
          tomorrow&rsquo;s bills and leaves every bill a patient has already been handed exactly
          as it was.
        </p>
        <p className="muted">
          Only <strong>Bed, per day</strong> is applied automatically, from the ward the patient
          is in. The rest are default prices that appear when reception enters a charge, and
          reception can still change them.
        </p>
      </div>

      {rates.isLoading ? (
        <p className="empty">Loading…</p>
      ) : rates.isError ? (
        <div className="empty">
          <p>Could not load the prices.</p>
          <button type="button" className="secondary" onClick={() => void rates.refetch()}>
            Try again
          </button>
        </div>
      ) : (
        <form onSubmit={submit}>

          <div className="card">
            <h2>Admission fee</h2>
            <p className="muted">
              The one-off charge for opening an admission. Priced by the care level a clinician
              recorded, not by the ward — it is charged before the ward is known.
            </p>

            <Table>
              <thead>
                <tr>
                  <th>Care level</th>
                  <th>Price ({currency})</th>
                </tr>
              </thead>
              <tbody>
                {(rates.data?.admission_fees ?? []).map((fee) => (
                  <tr key={fee.category}>
                    <td>{admissionCategoryLabels[fee.category]}</td>
                    <td>
                      <PriceBox
                        id={feeCell(fee.category)}
                        label={`${admissionCategoryLabels[fee.category]} admission fee`}
                        value={draft[feeCell(fee.category)] ?? ''}
                        original={fee.amount}
                        currency={currency}
                        disabled={save.isPending}
                        onChange={(value) =>
                          setDraft((current) => ({ ...current, [feeCell(fee.category)]: value }))
                        }
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </Table>
          </div>

          {(rates.data?.wards ?? []).map((ward) => (
            <div className="card" key={ward.ward_type}>
              <h2>{wardTypeLabels[ward.ward_type]}</h2>

              <Table>
                <thead>
                  <tr>
                    <th>What for</th>
                    <th>Price ({currency})</th>
                  </tr>
                </thead>
                <tbody>
                  {ward.expenses.map((expense) => {
                    const cell = expenseCell(ward.ward_type, expense.expense_key);

                    return (
                      <tr key={expense.expense_key}>
                        <td>
                          <strong>{expenseLabel(expense.expense_key)}</strong>
                          <br />
                          <span className="muted">
                            {expenseHints[expense.expense_key] ?? 'Entered at the desk.'}
                          </span>
                        </td>
                        <td>
                          <PriceBox
                            id={cell}
                            label={`${expenseLabel(expense.expense_key)} in ${
                              wardTypeLabels[ward.ward_type]
                            }`}
                            value={draft[cell] ?? ''}
                            original={expense.amount}
                            currency={currency}
                            unpriceable={isUnpriceable(expense.expense_key)}
                            disabled={save.isPending}
                            onChange={(value) =>
                              setDraft((current) => ({ ...current, [cell]: value }))
                            }
                          />
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </Table>
            </div>
          ))}

          <div className="card">
            <button type="submit" disabled={save.isPending || pendingCount === 0}>
              {save.isPending
                ? 'Saving…'
                : pendingCount === 0
                  ? 'Nothing changed'
                  : `Save ${pendingCount} price${pendingCount === 1 ? '' : 's'}`}
            </button>

            {pendingCount > 0 && !save.isPending && (
              <p className="hint">
                {pendingCount} price{pendingCount === 1 ? '' : 's'} changed and not yet saved.
                Each shows its previous value.
              </p>
            )}
          </div>
        </form>
      )}
    </>
  );
}

function PriceBox({
  id,
  label,
  value,
  original,
  currency,
  unpriceable = false,
  disabled,
  onChange,
}: {
  id: string;
  label: string;
  value: string;
  original: number;
  currency: string;
  unpriceable?: boolean;
  disabled: boolean;
  onChange: (value: string) => void;
}) {
  const parsed = Number(value);
  const invalid = value.trim() !== '' && (!Number.isFinite(parsed) || parsed < 0);
  const dirty = !invalid && value.trim() !== '' && parsed !== original;

  return (
    <div className="field">
      <input
        id={id}
        aria-label={label}
        inputMode="decimal"
        value={value}
        aria-invalid={invalid}
        disabled={disabled}
        onChange={(event) => onChange(event.target.value)}
      />

      {invalid ? (
        <p className="field-error">A price is a number, and never below zero.</p>
      ) : dirty ? (
        <p className="hint">Previously {money(original, currency)}.</p>
      ) : unpriceable && original === 0 ? (
        <p className="hint">No default — reception enters this from the slip.</p>
      ) : null}
    </div>
  );
}
