import { el, clearElement } from '../utils.js';

export class DataTable {
  constructor({ columns, onSort, onRowClick, getRowId, emptyMessage = 'No records found.' }) {
    this.columns = columns;
    this.onSort = onSort ?? null;
    this.onRowClick = onRowClick ?? null;
    this.getRowId = getRowId ?? ((row) => row?.id ?? null);
    this.emptyMessage = emptyMessage;
    this.sort = null;
    this.rows = [];
    this.mode = 'loading';
    this.errorMessage = '';
    this.details = new Map();
  }

  mount(container) {
    this.container = container;
    clearElement(container);
    this.renderShell();
  }

  renderShell() {
    const wrapper = el('div', 'table-scroll');
    this.tableEl = el('table', 'table');

    const thead = el('thead');
    const headRow = el('tr');
    for (const column of this.columns) {
      const th = el('th');
      if (column.align === 'right') th.className = 'num';
      if (column.sortable) {
        const button = el('button', 'th-sort');
        button.type = 'button';
        const label = el('span', '', column.label);
        const indicator = el('span', 'sort-indicator');
        indicator.setAttribute('aria-hidden', 'true');
        button.append(label, indicator);

        if (this.sort && this.sort.key === column.key) {
          button.setAttribute('aria-sort', this.sort.dir === 'desc' ? 'descending' : 'ascending');
        }
        button.addEventListener('click', () => this.toggleSort(column));
        th.append(button);
      } else {
        th.textContent = column.label;
      }
      headRow.append(th);
    }
    thead.append(headRow);

    this.tbody = el('tbody');
    this.tableEl.append(thead, this.tbody);
    wrapper.append(this.tableEl);

    clearElement(this.container);
    this.container.append(wrapper);
    this.renderBody();
  }

  toggleSort(column) {
    let next = null;
    if (this.sort && this.sort.key === column.key) {
      if (this.sort.dir === 'asc') {
        next = { key: column.key, dir: 'desc' };
      }
    } else {
      next = { key: column.key, dir: 'asc' };
    }

    this.sort = next;

    if (this.onSort) {
      this.onSort(next ?? { key: null, dir: null });
    } else {
      this.applyClientSort();
      this.renderShell();
    }
  }

  applyClientSort() {
    if (!this.sort) return;
    const column = this.columns.find((c) => c.key === this.sort.key);
    if (!column) return;
    const dir = this.sort.dir === 'desc' ? -1 : 1;
    const get = column.sortValue ?? ((row) => row[column.key]);
    this.rows = [...this.rows].sort((a, b) => {
      const va = get(a);
      const vb = get(b);
      if (va === vb) return 0;
      if (va === null || va === undefined) return 1;
      if (vb === null || vb === undefined) return -1;
      const cmp = typeof va === 'number' && typeof vb === 'number'
        ? va - vb
        : String(va).localeCompare(String(vb));
      return cmp * dir;
    });
  }

  renderBody() {
    clearElement(this.tbody);

    if (this.mode === 'loading') {
      const state = el('div', 'table-state', 'Loading…');
      const spinner = el('span', 'spinner');
      spinner.setAttribute('aria-hidden', 'true');
      state.prepend(spinner);
      this.tbody.append(this.stateRow(state, this.columns.length));
      return;
    }

    if (this.mode === 'error') {
      const state = el('div', 'table-state table-state--error', `⚠ ${this.errorMessage}`);
      this.tbody.append(this.stateRow(state, this.columns.length));
      return;
    }

    if (!this.rows.length) {
      this.tbody.append(this.stateRow(el('div', 'table-state', this.emptyMessage), this.columns.length));
      return;
    }

    for (const row of this.rows) {
      const tr = el('tr');
      if (this.onRowClick) {
        tr.classList.add('is-clickable');
        tr.tabIndex = 0;
        tr.setAttribute('role', 'button');
        tr.addEventListener('click', (event) => {
          if (event.target.closest('button, a, input, select, textarea, .icon-btn')) return;
          this.onRowClick(row, event);
        });
        tr.addEventListener('keydown', (event) => {
          if (event.target.closest('button, a, input, select, textarea')) return;
          if (event.key === 'Enter' || event.key === ' ') {
            event.preventDefault();
            this.onRowClick(row, event);
          }
        });
      }

      for (const column of this.columns) {
        const td = el('td');
        if (column.align === 'right') td.classList.add('num');
        if (column.render) {
          const output = column.render(row, td);
          if (output instanceof Node) {
            td.append(output);
          } else if (typeof output === 'string') {
            td.textContent = output;
          }
        } else {
          td.textContent = row[column.key] ?? '';
        }
        tr.append(td);
      }

      this.tbody.append(tr);

      const rowId = this.getRowId(row);
      const detail = rowId !== null && rowId !== undefined ? this.details.get(rowId) : null;
      if (detail) {
        this.tbody.append(this.detailRow(detail, this.columns.length));
      }
    }
  }

  stateRow(content, colspan) {
    const tr = el('tr');
    const td = el('td');
    td.colSpan = colspan;
    td.append(content);
    tr.append(td);
    return tr;
  }

  detailRow(node, colspan) {
    const tr = el('tr', 'table-detail-row');
    const td = el('td');
    td.colSpan = colspan;
    td.append(node);
    tr.append(td);
    return tr;
  }

  setDetail(rowId, node) {
    if (node) {
      this.details.set(rowId, node);
    } else {
      this.details.delete(rowId);
    }
    this.renderBody();
  }

  setLoading() {
    this.mode = 'loading';
    this.renderBody();
  }

  setError(message) {
    this.mode = 'error';
    this.errorMessage = message;
    this.renderBody();
  }

  setData(rows) {
    this.rows = rows;
    this.mode = 'ready';
    this.renderBody();
  }

  destroy() {
    this.details.clear();
    if (this.container) clearElement(this.container);
  }
}
