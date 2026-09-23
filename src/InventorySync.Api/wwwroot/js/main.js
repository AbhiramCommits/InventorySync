import { ApiClient } from './api.js';
import { Router } from './router.js';
import { ToastHost } from './components/Toast.js';
import { ConfirmDialog } from './components/ConfirmDialog.js';
import { InventoryView } from './views/inventoryView.js';
import { OrdersView } from './views/ordersView.js';
import { RunsView } from './views/runsView.js';
import { RunDetailView } from './views/runDetailView.js';

const loadingIndicator = document.getElementById('global-loading');

const api = new ApiClient({
  baseUrl: '',
  onActivity: (count) => {
    loadingIndicator.hidden = count === 0;
  },
});

const toast = new ToastHost(document.getElementById('toast-region'));
const confirm = new ConfirmDialog(document.getElementById('dialog-root'));

const context = { api, toast, confirm, router: null };

const router = new Router({
  container: document.getElementById('app'),
  routes: [
    {
      pattern: ['inventory'],
      nav: 'inventory',
      match: (segments) => Router.matchPattern(['inventory'], segments),
      view: () => new InventoryView(context),
    },
    {
      pattern: ['orders'],
      nav: 'orders',
      match: (segments) => Router.matchPattern(['orders'], segments),
      view: () => new OrdersView(context),
    },
    {
      pattern: ['runs'],
      nav: 'runs',
      match: (segments) => Router.matchPattern(['runs'], segments),
      view: () => new RunsView(context),
    },
    {
      pattern: ['runs', ':id'],
      nav: 'runs',
      match: (segments) => Router.matchPattern(['runs', ':id'], segments),
      view: (params) => new RunDetailView(context, Number(params.id)),
    },
  ],
  fallback: () => ({
    pattern: ['inventory'],
    nav: 'inventory',
    view: () => new InventoryView(context),
  }),
});

context.router = router;
router.start();
