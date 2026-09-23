import { DataTable } from '../components/DataTable.js';
import { FilterBar } from '../components/FilterBar.js';
import { Pager } from '../components/Pager.js';
import { statusBadgeNode, RUN_STATUS_LABELS } from '../components/StatusBadge.js';
import { el, clearElement, formatDateTime, formatDuration, formatInt, sleep } from '../utils.js';

const PAGE_SIZE = 20;
const POLL_INTERVAL_MS = 2000;

const STATUS_OPTIONS = [
  { value: '', label: 'All statuses' },
  { value: '0', label: 'Running' },
  { value: '1', label: 'Succeeded' },
  { value: '2', label: 'Failed' },
  { value: '3', label: 'Partial success' },
];

const ENTITY_OPTIONS = [
  { value: '', label: 'All entities' },
  { value: '0', label: 'Inventory' },
  { value: '1', label: 'Purchase orders' },
];

export class RunsView {
  constructor(context) {
    this.ctx = context;
    this.state = { entityType: null, status: null, page: 1, pageSize: PAGE_SIZE };
    this.rows = [];
    this.syncing = false;
    this.activeRun = null;
    this.loadControllers = new Set();
    this.pollControllers = new Set();
  }

  async mount(container) {
    this.container = container;
    clearElement(container);

    const header = el('header', 'view-header');
    header.append(el('h1', '', 'Sync runs'));

    const actions = el('div', 'view-actions');
    this.runInventoryBtn = el('button', 'btn btn--primary', 'Run inventory sync');
    this.runInventoryBtn.type = 'button';
    this.runPoBtn = el('button', 'btn', 'Run PO sync');
    this.runPoBtn.type = 'button';
    this.runningNote = el('span', 'running-note');
    this.runningNote.hidden = true;

    this.runInventoryBtn.addEventListener('click', () => this.trigger('inventory'));
    this.runPoBtn.addEventListener('click', () => this.trigger('purchase-orders'));

    actions.append(this.runningNote, this.runInventoryBtn, this.runPoBtn);
    header.append(actions);

    const filterHost = el('div');
    const tableHost = el('div', 'card');
    const pagerHost = el('div');

    container.append(header, filterHost, tableHost, pagerHost);

    this.filterBar = new FilterBar({
      fields: [
        { key: 'entityType', type: 'select', label: 'Entity', options: ENTITY_OPTIONS },
        { key: 'status', type: 'select', label: 'Status', options: STATUS_OPTIONS },
      ],
      onChange: ({ key, value }) => {
        this.state[key] = value ?? null;
        this.state.page = 1;
        this.load();
      },
    });
    this.filterBar.mount(filterHost);

    this.table = new DataTable({
      columns: [
        { key: 'id', label: 'Run', align: 'right', render: (row) => el('span', 'mono', `#${row.id}`) },
        { key: 'entityType', label: 'Entity', render: (row) => statusBadgeNode('entity', row.entityType) },
        { key: 'status', label: 'Status', render: (row) => statusBadgeNode('run', row.status) },
        { key: 'startedUtc', label: 'Started', render: (row) => formatDateTime(row.startedUtc) },
        {
          key: 'duration',
          label: 'Duration',
          render: (row) => formatDuration(row.startedUtc, row.completedUtc),
        },
        { key: 'recordsRead', label: 'Read', align: 'right', render: (row) => formatInt(row.recordsRead) },
        { key: 'recordsInserted', label: 'Inserted', align: 'right', render: (row) => formatInt(row.recordsInserted) },
        { key: 'recordsUpdated', label: 'Updated', align: 'right', render: (row) => formatInt(row.recordsUpdated) },
        {
          key: 'recordsFailed',
          label: 'Failed',
          align: 'right',
          render: (row) => (row.recordsFailed > 0
            ? el('span', 'badge badge--danger', formatInt(row.recordsFailed))
            : el('span', '', '0')),
        },
        {
          key: 'triggeredBy',
          label: 'Triggered by',
          render: (row) => row.triggeredBy,
        },
        {
          key: 'parentSyncRunId',
          label: 'Parent',
          render: (row) => (row.parentSyncRunId != null
            ? el('span', 'badge badge--info', `retry of #${row.parentSyncRunId}`)
            : el('span', '', '')),
        },
      ],
      onRowClick: (row) => this.ctx.router.navigate(`/runs/${row.id}`),
      getRowId: (row) => row.id,
      emptyMessage: 'No sync runs yet. Trigger one with the buttons above.',
    });
    this.table.mount(tableHost);

    this.pager = new Pager({
      onPage: (page) => {
        this.state.page = page;
        this.load();
      },
    });
    this.pager.mount(pagerHost);

    this.renderButtons();
    await this.load();
  }

  renderButtons() {
    if (!this.runInventoryBtn) return;
    this.runInventoryBtn.disabled = this.syncing;
    this.runPoBtn.disabled = this.syncing;

    if (this.syncing && this.activeRun) {
      this.runInventoryBtn.textContent = 'Sync running…';
      this.runPoBtn.textContent = 'Sync running…';
      this.runningNote.hidden = false;
      const spinner = el('span', 'spinner');
      spinner.setAttribute('aria-hidden', 'true');
      this.runningNote.replaceChildren(
        spinner,
        el('span', '', `Run #${this.activeRun.id}: ${RUN_STATUS_LABELS[this.activeRun.status] ?? '…'}`),
      );
    } else {
      this.runInventoryBtn.textContent = 'Run inventory sync';
      this.runPoBtn.textContent = 'Run PO sync';
      this.runningNote.hidden = true;
    }
  }

  async trigger(kind) {
    if (this.syncing) return;
    this.syncing = true;
    this.activeRun = null;
    this.renderButtons();

    try {
      const run = kind === 'inventory'
        ? await this.ctx.api.runInventorySync('admin-ui')
        : await this.ctx.api.runPurchaseOrderSync('admin-ui');

      this.activeRun = run;
      this.renderButtons();
      this.ctx.toast.show(`Sync started (run #${run.id})`, { type: 'info' });

      const finalRun = await this.pollRun(run.id);
      if (finalRun) {
        const label = RUN_STATUS_LABELS[finalRun.status] ?? 'finished';
        const failed = finalRun.recordsFailed > 0;
        this.ctx.toast.show(
          `Run #${finalRun.id} ${label} — ${formatInt(finalRun.recordsInserted)} inserted, ${formatInt(finalRun.recordsUpdated)} updated${failed ? `, ${formatInt(finalRun.recordsFailed)} failed` : ''}`,
          { type: failed ? 'warning' : 'success', duration: 8000 },
        );
      }
    } catch (err) {
      if (err.name !== 'AbortError') {
        this.ctx.toast.show(`Sync failed: ${err.message}`, { type: 'error' });
      }
    } finally {
      this.syncing = false;
      this.activeRun = null;
      this.renderButtons();
      await this.load();
    }
  }

  async pollRun(id) {
    const controller = new AbortController();
    this.pollControllers.add(controller);

    try {
      for (;;) {
        await sleep(POLL_INTERVAL_MS, controller.signal);
        const run = await this.ctx.api.getRun(id, { signal: controller.signal, silent: true });
        this.activeRun = run;
        this.renderButtons();
        if (run.status !== 0) return run;
      }
    } catch (err) {
      if (err.name === 'AbortError') return null;
      this.ctx.toast.show(`Lost contact while polling run #${id}: ${err.message}`, { type: 'error' });
      return null;
    } finally {
      this.pollControllers.delete(controller);
    }
  }

  async load() {
    for (const controller of this.loadControllers) controller.abort();
    const controller = new AbortController();
    this.loadControllers.add(controller);
    this.table.setLoading();

    try {
      const result = await this.ctx.api.getRuns({
        entityType: this.state.entityType,
        status: this.state.status,
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
    for (const controller of this.pollControllers) controller.abort();
    this.filterBar?.destroy();
    this.table?.destroy();
    this.pager?.destroy();
    if (this.container) clearElement(this.container);
  }
}
