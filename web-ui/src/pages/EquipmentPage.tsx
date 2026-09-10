import { useState } from 'react';
import type { FormEvent, ReactNode } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  assignEquipmentItemMutation,
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
} from '../services/api/generated';
import { useSession } from '../services/auth/useSession';
import { canManageEquipment, canReportFault } from '../types/permissions';
import { equipmentStatusLabels, equipmentStatuses } from '../types/equipment';
import { ItemDetailCard } from './equipment/ItemDetailCard';

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

  const categories = useQuery(listEquipmentCategoriesOptions());
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

  // Any lifecycle move can change which page an item belongs to, so the whole list is
  // refetched rather than the row patched. Matches the generated key's _id, which is a
  // single object rather than an array prefix.
  function refreshItems() {
    queryClient.invalidateQueries({
      predicate: (query) => {
        const id = (query.queryKey[0] as { _id?: string } | undefined)?._id;
        return id === 'listEquipmentItems' || id === 'getEquipmentItem';
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

      {/* Hidden, not disabled: a control the user's role cannot use should not be on screen. */}
      {canManageEquipment(role) && (
        <RegisterItemCard
          categories={(categories.data ?? []).map((c) => ({ id: c.id, name: c.name }))}
          wards={(wards.data ?? []).map((w) => ({ id: w.id, name: w.name }))}
          onDone={() => {
            refreshItems();
            page1();
          }}
          onCategoryCreated={() => {
            queryClient.invalidateQueries({
              predicate: (query) =>
                (query.queryKey[0] as { _id?: string } | undefined)?._id ===
                'listEquipmentCategories',
            });
          }}
        />
      )}

      <div className="card">
        <h2>Item register</h2>
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

      {selected && <ItemDetailCard id={selected} onClose={() => setSelected(null)} />}
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

  // The toast already fired. A list shows "Try again" rather than an empty table that
  // looks like a hospital with no equipment.
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
    <table>
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
    </table>
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

  const manage = canManageEquipment(role);
  const report = canReportFault(role);

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

        {/* A fault can be reported in any state but retired, including while a patient is
            using the item. That is the documented exception in the server's guard, and the
            reason this button does not disappear when the item is assigned. */}
        {report && item.status !== 'retired' && (
          <button type="button" className="secondary danger" onClick={() => setReporting(true)}>
            Report fault
          </button>
        )}
      </div>

      {assigning && (
        <AssignDialog item={item} onClose={() => setAssigning(false)} onChanged={onChanged} />
      )}
      {reporting && (
        <FaultDialog item={item} onClose={() => setReporting(false)} onChanged={onChanged} />
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
  const [admissionId, setAdmissionId] = useState('');

  const assign = useMutation({
    ...assignEquipmentItemMutation(),
    onSuccess: (updated) => {
      toast.success(`${updated.name} assigned.`);
      onChanged();
      onClose();
    },
  });

  return (
    <Dialog title={`Assign ${item.name}`} onClose={onClose}>
      <p className="muted">
        An assignment points at Patient Management&rsquo;s admission by id. No patient data
        is copied here. There is no admissions endpoint to pick from yet, so the id has to
        be pasted in.
      </p>

      <form
        onSubmit={(event: FormEvent) => {
          event.preventDefault();
          assign.mutate({ path: { id: item.id }, body: { admission_id: admissionId.trim() } });
        }}
      >
        <div className="field">
          <label htmlFor="admission-id">Admission id</label>
          <input
            id="admission-id"
            value={admissionId}
            placeholder="00000000-0000-0000-0000-000000000000"
            onChange={(event) => setAdmissionId(event.target.value)}
            required
          />
        </div>
        <div className="actions">
          <button type="submit" disabled={assign.isPending || admissionId.trim().length === 0}>
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
    <div className="dialog-backdrop" role="dialog" aria-modal="true" aria-label={title}>
      <div className="dialog card">
        <div className="dialog-head">
          <h2>{title}</h2>
          <button type="button" className="secondary" onClick={onClose} aria-label="Close">
            ×
          </button>
        </div>
        {children}
      </div>
    </div>
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
      toast.success(`${item.name} registered as ${item.asset_tag}.`);
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
        A new item is always available. There is no way to register one already assigned or
        retired, because neither has a story behind it. Asset tags are unique across beds and
        equipment together, since a tag scan has to resolve to one thing.
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
