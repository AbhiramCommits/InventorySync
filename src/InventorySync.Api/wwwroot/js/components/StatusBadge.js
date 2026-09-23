import { el } from '../utils.js';

const META = {
  run: {
    0: ['running', 'Running'],
    1: ['success', 'Succeeded'],
    2: ['danger', 'Failed'],
    3: ['warning', 'Partial success'],
  },
  po: {
    0: ['neutral', 'Draft'],
    1: ['info', 'Submitted'],
    2: ['warning', 'Partially received'],
    3: ['success', 'Received'],
    4: ['danger', 'Cancelled'],
  },
  audit: {
    0: ['success', 'Insert'],
    1: ['info', 'Update'],
    2: ['neutral', 'Skip'],
    3: ['warning', 'Conflict'],
    4: ['danger', 'Error'],
  },
  entity: {
    0: ['neutral', 'Inventory'],
    1: ['neutral', 'PO'],
  },
};

export const RUN_STATUS_LABELS = {
  0: 'Running',
  1: 'Succeeded',
  2: 'Failed',
  3: 'Partial success',
};

export class StatusBadge {
  constructor(kind, value) {
    this.kind = kind;
    this.value = value;
  }

  render() {
    const table = META[this.kind] ?? {};
    const [modifier, label] = table[this.value] ?? ['neutral', String(this.value ?? '—')];
    const badge = el('span', `badge badge--${modifier}`, label);

    if (this.kind === 'run' && this.value === 0) {
      const spinner = el('span', 'spinner');
      spinner.setAttribute('aria-hidden', 'true');
      badge.prepend(spinner);
    }

    badge.title = label;
    return badge;
  }
}

export function statusBadgeNode(kind, value) {
  return new StatusBadge(kind, value).render();
}
