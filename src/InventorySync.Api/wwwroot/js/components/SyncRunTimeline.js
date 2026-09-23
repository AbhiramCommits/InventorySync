import { el, clearElement, formatDateTime, formatDuration, formatInt } from '../utils.js';
import { StatusBadge } from './StatusBadge.js';

export class SyncRunTimeline {
  constructor({ run, onNavigate }) {
    this.run = run;
    this.onNavigate = onNavigate ?? null;
  }

  mount(container) {
    this.mountContainer = container;
    clearElement(container);

    const run = this.run;
    const finished = run.completedUtc != null;
    const failed = run.status === 2;

    const timeline = el('div', 'timeline');

    timeline.append(
      this.step('Started', formatDateTime(run.startedUtc), 'done'),
      this.separator(),
      this.step(
        finished ? 'Completed' : 'In progress',
        finished ? formatDateTime(run.completedUtc) : `Running for ${formatDuration(run.startedUtc)}`,
        finished ? (failed ? 'failed' : 'done') : 'active',
      ),
      this.separator(),
      this.step('Result', null, 'done', new StatusBadge('run', run.status).render()),
    );

    const stats = el('div', 'stats');
    const statDefs = [
      ['recordsRead', 'Read'],
      ['recordsInserted', 'Inserted'],
      ['recordsUpdated', 'Updated'],
      ['recordsFailed', 'Failed', run.recordsFailed > 0],
    ];
    for (const [key, label, danger] of statDefs) {
      const stat = el('div', 'stat');
      const value = el('div', `stat-value${danger ? ' stat-value--danger' : ''}`, formatInt(run[key]));
      stat.append(value, el('div', 'stat-label', label));
      stats.append(stat);
    }

    container.append(timeline, stats);

    const meta = el('p', 'view-subtitle');
    meta.style.marginTop = '16px';

    const parts = [];
    parts.push(`Triggered by ${run.triggeredBy || 'unknown'}`);
    parts.push(`Entity: ${run.entityType === 0 ? 'Inventory' : 'Purchase orders'}`);
    if (run.parentSyncRunId != null) {
      parts.push('Retry run');
    }
    meta.textContent = parts.join(' · ');
    container.append(meta);

    if (run.parentSyncRunId != null && this.onNavigate) {
      const parentLink = el('button', 'btn btn--small', `View parent run #${run.parentSyncRunId}`);
      parentLink.type = 'button';
      parentLink.addEventListener('click', () => this.onNavigate(`/runs/${run.parentSyncRunId}`));
      container.append(parentLink);
    }
  }

  step(label, detail, state, content) {
    const step = el('div', `timeline-step timeline-step--${state}`);
    const dot = el('span', 'timeline-dot');
    dot.setAttribute('aria-hidden', 'true');
    const text = el('div', 'timeline-text');
    text.append(el('span', 'timeline-label', label));
    if (detail) text.append(el('span', 'timeline-detail', detail));
    if (content) text.append(content);
    step.append(dot, text);
    return step;
  }

  separator() {
    const separator = el('div', 'timeline-separator');
    separator.setAttribute('aria-hidden', 'true');
    return separator;
  }

  destroy() {
    if (this.mountContainer) clearElement(this.mountContainer);
  }
}
