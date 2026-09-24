import { Table } from '../components/Table';
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
import { ActionDialog } from '../components/ui/action-dialog';
import { StockDialog } from './pharmacy/StockDialog';
import { ItemHistoryCard } from './pharmacy/ItemHistoryCard';
import { AddBatchDialog, BatchList } from './pharmacy/BatchList';
import { RemoveMedicineDialog } from './pharmacy/RemoveMedicineDialog';
import { PrescriptionsCard } from './pharmacy/PrescriptionsCard';
import { ReorderSuggestionPanel } from './pharmacy/ReorderSuggestionPanel';
import { AppSelect } from '../components/ui/app-select';

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
  const [batchesItem, setBatchesItem] = useState<PharmacyItem | null>(null);
  const [suggestingItem, setSuggestingItem] = useState<PharmacyItem | null>(null);
  const [addOpen, setAddOpen] = useState(false);

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

      {canManageEquipment(role) && <PrescriptionsCard />}

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
            <AppSelect
              id="ph-category"
              label="Category"
              value={categoryId}
              onValueChange={(value) => {
                setCategoryId(value);
                page1();
              }}
              options={[
                { value: '', label: 'All categories' },
                ...(categories.data ?? []).map((category) => ({ value: category.id, label: category.name })),
              ]}
            />
          </div>
          <div>
            <AppSelect
              id="ph-available"
              label="Availability"
              value={availableOnly ? 'in-stock' : 'all'}
              onValueChange={(value) => {
                setAvailableOnly(value === 'in-stock');
                page1();
              }}
              options={[
                { value: 'all', label: 'Everything in the catalog' },
                { value: 'in-stock', label: 'On the shelf now' },
              ]}
            />
          </div>
        </div>
        <p className="hint">
          Availability is worked out from the quantity when you read it, never stored as its
          own column, so it cannot fall out of step with what is on the shelf.
        </p>
      </div>

      {canManageEquipment(role) && (
        <>
        <button type="button" onClick={() => setAddOpen(true)}>Add medicine</button>
        <ActionDialog title="Add medicine" isOpen={addOpen} onClose={() => setAddOpen(false)}>
        {addOpen && <AddItemCard
          categories={(categories.data ?? []).map((c) => ({ id: c.id, name: c.name }))}
          onDone={() => {
            refresh();
            page1();
            setAddOpen(false);
          }}
          onCategoryAdded={() =>
            queryClient.invalidateQueries({
              predicate: (query) =>
                (query.queryKey[0] as { _id?: string } | undefined)?._id ===
                'listPharmacyCategories',
            })
          }
        />}
        </ActionDialog>
        </>
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
          onOpenBatches={setBatchesItem}
          onSuggest={setSuggestingItem}
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

      <ActionDialog title="Medicine details" isOpen={selected != null} onClose={() => setSelected(null)}>
        {selected && <ItemHistoryCard id={selected} onClose={() => setSelected(null)} />}
      </ActionDialog>
      <ActionDialog title={`Batches · ${batchesItem?.name ?? 'medicine'}`} isOpen={batchesItem != null} onClose={() => setBatchesItem(null)}>
        {batchesItem && <BatchList item={batchesItem} manage={canManageEquipment(role)} onChanged={refresh} />}
      </ActionDialog>
      <ActionDialog title={`Suggest threshold · ${suggestingItem?.name ?? 'medicine'}`} isOpen={suggestingItem != null} onClose={() => setSuggestingItem(null)}>
        {suggestingItem && <ReorderSuggestionPanel item={suggestingItem} onClose={() => setSuggestingItem(null)} onChanged={refresh} />}
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
  onOpenBatches,
  onSuggest,
  role,
  onChanged,
}: {
  items: PharmacyItem[];
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  selectedId: string | null;
  onSelect: (id: string) => void;
  onOpenBatches: (item: PharmacyItem) => void;
  onSuggest: (item: PharmacyItem) => void;
  role: PrincipalRole | undefined;
  onChanged: () => void;
}) {
  if (isLoading) {
    return <p className="empty">Loading…</p>;
  }

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
    <Table>
      <thead>
        <tr>
          <th>Item</th>
          <th>Category</th>
          <th>Expires first</th>
          <th>On hand</th>
          <th>Stock</th>
          {canManageEquipment(role) && <th>Movement</th>}
        </tr>
      </thead>
      <tbody>
        {items.map((item) => (
          <ItemRows
            key={item.id}
            item={item}
            selectedId={selectedId}
            onSelect={onSelect}
            onOpenBatches={onOpenBatches}
            onSuggest={onSuggest}
            role={role}
            onChanged={onChanged}
          />
        ))}
      </tbody>
    </Table>
  );
}

// One medicine: its own row, and under it every batch once the arrow is opened.
function ItemRows({
  item,
  selectedId,
  onSelect,
  onOpenBatches,
  onSuggest,
  role,
  onChanged,
}: {
  item: PharmacyItem;
  selectedId: string | null;
  onSelect: (id: string) => void;
  onOpenBatches: (item: PharmacyItem) => void;
  onSuggest: (item: PharmacyItem) => void;
  role: PrincipalRole | undefined;
  onChanged: () => void;
}) {
  const manage = canManageEquipment(role);

  return (
      <tr className={selectedId === item.id ? 'selected' : undefined}>
        <td>
          <button type="button" className="linklike" onClick={() => onSelect(item.id)}>
            <strong>{item.name}</strong>
          </button>
          <div className="muted small">{item.manufacturer ?? 'No manufacturer recorded'}</div>
          {item.batch_count > 0 && (
            <button
              type="button"
              className="linklike small"
              onClick={() => onOpenBatches(item)}
            >
              View {item.batch_count} {item.batch_count === 1 ? 'batch' : 'batches'}
            </button>
          )}
        </td>
        <td>{item.category_name}</td>
        <td>
          <Expiry date={item.earliest_expiry} />
        </td>
        <td>
          <strong>{item.quantity_on_hand}</strong>{' '}
          <span className="muted small">{item.unit}</span>
        </td>
        <td>
          <StockBadge item={item} />
        </td>
        {manage && (
          <td>
            <MoveStockButton item={item} onChanged={onChanged} />{' '}
            <button type="button" className="secondary" onClick={() => onSuggest(item)}>
              Suggest threshold
            </button>
          </td>
        )}
      </tr>
  );
}

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
  const [adding, setAdding] = useState(false);
  const [removing, setRemoving] = useState(false);

  return (
    <>
      <button type="button" className="secondary" onClick={() => setAdding(true)}>
        Add batch
      </button>{' '}
      <button type="button" className="secondary" onClick={() => setOpen(true)}>
        Record movement
      </button>{' '}
      <button
        type="button"
        className="secondary danger"
        title={
          item.quantity_on_hand > 0
            ? 'Dispense or write off the rest of the stock first'
            : undefined
        }
        disabled={item.quantity_on_hand > 0}
        onClick={() => setRemoving(true)}
      >
        Remove
      </button>
      {open && (
        <StockDialog item={item} onClose={() => setOpen(false)} onChanged={onChanged} />
      )}
      {adding && (
        <AddBatchDialog item={item} onClose={() => setAdding(false)} onChanged={onChanged} />
      )}
      {removing && (
        <RemoveMedicineDialog
          item={item}
          onClose={() => setRemoving(false)}
          onDone={() => {
            setRemoving(false);
            onChanged();
          }}
        />
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
        recorded movement, so the shelf and the history can never disagree. The reorder
        threshold starts at 10 — there is no dispensing history yet for Suggest threshold to
        reason from, so set it once the medicine has some.
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
            <AppSelect
              id="ph-new-category"
              label="Category"
              value={categoryId}
              onValueChange={setCategoryId}
              isRequired
              options={[
                { value: '', label: 'Choose…' },
                ...categories.map((category) => ({ value: category.id, label: category.name })),
              ]}
            />
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
            <AppSelect
              id="ph-cat-rx"
              label="Prescription"
              value={prescription ? 'yes' : 'no'}
              onValueChange={(value) => setPrescription(value === 'yes')}
              options={[
                { value: 'no', label: 'Not required' },
                { value: 'yes', label: 'Required' },
              ]}
            />
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
