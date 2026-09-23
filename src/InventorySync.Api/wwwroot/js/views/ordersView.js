import { DataTable } from '../components/DataTable.js';
import { FilterBar } from '../components/FilterBar.js';
import { Pager } from '../components/Pager.js';
import { statusBadgeNode } from '../components/StatusBadge.js';
import { el, clearElement, formatCurrency, formatDate, formatInt } from '../utils.js';

const PAGE_SIZE = 20;

const STATUS_OPTIONS = [
  { value: '', label: 'All statuses' },
  { value: '0', label: 'Draft' },
  { value: '1', label: 'Submitted' },
  { value: '2', label: 'Partially received' },
  { value: '3', label: 'Received' },
  { value: '4', label: 'Cancelled' },
];

export class OrdersView {
  constructor(context) {
    this.ctx = context;
    this.state = { search: '', status: null, sort: 'poNumber', page: 1, pageSize: PAGE_SIZE };
    this.rows = [];
    this.expanded = new Set();
    this.orderDetails = new Map();
    this.loadControllers = new Set();
    this.detailControllers = new Map();
  }

  async mount(container) {
    this.container = container;
    clearElement(container);

    const header = el('header', 'view-header');
    header.append(el('h1', '', 'Purchase orders'));
    header.append(el('p', 'view-subtitle', 'Click a row to load its lines.'));

    const filterHost = el('div');
    const tableHost = el('div', 'card');
    const pagerHost = el('div');

    container.append(header, filterHost, tableHost, pagerHost);

    this.filterBar = new FilterBar({
      fields: [
        {
          key: 'search',
          type: 'search',
          label: 'PO search',
          placeholder: 'Search PO numbers…',
        },
        {
          key: 'status',
          type: 'select',
          label: 'Status',
          options: STATUS_OPTIONS,
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
        { key: 'poNumber', label: 'PO number', sortable: true, render: (row) => el('strong', 'mono', row.poNumber) },
        { key: 'vendorCode', label: 'Vendor', sortable: true, render: (row) => el('span', 'mono', row.vendorCode) },
        { key: 'status', label: 'Status', sortable: true, render: (row) => statusBadgeNode('po', row.status) },
        { key: 'orderDateUtc', label: 'Order date', sortable: true, render: (row) => formatDate(row.orderDateUtc) },
        { key: 'expectedDateUtc', label: 'Expected', sortable: true, render: (row) => formatDate(row.expectedDateUtc) },
        { key: 'totalAmount', label: 'Total', sortable: true, align: 'right', render: (row) => formatCurrency(row.totalAmount) },
        {
          key: 'locallyModifiedUtc',
          label: 'Local edits',
          render: (row) => (row.locallyModifiedUtc ? el('span', 'badge badge--warning', 'Modified') : el('span', '', '')),
        },
        { key: 'lines', label: '', render: (row) => el('span', 'icon-btn', this.expanded.has(row.id) ? '▾' : '▸') },
      ],
      onSort: ({ key, dir }) => {
        this.state.sort = dir ? (dir === 'desc' ? `-${key}` : key) : 'poNumber';
        this.state.page = 1;
        this.load();
      },
      onRowClick: (row) => this.toggleOrder(row),
      getRowId: (row) => row.id,
      emptyMessage: 'No purchase orders match your filters.',
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

  toggleOrder(order) {
    const id = order.id;

    if (this.expanded.has(id)) {
      this.expanded.delete(id);
      this.table.setDetail(id, null);
      return;
    }

    this.expanded.add(id);
    this.table.setDetail(id, this.detailLoadingNode());

    const cached = this.orderDetails.get(id);
    if (cached) {
      this.table.setDetail(id, this.linesNode(cached));
      return;
    }

    this.fetchOrderDetail(order);
  }

  async fetchOrderDetail(order) {
    const controller = new AbortController();
    this.detailControllers.set(order.id, controller);

    try {
      const full = await this.ctx.api.getOrder(order.id, { signal: controller.signal });
      this.orderDetails.set(order.id, full);
      if (this.expanded.has(order.id)) {
        this.table.setDetail(order.id, this.linesNode(full));
      }
    } catch (err) {
      if (err.name === 'AbortError') return;
      if (this.expanded.has(order.id)) {
        this.table.setDetail(order.id, this.detailErrorNode(err.message));
      }
    } finally {
      this.detailControllers.delete(order.id);
    }
  }

  detailLoadingNode() {
    const node = el('div', 'table-state', 'Loading lines…');
    const spinner = el('span', 'spinner');
    spinner.setAttribute('aria-hidden', 'true');
    node.prepend(spinner);
    return node;
  }

  detailErrorNode(message) {
    return el('div', 'table-state table-state--error', `⚠ ${message}`);
  }

  linesNode(order) {
    const wrap = el('div');
    wrap.append(el('p', 'order-lines-title', `${order.lines?.length ?? 0} line(s) · vendor ${order.vendorCode}`));

    const table = el('table', 'table table--dense');
    const thead = el('thead');
    const headRow = el('tr');
    for (const label of ['SKU', 'Ordered', 'Received', 'Unit price', 'Line total']) {
      headRow.append(el('th', '', label));
    }
    thead.append(headRow);
    table.append(thead);

    const tbody = el('tbody');
    for (const line of order.lines ?? []) {
      const tr = el('tr');
      tr.append(
        el('td', 'mono', line.sku),
        el('td', 'num', formatInt(line.quantityOrdered)),
        el('td', 'num', formatInt(line.quantityReceived)),
        el('td', 'num', formatCurrency(line.unitPrice)),
        el('td', 'num', formatCurrency(line.quantityOrdered * line.unitPrice)),
      );
      tbody.append(tr);
    }
    table.append(tbody);
    wrap.append(table);
    return wrap;
  }

  async load() {
    for (const controller of this.loadControllers) controller.abort();
    const controller = new AbortController();
    this.loadControllers.add(controller);
    this.table.setLoading();

    try {
      const result = await this.ctx.api.getOrders({
        search: this.state.search || null,
        status: this.state.status,
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
    for (const controller of this.detailControllers.values()) controller.abort();
    this.filterBar?.destroy();
    this.table?.destroy();
    this.pager?.destroy();
    if (this.container) clearElement(this.container);
  }
}
