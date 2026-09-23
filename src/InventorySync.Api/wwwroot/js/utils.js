const HAS_ESCAPE = /[&<>"']/;

export function escapeHtml(value) {
  const str = String(value ?? '');
  if (!HAS_ESCAPE.test(str)) return str;
  return str
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');
}

export function debounce(fn, wait = 300) {
  let timer = null;
  const wrapped = (...args) => {
    clearTimeout(timer);
    timer = setTimeout(() => fn(...args), wait);
  };
  wrapped.cancel = () => clearTimeout(timer);
  return wrapped;
}

export function sleep(ms, signal) {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(resolve, ms);
    if (signal) {
      signal.addEventListener('abort', () => {
        clearTimeout(timer);
        reject(new DOMException('Aborted', 'AbortError'));
      }, { once: true });
    }
  });
}

const currencyFormatter = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
});

const integerFormatter = new Intl.NumberFormat('en-US');

const dateFormatter = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' });
const dateTimeFormatter = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'medium' });

export function formatCurrency(value) {
  const num = Number(value);
  return Number.isFinite(num) ? currencyFormatter.format(num) : '—';
}

export function formatInt(value) {
  const num = Number(value);
  return Number.isFinite(num) ? integerFormatter.format(num) : '—';
}

export function formatDate(value) {
  return value ? dateFormatter.format(new Date(value)) : '—';
}

export function formatDateTime(value) {
  return value ? dateTimeFormatter.format(new Date(value)) : '—';
}

export function formatDuration(start, end) {
  if (!start) return '—';
  const ms = Math.max(0, new Date(end ?? Date.now()).getTime() - new Date(start).getTime());
  if (ms < 1000) return `${ms} ms`;
  const totalSeconds = Math.round(ms / 1000);
  if (totalSeconds < 60) return `${totalSeconds} s`;
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes} m ${seconds} s`;
}

export function el(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined && text !== null) node.textContent = text;
  return node;
}

export function clearElement(node) {
  while (node.firstChild) {
    node.removeChild(node.firstChild);
  }
}
