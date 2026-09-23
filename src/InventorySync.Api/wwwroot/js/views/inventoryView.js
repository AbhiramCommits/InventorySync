import { DataTable } from '../components/DataTable.js';
import { FilterBar } from '../components/FilterBar.js';
import { Pager } from '../components/Pager.js';
import { el, clearElement, formatCurrency, formatDateTime, formatInt } from '../utils.js';

const WAREHOUSES = ['WH01', 'WH02', 'WH03', 'WH04', 'WH05', 'WH06', 'WH07', 'WH08'];
const PAGE_SIZE = 20;

export class InventoryView {
  constructor(context) {
    this.ctx = context;
    this.state = { search: '', warehouseCode: '', sort: 'sku', page: 1, pageSize: PAGE_SIZE };
    this.rows = [];
    this.editing = null;
    this.loadControllers = new Set();
  }

  async mount(container) {
    this.container = container;
    clearElement(container);

    const header = el('header', 'view-header');
    header.append(el('h1', '', 'Inventory'));
    header.append(el('p', 'view-subtitle', 'Items synchronised from the ERP system. Edit quantities and unit costs inline.'));

    const filterHost = el('div');
    const tableHost = el('div', 'card');
    const pagerHost = el('div');

    container.append(header, filterHost, tableHost, pagerHost);

    this.filterBar = new FilterBar({
      fields: [
        {
          key: 'search',
          type: 'search',
          label: 'SKU search',
          placeholder: 'Search SKUs…',
        },
        {
          key: 'warehouseCode',
          type: 'select',
          label: 'Warehouse',
          options: [{ value: '', label: 'All warehouses' }, ...WAREHOUSES.map((w) => ({ value: w, label: w }))],
        },
      ],
      debounceMs: 300,
      onChange: ({ key, value }) => {
        this.state[key] = value ?? '';
        this.state.page = 1;
        this.load();
      },
    });
    this.filterBar.mount(filterHost);

    this.table = new DataTable({
      columns: [
        { key: 'sku', label: 'SKU', sortable: true, render: (row) => el('strong', 'mono', row.sku) },
        { key: 'name', label: 'Name', sortable: true },
        { key: 'warehouseCode', label: 'Warehouse', sortable: true },
        {
          key: 'quantityOnHand',
          label: 'Qty on hand',
          sortable: true,
          align: 'right',
          render: (row) => this.editableCell(row, 'quantityOnHand', 'int'),
        },
        {
          key: 'unitCost',
          label: 'Unit cost',
          sortable: true,
          align: 'right',
          render: (row) => this.editableCell(row, 'unitCost', 'decimal'),
        },
        { key: 'lastSyncedUtc', label: 'Last synced', sortable: true, render: (row) => formatDateTime(row.lastSyncedUtc) },
        {
          key: 'locallyModifiedUtc',
          label: 'Local edits',
          render: (row) => (row.locallyModifiedUtc ? el('span', 'badge badge--warning', 'Modified locally') : el('span', '', '')),
        },
        {
          key: 'actions',
          label: '',
          render: (row) => this.actionsCell(row),
        },
      ],
      onSort: ({ key, dir }) => {
        this.state.sort = dir ? (dir === 'desc' ? `-${key}` : key) : 'sku';
        this.state.page = 1;
        this.load();
      },
      emptyMessage: 'No inventory items match your filters.',
    });
    this.table.mount(tableHost);

    this.pager = new Pager({
      onPage: (page) => {
        this.state.page = page;
        this.load();
      },
    });
    this.pager.mount(pagerHost);

    await this.load();
  }

  editableCell(row, field, kind) {
    const wrap = el('span', 'editable');
    const value = el('span', '', kind === 'decimal' ? formatCurrency(row[field]) : formatInt(row[field]));
    const editBtn = el('button', 'icon-btn', '✎');
    editBtn.type = 'button';
    editBtn.setAttribute('aria-label', `Edit ${field === 'quantityOnHand' ? 'quantity on hand' : 'unit cost'} for ${row.sku}`);
    editBtn.addEventListener('click', (event) => {
      event.stopPropagation();
      this.beginEdit(row, field, wrap, kind);
    });
    wrap.append(value, editBtn);
    return wrap;
  }

  actionsCell(row) {
    const wrap = el('span', 'editable');
    const deleteBtn = el('button', 'icon-btn icon-btn--danger', '🗑');
    deleteBtn.type = 'button';
    deleteBtn.setAttribute('aria-label', `Delete ${row.sku}`);
    deleteBtn.addEventListener('click', (event) => {
      event.stopPropagation();
      this.deleteRow(row);
    });
    wrap.append(deleteBtn);
    return wrap;
  }

  beginEdit(row, field, wrap, kind) {
    if (this.editing) this.cancelEdit();

    const input = el('input', 'edit-input');
    input.type = 'number';
    input.step = kind === 'int' ? '1' : '0.0001';
    input.min = '0';
    input.value = String(row[field] ?? '');

    const save = el('button', 'btn btn--small btn--primary', 'Save');
    save.type = 'button';
    const cancel = el('button', 'btn btn--small', 'Cancel');
    cancel.type = 'button';

    clearElement(wrap);
    wrap.append(input, save, cancel);
    input.focus();
    input.select();

    this.editing = { row, field, wrap, kind, input };

    const commit = async () => {
      const parsed = kind === 'int' ? parseInt(input.value, 10) : parseFloat(input.value);
      if (!Number.isFinite(parsed) || parsed < 0) {
        this.cancelEdit();
        return;
      }

      const previous = row[field];
      this.editing = null;
      row[field] = parsed;
      this.table.setData(this.rows);

      try {
        const updated = await this.ctx.api.updateInventoryItem(row.id, {
          name: row.name,
          description: row.description ?? null,
          quantityOnHand: row.quantityOnHand,
          unitCost: row.unitCost,
          warehouseCode: row.warehouseCode,
          rowVersion: row.rowVersion ?? null,
        });
        Object.assign(row, updated);
        this.table.setData(this.rows);
        this.ctx.toast.show(`${row.sku} updated`, { type: 'success' });
      } catch (err) {
        row[field] = previous;
        this.table.setData(this.rows);
        this.ctx.toast.show(`Update failed for ${row.sku}: ${err.message}`, { type: 'error' });
      }
    };

    save.addEventListener('click', commit);
    cancel.addEventListener('click', () => this.cancelEdit());
    input.addEventListener('keydown', (event) => {
      if (event.key === 'Enter') {
        event.preventDefault();
        commit();
      } else if (event.key === 'Escape') {
        this.cancelEdit();
      }
    });
  }

  cancelEdit() {
    this.editing = null;
    this.table.setData(this.rows);
  }

  async deleteRow(row) {
    const confirmed = await this.ctx.confirm.confirm({
      title: `Delete ${row.sku}?`,
      message: `This permanently removes “${row.name}” from inventory. This action cannot be undone.`,
      confirmLabel: 'Delete',
      danger: true,
    });

    if (!confirmed) return;

    try {
      await this.ctx.api.deleteInventoryItem(row.id);
      this.ctx.toast.show(`${row.sku} deleted`, { type: 'success' });
      if (this.rows.length === 1 && this.state.page > 1) this.state.page -= 1;
      await this.load();
    } catch (err) {
      this.ctx.toast.show(`Delete failed for ${row.sku}: ${err.message}`, { type: 'error' });
    }
  }

  async load() {
    for (const controller of this.loadControllers) controller.abort();
    const controller = new AbortController();
    this.loadControllers.add(controller);
    this.table.setLoading();

    try {
      const result = await this.ctx.api.getInventory({
        search: this.state.search || null,
        warehouseCode: this.state.warehouseCode || null,
        sort: this.state.sort,
        page: this.state.page,
        pageSize: this.state.pageSize,
        signal: controller.signal,
      });

      this.rows = result.items ?? [];
      this.table.setData(this.rows);
      this.pager.update({
        page: result.page,
        pageSize: result.pageSize,
        totalCount: result.totalCount,
        totalPages: result.totalPages,
      });
    } catch (err) {
      if (err.name === 'AbortError') return;
      this.table.setError(err.message);
      this.pager.update({ page: this.state.page, pageSize: this.state.pageSize, totalCount: 0, totalPages: 0 });
    } finally {
      this.loadControllers.delete(controller);
    }
  }

  destroy() {
    for (const controller of this.loadControllers) controller.abort();
    this.editing = null;
    this.filterBar?.destroy();
    this.table?.destroy();
    this.pager?.destroy();
    if (this.container) clearElement(this.container);
  }
}
