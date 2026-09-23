import { Table } from '../components/Table';
import { useState } from 'react';
import type { FormEvent, ReactNode } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  assignEquipmentItemMutation,
  countEquipmentItemsAwaitingConfirmationOptions,
  createEquipmentCategoryMutation,
  createEquipmentItemMutation,
  listEquipmentCategoriesOptions,
  listEquipmentItemsOptions,
  listWardsOptions,
  releaseEquipmentItemMutation,
  reportEquipmentFaultMutation,
} from '../services/api/generated/@tanstack/react-query.gen';
import type {
  EquipmentItemSummary,
  EquipmentStatus,
  PrincipalRole,
  WardPatient,
} from '../services/api/generated';
import { WardPatientPicker } from '../components/WardPatientPicker';
import { ActionDialog } from '../components/ui/action-dialog';
import { useSession } from '../services/auth/useSession';
import {
  canConfirmEquipment,
  canManageEquipment,
  canReportFault,
  canTrackEquipmentConfirmations,
} from '../types/permissions';
import { equipmentStatusLabels, equipmentStatuses } from '../types/equipment';
import { ItemDetailCard } from './equipment/ItemDetailCard';
import { ConfirmNewEquipmentCard } from './equipment/ConfirmNewEquipmentCard';
import { RemoveCategoriesCard } from './equipment/RemoveCategoriesCard';
import { RemoveItemDialog, RetireItemDialog } from './equipment/RetireItemDialog';

const PAGE_SIZE = 10;

export function EquipmentPage() {
  const session = useSession();
  const role = session?.principal.role;
  const queryClient = useQueryClient();

  const [search, setSearch] = useState('');
  const [categoryId, setCategoryId] = useState('');
  const [status, setStatus] = useState<EquipmentStatus | ''>('');
  const [wardId, setWardId] = useState('');
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<string | null>(null);
  const [registerOpen, setRegisterOpen] = useState(false);

  const categories = useQuery(listEquipmentCategoriesOptions());
  const awaitingConfirmation = useQuery({
    ...countEquipmentItemsAwaitingConfirmationOptions(),
    enabled: canTrackEquipmentConfirmations(role),
  });
  const wards = useQuery(listWardsOptions({ query: { isActive: true } }));

  const items = useQuery(
    listEquipmentItemsOptions({
      query: {
        page,
        pageSize: PAGE_SIZE,
        ...(search.trim() ? { search: search.trim() } : {}),
        ...(categoryId ? { categoryId } : {}),
        ...(status ? { status } : {}),
        ...(wardId ? { wardId } : {}),
      },
    }),
  );

  function refreshItems() {
    queryClient.invalidateQueries({
      predicate: (query) => {
        const id = (query.queryKey[0] as { _id?: string } | undefined)?._id;
        return (
          id === 'listEquipmentItems' ||
          id === 'getEquipmentItem' ||
          id === 'countEquipmentItemsAwaitingConfirmation'
        );
      },
    });
  }

  const page1 = () => setPage(1);
  const paged = items.data;
  const totalPages = paged?.total_pages ?? 1;

  return (
    <>
      <h1>Equipment</h1>
      <p className="muted">
        The item register and its lifecycle. Owned by Equipment Management.
      </p>

      <p className="stub-note">
        <strong>Ward names are a stub.</strong> Patient Management&rsquo;s ward directory is
        not wired in yet, so an item in a ward reads as <code>Stub ward …</code>. The ward
        it belongs to is real; only the name is faked. See <code>STUBS.md</code> row 2.
      </p>

      <div className="card">
        <h2>Filter</h2>
        <div className="row">
          <div>
            <label htmlFor="filter-search">Search</label>
            <input
              id="filter-search"
              value={search}
              placeholder="Name, model, tag or serial"
              onChange={(event) => {
                setSearch(event.target.value);
                page1();
              }}
            />
          </div>
          <div>
            <label htmlFor="filter-category">Category</label>
            <select
              id="filter-category"
              value={categoryId}
              onChange={(event) => {
                setCategoryId(event.target.value);
                page1();
              }}
            >
              <option value="">All categories</option>
              {(categories.data ?? []).map((category) => (
                <option key={category.id} value={category.id}>
                  {category.name}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label htmlFor="filter-status">Status</label>
            <select
              id="filter-status"
              value={status}
              onChange={(event) => {
                setStatus(event.target.value as EquipmentStatus | '');
                page1();
              }}
            >
              <option value="">Any status</option>
              {equipmentStatuses.map((value) => (
                <option key={value} value={value}>
                  {equipmentStatusLabels[value]}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label htmlFor="filter-ward">Ward</label>
            <select
              id="filter-ward"
              value={wardId}
              onChange={(event) => {
                setWardId(event.target.value);
                page1();
              }}
            >
              <option value="">Anywhere</option>
              {(wards.data ?? []).map((ward) => (
                <option key={ward.id} value={ward.id}>
                  {ward.name}
                </option>
              ))}
            </select>
          </div>
        </div>
      </div>

      {canConfirmEquipment(role) && <ConfirmNewEquipmentCard />}

      {canConfirmEquipment(role) && <RemoveCategoriesCard />}

      {canManageEquipment(role) && (
        <>
        <button type="button" onClick={() => setRegisterOpen(true)}>Register equipment</button>
        <ActionDialog title="Register equipment" isOpen={registerOpen} onClose={() => setRegisterOpen(false)}>
        {registerOpen && <RegisterItemCard
          categories={(categories.data ?? []).map((c) => ({ id: c.id, name: c.name }))}
          wards={(wards.data ?? []).map((w) => ({ id: w.id, name: w.name }))}
          onDone={() => {
            refreshItems();
            page1();
            setRegisterOpen(false);
          }}
          onCategoryCreated={() => {
            queryClient.invalidateQueries({
              predicate: (query) =>
                (query.queryKey[0] as { _id?: string } | undefined)?._id ===
                'listEquipmentCategories',
            });
          }}
        />}
        </ActionDialog>
        </>
      )}

      <div className="card">
        <h2>Item register</h2>
        {awaitingConfirmation.data && awaitingConfirmation.data.count > 0 && (
          <p className="info-note">
            <strong>
              {awaitingConfirmation.data.count === 1
                ? '1 item is'
                : `${awaitingConfirmation.data.count} items are`}{' '}
              awaiting confirmation.
            </strong>{' '}
            The hospital administrator confirms new items, on this page or in the mobile app.
            Each one appears here once it is confirmed.
          </p>
        )}
        <ItemTable
          isLoading={items.isLoading}
          isError={items.isError}
          onRetry={() => void items.refetch()}
          items={paged?.items ?? []}
          selectedId={selected}
          onSelect={(id) => setSelected((current) => (current === id ? null : id))}
          role={role}
          onChanged={refreshItems}
        />

        {paged && paged.total_items > 0 && (
          <div className="pager">
            <button
              type="button"
              className="secondary"
              disabled={page <= 1}
              onClick={() => setPage((p) => Math.max(1, p - 1))}
            >
              Previous
            </button>
            <span className="muted">
              Page {paged.page} of {totalPages} · {paged.total_items} items
            </span>
            <button
              type="button"
              className="secondary"
              disabled={page >= totalPages}
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
            >
              Next
            </button>
          </div>
        )}
      </div>

      <ActionDialog title="Equipment details" isOpen={selected != null} onClose={() => setSelected(null)}>
        {selected && <ItemDetailCard id={selected} onClose={() => setSelected(null)} />}
      </ActionDialog>
    </>
  );
}

function ItemTable({
  items,
  isLoading,
  isError,
  onRetry,
  selectedId,
  onSelect,
  role,
  onChanged,
}: {
  items: EquipmentItemSummary[];
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  selectedId: string | null;
  onSelect: (id: string) => void;
  role: PrincipalRole | undefined;
  onChanged: () => void;
}) {
  if (isLoading) {
    return <p className="empty">Loading…</p>;
  }

  if (isError) {
    return (
      <div className="empty">
        <p>Could not load the item register.</p>
        <button type="button" className="secondary" onClick={onRetry}>
          Try again
        </button>
      </div>
    );
  }

  if (items.length === 0) {
    return <p className="empty">No equipment matches this filter.</p>;
  }

  return (
    <Table>
      <thead>
        <tr>
          <th>Item</th>
          <th>Asset tag</th>
          <th>Category</th>
          <th>Where</th>
          <th>Status</th>
          <th>Lifecycle</th>
        </tr>
      </thead>
      <tbody>
        {items.map((item) => (
          <tr key={item.id} className={selectedId === item.id ? 'selected' : undefined}>
            <td>
              <button type="button" className="linklike" onClick={() => onSelect(item.id)}>
                <strong>{item.name}</strong>
              </button>
              <div className="muted small">
                {item.manufacturer} {item.model}
              </div>
            </td>
            <td>
              <code>{item.asset_tag}</code>
            </td>
            <td>{item.category_name}</td>
            <td>{item.ward_name ?? <span className="muted">Central store</span>}</td>
            <td>
              <StatusBadge status={item.status} />
            </td>
            <td>
              <LifecycleActions item={item} role={role} onChanged={onChanged} />
            </td>
          </tr>
        ))}
      </tbody>
    </Table>
  );
}

export function StatusBadge({ status }: { status: EquipmentStatus }) {
  return (
    <span className={`badge status-${status}`}>{equipmentStatusLabels[status]}</span>
  );
}

function LifecycleActions({
  item,
  role,
  onChanged,
}: {
  item: EquipmentItemSummary;
  role: PrincipalRole | undefined;
  onChanged: () => void;
}) {
  const [assigning, setAssigning] = useState(false);
  const [reporting, setReporting] = useState(false);
  const [retiring, setRetiring] = useState(false);
  const [removing, setRemoving] = useState(false);

  const manage = canManageEquipment(role);
  const report = canReportFault(role);
  const retire = canConfirmEquipment(role);

  const release = useMutation({
    ...releaseEquipmentItemMutation(),
    onSuccess: (updated) => {
      toast.success(`${updated.name} released and back in service.`);
      onChanged();
    },
  });

  return (
    <>
      <div className="actions">
        {manage && item.status === 'available' && (
          <button type="button" className="secondary" onClick={() => setAssigning(true)}>
            Assign
          </button>
        )}

        {manage && item.status === 'assigned' && (
          <button
            type="button"
            className="secondary"
            disabled={release.isPending}
            onClick={() => release.mutate({ path: { id: item.id } })}
          >
            {release.isPending ? 'Releasing…' : 'Release'}
          </button>
        )}

        {report && item.status !== 'retired' && (
          <button type="button" className="secondary danger" onClick={() => setReporting(true)}>
            Report fault
          </button>
        )}

        {retire && item.status !== 'retired' && (
          <button type="button" className="secondary danger" onClick={() => setRetiring(true)}>
            Retire
          </button>
        )}

        {retire && item.status === 'retired' && (
          <button type="button" className="secondary danger" onClick={() => setRemoving(true)}>
            Remove
          </button>
        )}
      </div>

      {assigning && (
        <AssignDialog item={item} onClose={() => setAssigning(false)} onChanged={onChanged} />
      )}
      {reporting && (
        <FaultDialog item={item} onClose={() => setReporting(false)} onChanged={onChanged} />
      )}
      {removing && (
        <RemoveItemDialog
          itemId={item.id}
          itemName={item.name}
          assetTag={item.asset_tag}
          onClose={() => setRemoving(false)}
          onDone={() => {
            setRemoving(false);
            onChanged();
          }}
        />
      )}
      {retiring && (
        <RetireItemDialog
          itemId={item.id}
          itemName={item.name}
          assetTag={item.asset_tag}
          onClose={() => setRetiring(false)}
          onDone={() => {
            setRetiring(false);
            onChanged();
          }}
        />
      )}
    </>
  );
}

function AssignDialog({
  item,
  onClose,
  onChanged,
}: {
  item: EquipmentItemSummary;
  onClose: () => void;
  onChanged: () => void;
}) {
  const [chosen, setChosen] = useState<WardPatient | null>(null);

  const assign = useMutation({
    ...assignEquipmentItemMutation(),
    onSuccess: (updated) => {
      toast.success(`${updated.name} assigned to ${chosen?.full_name}.`);
      onChanged();
      onClose();
    },
  });

  return (
    <Dialog title={`Assign ${item.name}`} onClose={onClose}>
      <p className="muted">
        Pick the patient by ward. The assignment stores their admission id and nothing else — no
        patient data is copied onto the equipment record.
      </p>

      {/* Somebody holding no bed is hidden: a ventilator goes to a bedside. */}
      <WardPatientPicker
        selectedAdmissionId={chosen?.admission_id}
        onSelect={setChosen}
        bedOnly
        emptyMessage="Nobody is in a bed there at the moment."
      />

      <form
        onSubmit={(event: FormEvent) => {
          event.preventDefault();

          if (!chosen) {
            return;
          }

          assign.mutate({
            path: { id: item.id },
            body: { admission_id: chosen.admission_id },
          });
        }}
      >
        {chosen && (
          <p>
            Assigning to <strong>{chosen.full_name}</strong> ({chosen.patient_code}) in{' '}
            {chosen.ward_name}, bed {chosen.bed_number ?? '—'}.
          </p>
        )}

        <div className="actions">
          <button type="submit" disabled={assign.isPending || chosen === null}>
            {assign.isPending ? 'Assigning…' : 'Assign'}
          </button>
          <button type="button" className="secondary" onClick={onClose}>
            Cancel
          </button>
        </div>
      </form>
    </Dialog>
  );
}

function FaultDialog({
  item,
  onClose,
  onChanged,
}: {
  item: EquipmentItemSummary;
  onClose: () => void;
  onChanged: () => void;
}) {
  const [description, setDescription] = useState('');

  const report = useMutation({
    ...reportEquipmentFaultMutation(),
    onSuccess: (updated) => {
      toast.success(`${updated.name} moved to maintenance and a warning was raised.`);
      onChanged();
      onClose();
    },
  });

  return (
    <Dialog title={`Report a fault on ${item.name}`} onClose={onClose}>
      {item.status === 'assigned' && (
        <p className="stub-note">
          <strong>This item is in use by a patient.</strong> Reporting a fault still goes
          through, and it detaches the item from that admission. A known-faulty machine must
          not keep reading as usable, so the fault wins over the assignment.
        </p>
      )}

      <p className="muted">
        The item moves to maintenance immediately and a high-severity warning is raised
        against it. What you write here becomes the recommended action on that warning.
      </p>

      <form
        onSubmit={(event: FormEvent) => {
          event.preventDefault();
          report.mutate({
            path: { id: item.id },
            body: { description: description.trim() },
          });
        }}
      >
        <div className="field">
          <label htmlFor="fault-description">What is wrong</label>
          <textarea
            id="fault-description"
            value={description}
            rows={3}
            maxLength={500}
            placeholder="Alarm silent on self-test."
            onChange={(event) => setDescription(event.target.value)}
            required
          />
        </div>
        <div className="actions">
          <button
            type="submit"
            className="danger"
            disabled={report.isPending || description.trim().length === 0}
          >
            {report.isPending ? 'Reporting…' : 'Report fault'}
          </button>
          <button type="button" className="secondary" onClick={onClose}>
            Cancel
          </button>
        </div>
      </form>
    </Dialog>
  );
}

export function Dialog({
  title,
  onClose,
  children,
}: {
  title: string;
  onClose: () => void;
  children: ReactNode;
}) {
  return (
    <ActionDialog title={title} isOpen onClose={onClose} size="compact">
      {children}
    </ActionDialog>
  );
}

function RegisterItemCard({
  categories,
  wards,
  onDone,
  onCategoryCreated,
}: {
  categories: { id: string; name: string }[];
  wards: { id: string; name: string }[];
  onDone: () => void;
  onCategoryCreated: () => void;
}) {
  const [name, setName] = useState('');
  const [categoryId, setCategoryId] = useState('');
  const [model, setModel] = useState('');
  const [manufacturer, setManufacturer] = useState('');
  const [assetTag, setAssetTag] = useState('');
  const [serialNumber, setSerialNumber] = useState('');
  const [purchaseDate, setPurchaseDate] = useState('');
  const [wardId, setWardId] = useState('');
  const [newCategory, setNewCategory] = useState('');

  const create = useMutation({
    ...createEquipmentItemMutation(),
    onSuccess: (item) => {
      toast.success(
        `${item.name} (${item.asset_tag}) sent to the hospital administrator for confirmation.`,
      );
      setName('');
      setAssetTag('');
      setSerialNumber('');
      onDone();
    },
  });

  const addCategory = useMutation({
    ...createEquipmentCategoryMutation(),
    onSuccess: (category) => {
      toast.success(`Category ${category.name} added.`);
      setNewCategory('');
      setCategoryId(category.id);
      onCategoryCreated();
    },
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    create.mutate({
      body: {
        name: name.trim(),
        category_id: categoryId,
        model: model.trim(),
        manufacturer: manufacturer.trim(),
        purchase_date: purchaseDate,
        asset_tag: assetTag.trim(),
        serial_number: serialNumber.trim() || null,
        ward_id: wardId || null,
      },
    });
  }

  return (
    <div className="card">
      <h2>Register an item</h2>
      <p className="muted" style={{ marginBottom: '0.9rem' }}>
        A new item waits for the hospital administrator to confirm it, on the web or in the
        mobile app, and only then joins the register below. Once confirmed it is available. There is no way to
        register one already assigned or retired, because neither has a story behind it. Asset
        tags are unique across beds and equipment together, since a tag scan has to resolve to
        one thing.
      </p>

      {categories.length === 0 ? (
        <p className="stub-note">
          <strong>No categories yet.</strong> An item needs one, so add a category first.
        </p>
      ) : null}

      <div className="row" style={{ marginBottom: '1rem' }}>
        <div className="field">
          <label htmlFor="new-category">Add a category</label>
          <input
            id="new-category"
            value={newCategory}
            placeholder="Ventilators"
            onChange={(event) => setNewCategory(event.target.value)}
          />
        </div>
        <div className="field" style={{ alignSelf: 'end' }}>
          <button
            type="button"
            className="secondary"
            disabled={addCategory.isPending || newCategory.trim().length === 0}
            onClick={() => addCategory.mutate({ body: { name: newCategory.trim() } })}
          >
            {addCategory.isPending ? 'Adding…' : 'Add category'}
          </button>
        </div>
      </div>

      <form onSubmit={submit}>
        <div className="row">
          <div className="field">
            <label htmlFor="item-name">Name</label>
            <input
              id="item-name"
              value={name}
              maxLength={150}
              placeholder="Ventilator"
              onChange={(event) => setName(event.target.value)}
              required
            />
          </div>
          <div className="field">
            <label htmlFor="item-category">Category</label>
            <select
              id="item-category"
              value={categoryId}
              onChange={(event) => setCategoryId(event.target.value)}
              required
            >
              <option value="">Choose…</option>
              {categories.map((category) => (
                <option key={category.id} value={category.id}>
                  {category.name}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label htmlFor="item-tag">Asset tag</label>
            <input
              id="item-tag"
              value={assetTag}
              maxLength={50}
              placeholder="EQ-0001"
              onChange={(event) => setAssetTag(event.target.value)}
              required
            />
          </div>
        </div>

        <div className="row">
          <div className="field">
            <label htmlFor="item-manufacturer">Manufacturer</label>
            <input
              id="item-manufacturer"
              value={manufacturer}
              maxLength={150}
              placeholder="Acme Medical"
              onChange={(event) => setManufacturer(event.target.value)}
              required
            />
          </div>
          <div className="field">
            <label htmlFor="item-model">Model</label>
            <input
              id="item-model"
              value={model}
              maxLength={100}
              placeholder="V-100"
              onChange={(event) => setModel(event.target.value)}
              required
            />
          </div>
          <div className="field">
            <label htmlFor="item-serial">Serial number</label>
            <input
              id="item-serial"
              value={serialNumber}
              placeholder="Optional"
              onChange={(event) => setSerialNumber(event.target.value)}
            />
          </div>
        </div>

        <div className="row">
          <div className="field">
            <label htmlFor="item-purchased">Purchase date</label>
            <input
              id="item-purchased"
              type="date"
              value={purchaseDate}
              onChange={(event) => setPurchaseDate(event.target.value)}
              required
            />
          </div>
          <div className="field">
            <label htmlFor="item-ward">Ward</label>
            <select
              id="item-ward"
              value={wardId}
              onChange={(event) => setWardId(event.target.value)}
            >
              <option value="">Central store</option>
              {wards.map((ward) => (
                <option key={ward.id} value={ward.id}>
                  {ward.name}
                </option>
              ))}
            </select>
          </div>
        </div>

        <button
          type="submit"
          disabled={
            create.isPending ||
            name.trim().length === 0 ||
            categoryId.length === 0 ||
            assetTag.trim().length === 0
          }
        >
          {create.isPending ? 'Registering…' : 'Register item'}
        </button>
      </form>
    </div>
  );
}
