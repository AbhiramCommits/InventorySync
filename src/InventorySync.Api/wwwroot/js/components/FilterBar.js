import { el, clearElement, debounce } from '../utils.js';

export class FilterBar {
  constructor({ fields, onChange, debounceMs = 300 }) {
    this.fields = fields;
    this.onChange = onChange;
    this.debounceMs = debounceMs;
    this.values = new Map();
    this.inputs = new Map();
    this.chipGroups = new Map();
    this.handlers = [];
  }

  mount(container) {
    this.container = container;
    clearElement(container);
    const bar = el('div', 'filter-bar');

    for (const field of this.fields) {
      this.values.set(field.key, field.type === 'chips' ? null : '');
      bar.append(this.buildField(field));
    }

    container.append(bar);
  }

  buildField(field) {
    const wrapper = el('div', 'filter-field');
    const label = el('label', '', field.label);
    label.htmlFor = `filter-${field.key}`;
    wrapper.append(label);

    if (field.type === 'text' || field.type === 'search') {
      const input = el('input');
      input.type = field.type === 'search' ? 'search' : 'text';
      input.id = `filter-${field.key}`;
      input.placeholder = field.placeholder ?? '';
      const emit = debounce((value) => {
        this.values.set(field.key, value);
        this.onChange?.({ key: field.key, value: value === '' ? null : value });
      }, this.debounceMs);
      const handler = () => emit(input.value);
      input.addEventListener('input', handler);
      this.handlers.push(() => input.removeEventListener('input', handler));
      this.inputs.set(field.key, input);
      wrapper.append(input);
    }

    if (field.type === 'select') {
      const select = el('select');
      select.id = `filter-${field.key}`;
      for (const option of field.options ?? []) {
        const optionEl = el('option', '', option.label);
        optionEl.value = option.value;
        select.append(optionEl);
      }
      const handler = () => {
        this.values.set(field.key, select.value);
        this.onChange?.({ key: field.key, value: select.value === '' ? null : select.value });
      };
      select.addEventListener('change', handler);
      this.handlers.push(() => select.removeEventListener('change', handler));
      this.inputs.set(field.key, select);
      wrapper.append(select);
    }

    if (field.type === 'chips') {
      const chips = el('div', 'chips');
      chips.setAttribute('role', 'group');
      chips.setAttribute('aria-label', field.label);
      for (const option of field.options ?? []) {
        const chip = el('button', 'chip', option.label);
        chip.type = 'button';
        chip.dataset.value = option.value;
        chip.setAttribute('aria-pressed', 'false');
        const handler = () => this.toggleChip(field.key, chips, option.value);
        chip.addEventListener('click', handler);
        this.handlers.push(() => chip.removeEventListener('click', handler));
        chips.append(chip);
      }
      wrapper.append(chips);
      this.chipGroups.set(field.key, chips);
    }

    return wrapper;
  }

  toggleChip(key, chips, value) {
    const current = this.values.get(key);
    const next = current === value ? null : value;
    this.values.set(key, next);
    for (const chip of chips.children) {
      chip.setAttribute('aria-pressed', chip.dataset.value === next ? 'true' : 'false');
    }
    this.onChange?.({ key, value: next });
  }

  getValue(key) {
    return this.values.get(key);
  }

  setValue(key, value) {
    this.values.set(key, value);
    const input = this.inputs.get(key);
    if (input && input.tagName === 'INPUT') {
      input.value = value ?? '';
    }
    if (input && input.tagName === 'SELECT') {
      input.value = value ?? '';
    }
    const chips = this.chipGroups.get(key);
    if (chips) {
      for (const chip of chips.children) {
        chip.setAttribute('aria-pressed', chip.dataset.value === String(value) ? 'true' : 'false');
      }
    }
  }

  destroy() {
    for (const remove of this.handlers) remove();
    this.handlers = [];
    if (this.container) clearElement(this.container);
  }
}
