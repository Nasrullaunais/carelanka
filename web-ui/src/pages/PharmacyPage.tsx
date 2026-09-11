import { useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  createPharmacyCategoryMutation,
  createPharmacyItemMutation,
  listPharmacyCategoriesOptions,
  listPharmacyItemsOptions,
} from '../services/api/generated/@tanstack/react-query.gen';
import type { PharmacyItem, PrincipalRole } from '../services/api/generated';
import { useSession } from '../services/auth/useSession';
import { canManageEquipment } from '../types/permissions';
import { Dialog } from './EquipmentPage';
import { StockDialog } from './pharmacy/StockDialog';
import { ItemHistoryCard } from './pharmacy/ItemHistoryCard';

const PAGE_SIZE = 10;

export function PharmacyPage() {
  const session = useSession();
  const role = session?.principal.role;
  const queryClient = useQueryClient();

  const [search, setSearch] = useState('');
  const [categoryId, setCategoryId] = useState('');
  const [availableOnly, setAvailableOnly] = useState(false);
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<string | null>(null);

  const categories = useQuery(listPharmacyCategoriesOptions());

  const items = useQuery(
    listPharmacyItemsOptions({
      query: {
        page,
        pageSize: PAGE_SIZE,
        availableOnly,
        ...(search.trim() ? { search: search.trim() } : {}),
        ...(categoryId ? { categoryId } : {}),
      },
    }),
  );

  // A movement changes the quantity, which changes availability, which changes which page
  // an item belongs to. So the list is refetched rather than the row patched.
  function refresh() {
    queryClient.invalidateQueries({
      predicate: (query) => {
        const id = (query.queryKey[0] as { _id?: string } | undefined)?._id;
        return (
          id === 'listPharmacyItems' ||
          id === 'getPharmacyItem' ||
          id === 'listPharmacyTransactions'
        );
      },
    });
  }

  const paged = items.data;
  const totalPages = paged?.total_pages ?? 1;
  const page1 = () => setPage(1);

  return (
    <>
      <h1>Pharmacy</h1>
      <p className="muted">
        The medicine and supply catalog, what is on the shelf, and every movement in or out.
      </p>

      <p className="stub-note">
        <strong>One central store, not per-ward stock.</strong> There is a single quantity
        per medicine for the whole hospital. Whether wards need their own stock is open
        question 1 in the component plan.
      </p>

      <div className="card">
        <h2>Search</h2>
        <div className="row">
          <div>
            <label htmlFor="ph-search">Name or manufacturer</label>
            <input
              id="ph-search"
              value={search}
              placeholder="Paracetamol"
              onChange={(event) => {
                setSearch(event.target.value);
                page1();
              }}
            />
          </div>
          <div>
            <label htmlFor="ph-category">Category</label>
            <select
              id="ph-category"
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
            <label htmlFor="ph-available">Availability</label>
            <select
              id="ph-available"
              value={availableOnly ? 'in-stock' : 'all'}
              onChange={(event) => {
                setAvailableOnly(event.target.value === 'in-stock');
                page1();
              }}
            >
              <option value="all">Everything in the catalog</option>
              <option value="in-stock">On the shelf now</option>
            </select>
          </div>
        </div>
        <p className="hint">
          Availability is worked out from the quantity when you read it, never stored as its
          own column, so it cannot fall out of step with what is on the shelf.
        </p>
      </div>

      {/* Hidden, not disabled: a control the user's role cannot use should not be on screen. */}
      {canManageEquipment(role) && (
        <AddItemCard
          categories={(categories.data ?? []).map((c) => ({ id: c.id, name: c.name }))}
          onDone={() => {
            refresh();
            page1();
          }}
          onCategoryAdded={() =>
            queryClient.invalidateQueries({
              predicate: (query) =>
                (query.queryKey[0] as { _id?: string } | undefined)?._id ===
                'listPharmacyCategories',
            })
          }
        />
      )}

      <div className="card">
        <h2>Catalog</h2>
        <ItemTable
          isLoading={items.isLoading}
          isError={items.isError}
          onRetry={() => void items.refetch()}
          items={paged?.items ?? []}
          selectedId={selected}
          onSelect={(id) => setSelected((current) => (current === id ? null : id))}
          role={role}
          onChanged={refresh}
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

      {selected && <ItemHistoryCard id={selected} onClose={() => setSelected(null)} />}
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
  items: PharmacyItem[];
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

  // The toast already fired. A list shows "Try again" rather than an empty table that looks
  // like a pharmacy with nothing in it.
  if (isError) {
    return (
      <div className="empty">
        <p>Could not load the pharmacy catalog.</p>
        <button type="button" className="secondary" onClick={onRetry}>
          Try again
        </button>
      </div>
    );
  }

  if (items.length === 0) {
    return <p className="empty">Nothing matches this search.</p>;
  }

  return (
    <table>
      <thead>
        <tr>
          <th>Item</th>
          <th>Category</th>
          <th>Expires</th>
          <th>On hand</th>
          <th>Stock</th>
          {canManageEquipment(role) && <th>Movement</th>}
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
                {item.manufacturer ?? 'No manufacturer recorded'}
                {item.batch_number ? ` · batch ${item.batch_number}` : ''}
              </div>
            </td>
            <td>{item.category_name}</td>
            <td>
              <Expiry date={item.expiry_date} />
            </td>
            <td>
              <strong>{item.quantity_on_hand}</strong>{' '}
              <span className="muted small">{item.unit}</span>
            </td>
            <td>
              <StockBadge item={item} />
            </td>
            {canManageEquipment(role) && (
              <td>
                <MoveStockButton item={item} onChanged={onChanged} />
              </td>
            )}
          </tr>
        ))}
      </tbody>
    </table>
  );
}

// Three states, not two. "Low" is the reorder threshold the sweep will key off, and it is
// worth seeing before the shelf is actually empty.
function StockBadge({ item }: { item: PharmacyItem }) {
  if (!item.is_available) {
    return <span className="badge severity-high">Out of stock</span>;
  }

  if (item.below_threshold) {
    return <span className="badge status-maintenance">Low</span>;
  }

  return <span className="badge status-available">In stock</span>;
}

function Expiry({ date }: { date: string | null | undefined }) {
  if (!date) {
    return <span className="muted">Does not expire</span>;
  }

  const days = Math.ceil((new Date(date).getTime() - Date.now()) / 86_400_000);

  // 30 days is the window the plan gives the medicine_expiring warning, so the screen and
  // the sweep agree on what "expiring" means.
  if (days < 0) {
    return <span className="badge severity-high">Expired</span>;
  }

  return (
    <>
      {date}
      {days <= 30 && <div className="muted small">in {days} days</div>}
    </>
  );
}

function MoveStockButton({ item, onChanged }: { item: PharmacyItem; onChanged: () => void }) {
  const [open, setOpen] = useState(false);

  return (
    <>
      <button type="button" className="secondary" onClick={() => setOpen(true)}>
        Record movement
      </button>
      {open && (
        <StockDialog item={item} onClose={() => setOpen(false)} onChanged={onChanged} />
      )}
    </>
  );
}

function AddItemCard({
  categories,
  onDone,
  onCategoryAdded,
}: {
  categories: { id: string; name: string }[];
  onDone: () => void;
  onCategoryAdded: () => void;
}) {
  const [name, setName] = useState('');
  const [categoryId, setCategoryId] = useState('');
  const [manufacturer, setManufacturer] = useState('');
  const [batchNumber, setBatchNumber] = useState('');
  const [expiryDate, setExpiryDate] = useState('');
  const [unit, setUnit] = useState('box');
  const [quantity, setQuantity] = useState('0');
  const [threshold, setThreshold] = useState('10');
  const [unitPrice, setUnitPrice] = useState('');

  const [newCategory, setNewCategory] = useState('');
  const [prescription, setPrescription] = useState(false);
  const [categoryOpen, setCategoryOpen] = useState(false);

  const create = useMutation({
    ...createPharmacyItemMutation(),
    onSuccess: (item) => {
      toast.success(`${item.name} added to the catalog.`);
      setName('');
      setBatchNumber('');
      onDone();
    },
  });

  const addCategory = useMutation({
    ...createPharmacyCategoryMutation(),
    onSuccess: (category) => {
      toast.success(`Category ${category.name} added.`);
      setNewCategory('');
      setPrescription(false);
      setCategoryId(category.id);
      setCategoryOpen(false);
      onCategoryAdded();
    },
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    create.mutate({
      body: {
        name: name.trim(),
        category_id: categoryId,
        manufacturer: manufacturer.trim() || null,
        batch_number: batchNumber.trim() || null,
        expiry_date: expiryDate || null,
        unit: unit.trim(),
        quantity_on_hand: Number(quantity),
        reorder_threshold: Number(threshold),
        unit_price: unitPrice ? Number(unitPrice) : null,
      },
    });
  }

  return (
    <div className="card">
      <div className="dialog-head">
        <h2>Add to the catalog</h2>
        <button type="button" className="secondary" onClick={() => setCategoryOpen(true)}>
          New category
        </button>
      </div>

      <p className="muted" style={{ marginBottom: '0.9rem' }}>
        The quantity here is opening stock. After this, it only ever changes through a
        recorded movement, so the shelf and the history can never disagree.
      </p>

      {categories.length === 0 && (
        <p className="stub-note">
          <strong>No categories yet.</strong> An item needs one, so add a category first.
        </p>
      )}

      <form onSubmit={submit}>
        <div className="row">
          <div className="field">
            <label htmlFor="ph-name">Name</label>
            <input
              id="ph-name"
              value={name}
              maxLength={200}
              placeholder="Paracetamol 500mg"
              onChange={(event) => setName(event.target.value)}
              required
            />
          </div>
          <div className="field">
            <label htmlFor="ph-new-category">Category</label>
            <select
              id="ph-new-category"
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
            <label htmlFor="ph-unit">Unit</label>
            <input
              id="ph-unit"
              value={unit}
              maxLength={20}
              placeholder="box"
              onChange={(event) => setUnit(event.target.value)}
              required
            />
          </div>
        </div>

        <div className="row">
          <div className="field">
            <label htmlFor="ph-manufacturer">Manufacturer</label>
            <input
              id="ph-manufacturer"
              value={manufacturer}
              maxLength={150}
              placeholder="Optional"
              onChange={(event) => setManufacturer(event.target.value)}
            />
          </div>
          <div className="field">
            <label htmlFor="ph-batch">Batch number</label>
            <input
              id="ph-batch"
              value={batchNumber}
              maxLength={50}
              placeholder="Optional"
              onChange={(event) => setBatchNumber(event.target.value)}
            />
          </div>
          <div className="field">
            <label htmlFor="ph-expiry">Expiry date</label>
            <input
              id="ph-expiry"
              type="date"
              value={expiryDate}
              onChange={(event) => setExpiryDate(event.target.value)}
            />
          </div>
        </div>

        <div className="row">
          <div className="field">
            <label htmlFor="ph-qty">Opening stock</label>
            <input
              id="ph-qty"
              type="number"
              min={0}
              value={quantity}
              onChange={(event) => setQuantity(event.target.value)}
              required
            />
          </div>
          <div className="field">
            <label htmlFor="ph-threshold">Reorder threshold</label>
            <input
              id="ph-threshold"
              type="number"
              min={0}
              value={threshold}
              onChange={(event) => setThreshold(event.target.value)}
              required
            />
          </div>
          <div className="field">
            <label htmlFor="ph-price">Unit price</label>
            <input
              id="ph-price"
              type="number"
              min={0}
              step="0.01"
              value={unitPrice}
              placeholder="Optional"
              onChange={(event) => setUnitPrice(event.target.value)}
            />
          </div>
        </div>

        <button
          type="submit"
          disabled={
            create.isPending || name.trim().length === 0 || categoryId.length === 0
          }
        >
          {create.isPending ? 'Adding…' : 'Add item'}
        </button>
      </form>

      {categoryOpen && (
        <Dialog title="New pharmacy category" onClose={() => setCategoryOpen(false)}>
          <p className="muted">
            Whether a category needs a prescription is recorded here. Deciding what a patient
            should actually be given is clinical staff&rsquo;s call, never this component&rsquo;s.
          </p>
          <div className="field">
            <label htmlFor="ph-cat-name">Name</label>
            <input
              id="ph-cat-name"
              value={newCategory}
              maxLength={150}
              placeholder="Chronic Medicines"
              onChange={(event) => setNewCategory(event.target.value)}
            />
          </div>
          <div className="field">
            <label htmlFor="ph-cat-rx">Prescription</label>
            <select
              id="ph-cat-rx"
              value={prescription ? 'yes' : 'no'}
              onChange={(event) => setPrescription(event.target.value === 'yes')}
            >
              <option value="no">Not required</option>
              <option value="yes">Required</option>
            </select>
          </div>
          <div className="actions">
            <button
              type="button"
              disabled={addCategory.isPending || newCategory.trim().length === 0}
              onClick={() =>
                addCategory.mutate({
                  body: {
                    name: newCategory.trim(),
                    requires_prescription: prescription,
                  },
                })
              }
            >
              {addCategory.isPending ? 'Adding…' : 'Add category'}
            </button>
            <button type="button" className="secondary" onClick={() => setCategoryOpen(false)}>
              Cancel
            </button>
          </div>
        </Dialog>
      )}
    </div>
  );
}
