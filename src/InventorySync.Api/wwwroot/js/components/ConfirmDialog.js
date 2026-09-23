import { el } from '../utils.js';

let dialogId = 0;

export class ConfirmDialog {
  constructor(root) {
    this.root = root;
    this.overlay = null;
  }

  confirm({ title = 'Are you sure?', message = '', confirmLabel = 'Confirm', cancelLabel = 'Cancel', danger = false } = {}) {
    this.close();

    return new Promise((resolve) => {
      dialogId += 1;
      const titleId = `confirm-title-${dialogId}`;

      const overlay = el('div', 'overlay');
      const dialog = el('div', 'dialog');
      dialog.setAttribute('role', 'dialog');
      dialog.setAttribute('aria-modal', 'true');
      dialog.setAttribute('aria-labelledby', titleId);

      const titleEl = el('h2', 'dialog-title', title);
      titleEl.id = titleId;

      const messageEl = el('p', 'dialog-message', message);

      const actions = el('div', 'dialog-actions');
      const cancelBtn = el('button', 'btn', cancelLabel);
      cancelBtn.type = 'button';
      const confirmBtn = el('button', `btn ${danger ? 'btn--danger' : 'btn--primary'}`, confirmLabel);
      confirmBtn.type = 'button';

      let finished = false;
      const finish = (result) => {
        if (finished) return;
        finished = true;
        document.removeEventListener('keydown', onKeydown);
        overlay.remove();
        this.overlay = null;
        resolve(result);
      };

      const onKeydown = (event) => {
        if (event.key === 'Escape') {
          event.stopPropagation();
          finish(false);
        }
      };

      cancelBtn.addEventListener('click', () => finish(false));
      confirmBtn.addEventListener('click', () => finish(true));
      overlay.addEventListener('click', (event) => {
        if (event.target === overlay) finish(false);
      });

      actions.append(cancelBtn, confirmBtn);
      dialog.append(titleEl, messageEl, actions);
      overlay.append(dialog);

      document.addEventListener('keydown', onKeydown);
      this.root.append(overlay);
      this.overlay = overlay;

      confirmBtn.focus();
    });
  }

  close() {
    if (this.overlay) {
      this.overlay.remove();
      this.overlay = null;
    }
  }

  destroy() {
    this.close();
  }
}
