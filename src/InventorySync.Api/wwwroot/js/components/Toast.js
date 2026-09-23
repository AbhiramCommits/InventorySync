import { el, clearElement } from '../utils.js';

const ICONS = {
  success: '✓',
  error: '✕',
  info: 'ℹ',
  warning: '⚠',
};

export class ToastHost {
  constructor(container) {
    this.container = container;
  }

  show(message, { type = 'success', duration = 4500, title } = {}) {
    const toast = el('div', `toast toast--${type}`);
    toast.setAttribute('role', 'status');

    const icon = el('span', 'toast-icon', ICONS[type] ?? ICONS.info);
    icon.setAttribute('aria-hidden', 'true');

    const body = el('div', 'toast-body');
    if (title) body.append(el('div', 'toast-title', title));
    body.append(el('div', 'toast-message', message));

    const close = el('button', 'toast-close', '×');
    close.type = 'button';
    close.setAttribute('aria-label', 'Dismiss notification');
    close.addEventListener('click', () => this.dismiss(toast));

    toast.append(icon, body, close);
    this.container.append(toast);

    requestAnimationFrame(() => toast.classList.add('is-visible'));

    const timer = setTimeout(() => this.dismiss(toast), duration);
    toast.dataset.timer = String(timer);
  }

  dismiss(toast) {
    const timer = Number(toast.dataset.timer);
    if (timer) clearTimeout(timer);
    if (!toast.isConnected) return;
    toast.classList.remove('is-visible');
    toast.classList.add('is-leaving');
    setTimeout(() => toast.remove(), 180);
  }

  destroy() {
    clearElement(this.container);
  }
}
