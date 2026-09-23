import { DataTable } from '../components/DataTable.js';
import { Pager } from '../components/Pager.js';
import { SyncRunTimeline } from '../components/SyncRunTimeline.js';
import { statusBadgeNode } from '../components/StatusBadge.js';
import { el, clearElement, formatDateTime } from '../utils.js';

const PAGE_SIZE = 50;

const AUDIT_ACTIONS = [
  { value: null, label: 'All' },
  { value: 0, label: 'Insert' },
  { value: 1, label: 'Update' },
  { value: 2, label: 'Skip' },
  { value: 3, label: 'Conflict' },
  { value: 4, label: 'Error' },
];

export class RunDetailView {
  constructor(context, runId) {
    this.ctx = context;
    this.runId = runId;
    this.run = null;
    this.action = null;
    this.page = 1;
    this.loadControllers = new Set();
  }

  async mount(container) {
    this.container = container;
    clearElement(container);

    const back = el('a', 'link-back', '← Back to sync runs');
    back.href = '#/runs';

    const header = el('header', 'view-header');
    header.append(el('h1', '', `Sync run #${this.runId}`));

    const summaryHost = el('div', 'card');
    const retryHost = el('div', 'view-actions');
    const sectionTitle = el('h2', 'section-title', 'Audit entries');
    const chipsHost = el('div');
    const tableHost = el('div', 'card');
    const pagerHost = el('div');

    container.append(back, header, summaryHost, retryHost, sectionTitle, chipsHost, tableHost, pagerHost);

    this.timeline = new SyncRunTimeline({
      run: null,
      onNavigate: (path) => this.ctx.router.navigate(path),
    });

    this.summaryHost = summaryHost;
    this.retryHost = retryHost;
    this.chipsHost = chipsHost;
    this.buildActionChips();

    this.table = new DataTable({
      columns: [
        { key: 'action', label: 'Action', render: (row) => statusBadgeNode('audit', row.action) },
        { key: 'entityKey', label: 'Entity key', render: (row) => el('span', 'mono', row.entityKey) },
        { key: 'fieldName', label: 'Field', render: (row) => row.fieldName ?? '—' },
        {
          key: 'change',
          label: 'Old → New',
          render: (row) => this.changeCell(row),
        },
        { key: 'message', label: 'Message', render: (row) => row.message ?? '' },
        { key: 'timestampUtc', label: 'Timestamp', render: (row) => formatDateTime(row.timestampUtc) },
      ],
      getRowId: (row) => row.id,
      emptyMessage: 'No audit entries for this filter.',
    });
    this.table.mount(tableHost);

    this.pager = new Pager({
      onPage: (page) => {
        this.page = page;
        this.loadAudits();
      },
    });
    this.pager.mount(pagerHost);

    await this.loadRun();
    await this.loadAudits();
  }

  buildActionChips() {
    const chips = el('div', 'chips');
    chips.setAttribute('role', 'group');
    chips.setAttribute('aria-label', 'Filter audit entries by action');

    for (const option of AUDIT_ACTIONS) {
      const chip = el('button', 'chip', option.label);
      chip.type = 'button';
      chip.dataset.value = option.value === null ? '' : String(option.value);
      chip.setAttribute('aria-pressed', option.value === null ? 'true' : 'false');
      chip.addEventListener('click', () => {
        this.action = option.value;
        this.page = 1;
        this.updateChips(chips);
        this.loadAudits();
      });
      chips.append(chip);
    }

    clearElement(this.chipsHost);
    this.chipsHost.append(chips);
  }

  updateChips(chips) {
    for (const chip of chips.children) {
      const selected = chip.dataset.value === (this.action === null ? '' : String(this.action));
      chip.setAttribute('aria-pressed', selected ? 'true' : 'false');
    }
  }

  changeCell(row) {
    if (row.action === 1) {
      return this.diffNode(row.oldValue, row.newValue);
    }

    if (row.oldValue != null && row.oldValue !== '') {
      return el('span', 'mono', row.oldValue);
    }
    if (row.newValue != null && row.newValue !== '') {
      return el('span', 'mono', row.newValue);
    }
    return el('span', '', '—');
  }

  diffNode(oldValue, newValue) {
    const wrap = el('span', 'diff');
    const hasOld = oldValue !== null && oldValue !== undefined && oldValue !== '';
    const hasNew = newValue !== null && newValue !== undefined && newValue !== '';

    if (hasOld) wrap.append(el('del', '', oldValue));
    if (hasOld && hasNew) wrap.append(el('span', 'diff-arrow', '→'));
    if (hasNew) wrap.append(el('ins', '', newValue));
    if (!hasOld && !hasNew) wrap.append(el('span', '', '—'));
    return wrap;
  }

  async loadRun() {
    try {
      this.run = await this.ctx.api.getRun(this.runId);
      this.timeline.run = this.run;
      this.timeline.mount(this.summaryHost);
      this.renderRetry();
      document.title = `Run #${this.runId} · InventorySync`;
    } catch (err) {
      this.ctx.toast.show(`Failed to load run #${this.runId}: ${err.message}`, { type: 'error' });
      this.summaryHost.append(el('div', 'table-state table-state--error', `⚠ ${err.message}`));
    }
  }

  renderRetry() {
    clearElement(this.retryHost);
    if (!this.run || this.run.recordsFailed <= 0 || this.run.parentSyncRunId != null) return;

    const button = el('button', 'btn btn--primary', 'Retry failed records');
    button.type = 'button';
    button.addEventListener('click', () => this.retryFailed());
    this.retryHost.append(button);
  }

  async retryFailed() {
    const button = this.retryHost.querySelector('button');
    if (button) {
      button.disabled = true;
      button.textContent = 'Retrying…';
    }

    try {
      const child = await this.ctx.api.retryRun(this.runId);
      this.ctx.toast.show(`Retry started as run #${child.id}`, { type: 'info' });
      this.ctx.router.navigate(`/runs/${child.id}`);
    } catch (err) {
      this.ctx.toast.show(`Retry failed: ${err.message}`, { type: 'error' });
      this.renderRetry();
    }
  }

  async loadAudits() {
    for (const controller of this.loadControllers) controller.abort();
    const controller = new AbortController();
    this.loadControllers.add(controller);
    this.table.setLoading();

    try {
      const result = await this.ctx.api.getRunAudit(this.runId, {
        action: this.action,
        page: this.page,
        pageSize: PAGE_SIZE,
        signal: controller.signal,
      });

      this.table.setData(result.items ?? []);
      this.pager.update({
        page: result.page,
        pageSize: result.pageSize,
        totalCount: result.totalCount,
        totalPages: result.totalPages,
      });
    } catch (err) {
      if (err.name === 'AbortError') return;
      this.table.setError(err.message);
      this.pager.update({ page: this.page, pageSize: PAGE_SIZE, totalCount: 0, totalPages: 0 });
    } finally {
      this.loadControllers.delete(controller);
    }
  }

  destroy() {
    for (const controller of this.loadControllers) controller.abort();
    this.timeline?.destroy();
    this.table?.destroy();
    this.pager?.destroy();
    if (this.container) clearElement(this.container);
    document.title = 'InventorySync · Ops Console';
  }
}
