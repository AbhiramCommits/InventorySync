import { el, clearElement, formatInt } from '../utils.js';

export class Pager {
  constructor({ onPage }) {
    this.onPage = onPage;
    this.page = 1;
    this.pageSize = 20;
    this.totalCount = 0;
    this.totalPages = 0;
  }

  mount(container) {
    this.container = container;
    this.render();
  }

  update({ page, pageSize, totalCount, totalPages }) {
    this.page = page;
    this.pageSize = pageSize;
    this.totalCount = totalCount;
    this.totalPages = totalPages;
    this.render();
  }

  render() {
    if (!this.container) return;
    clearElement(this.container);

    if (this.totalCount === 0) {
      this.container.append(el('span', 'pager-empty', 'No records'));
      return;
    }

    const nav = el('nav', 'pager');
    nav.setAttribute('aria-label', 'Pagination');

    const count = el('span', 'pager-count', `${formatInt(this.totalCount)} record${this.totalCount === 1 ? '' : 's'}`);
    const label = el('span', 'pager-label', `Page ${formatInt(this.page)} of ${formatInt(Math.max(1, this.totalPages))}`);

    const first = this.pageButton('«', 'First page', () => this.goTo(1), this.page <= 1);
    const prev = this.pageButton('‹', 'Previous page', () => this.goTo(this.page - 1), this.page <= 1);
    const next = this.pageButton('›', 'Next page', () => this.goTo(this.page + 1), this.page >= this.totalPages);
    const last = this.pageButton('»', 'Last page', () => this.goTo(this.totalPages), this.page >= this.totalPages);

    nav.append(count, label, first, prev, next, last);
    this.container.append(nav);
  }

  pageButton(text, ariaLabel, handler, disabled) {
    const button = el('button', 'btn btn--small', text);
    button.type = 'button';
    button.setAttribute('aria-label', ariaLabel);
    button.disabled = disabled;
    button.addEventListener('click', handler);
    return button;
  }

  goTo(page) {
    if (page < 1 || page > this.totalPages || page === this.page) return;
    this.onPage(page);
  }

  destroy() {
    if (this.container) clearElement(this.container);
  }
}
