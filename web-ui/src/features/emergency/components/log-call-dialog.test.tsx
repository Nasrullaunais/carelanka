import { useState } from 'react';
import { cleanup, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../../../test/api-mocks';
import { LogCallDialog } from './log-call-dialog';

vi.mock('./location-picker', () => ({
  LocationPicker: () => <div>Scene map</div>,
}));
vi.mock('../../../services/api/generated/@tanstack/react-query.gen', () => ({
  searchSceneAddressesOptions: ({ query }: { query: { query: string } }) => ({
    queryKey: ['address-search', query.query],
    queryFn: () => Promise.resolve([]),
  }),
}));

function Harness({ onSubmit }: { onSubmit: (request: unknown) => void }) {
  const [open, setOpen] = useState(true);
  return (
    <>
      <button type="button" onClick={() => setOpen(true)}>Open again</button>
      <LogCallDialog isOpen={open} isPending={false} onOpenChange={setOpen} onSubmit={onSubmit} />
    </>
  );
}

async function fillScene(latitude: string, longitude: string, accuracy: string) {
  await userEvent.type(screen.getByLabelText('Emergency details'), 'Fall at home');
  if (latitude !== '') await userEvent.type(screen.getByLabelText('Latitude'), latitude);
  if (longitude !== '') await userEvent.type(screen.getByLabelText('Longitude'), longitude);
  if (accuracy !== '') await userEvent.type(screen.getByLabelText('Accuracy (m)'), accuracy);
}

describe('LogCallDialog', () => {
  afterEach(() => cleanup());

  it.each([
    ['no latitude', '', '79.8612', '20'],
    ['latitude above 90', '90.5', '79.8612', '20'],
    ['longitude below -180', '6.9271', '-180.5', '20'],
    ['negative accuracy', '6.9271', '79.8612', '-1'],
    ['accuracy too large to store', '6.9271', '79.8612', '100000000'],
  ])('sends nothing when the scene has %s', async (_case, latitude, longitude, accuracy) => {
    const onSubmit = vi.fn();
    renderWithProviders(<Harness onSubmit={onSubmit} />);

    await fillScene(latitude, longitude, accuracy);
    await userEvent.click(screen.getByRole('button', { name: 'Log call' }));

    expect(onSubmit).not.toHaveBeenCalled();
  });

  it.each([
    ['the largest accuracy the server takes', '90', '180', '99999999.99'],
    ['the smallest values', '-90', '-180', '0'],
  ])('sends a call at %s', async (_case, latitude, longitude, accuracy) => {
    const onSubmit = vi.fn();
    renderWithProviders(<Harness onSubmit={onSubmit} />);

    await fillScene(latitude, longitude, accuracy);
    await userEvent.click(screen.getByRole('button', { name: 'Log call' }));

    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));
    expect(onSubmit.mock.calls[0]![0]).toMatchObject({
      latitude: Number(latitude),
      longitude: Number(longitude),
      location_accuracy_metres: Number(accuracy),
    });
  });

  it('sends blank caller fields as empty and trims the ones that were typed', async () => {
    const onSubmit = vi.fn();
    renderWithProviders(<Harness onSubmit={onSubmit} />);

    await userEvent.type(screen.getByLabelText('Caller name'), '   ');
    await userEvent.type(screen.getByLabelText('Caller phone'), '  0771234567  ');
    await fillScene('6.9271', '79.8612', '15');
    await userEvent.click(screen.getByRole('button', { name: 'Log call' }));

    await waitFor(() => expect(onSubmit).toHaveBeenCalled());
    expect(onSubmit.mock.calls[0]![0]).toMatchObject({
      caller_name: null,
      caller_phone: '0771234567',
      details: 'Fall at home',
      patient_is_caller: true,
      priority: 'high',
    });
  });

  it('keeps one key while the dialog is open so a retry is not a second call, and a new key for the next call', async () => {
    const onSubmit = vi.fn();
    renderWithProviders(<Harness onSubmit={onSubmit} />);

    await fillScene('6.9271', '79.8612', '15');
    await userEvent.click(screen.getByRole('button', { name: 'Log call' }));
    await userEvent.click(screen.getByRole('button', { name: 'Log call' }));
    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(2));
    const first = onSubmit.mock.calls[0]![0].idempotency_key;
    expect(onSubmit.mock.calls[1]![0].idempotency_key).toBe(first);

    await userEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    await userEvent.click(screen.getByRole('button', { name: 'Open again' }));
    await fillScene('6.9271', '79.8612', '15');
    await userEvent.click(screen.getByRole('button', { name: 'Log call' }));

    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(3));
    expect(onSubmit.mock.calls[2]![0].idempotency_key).not.toBe(first);
    expect(onSubmit.mock.calls[2]![0].idempotency_key).toMatch(/^[0-9a-f-]{36}$/i);
  });
});
