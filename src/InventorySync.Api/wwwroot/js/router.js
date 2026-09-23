export class Router {
  constructor({ container, routes, fallback }) {
    this.container = container;
    this.routes = routes;
    this.fallback = fallback ?? (() => ({ path: '/inventory' }));
    this.current = null;
    this.navToken = 0;
    window.addEventListener('hashchange', () => this.resolve());
  }

  start() {
    this.resolve();
  }

  navigate(path) {
    location.hash = `#${path}`;
  }

  resolve() {
    const raw = location.hash.replace(/^#\/?/, '');
    const segments = raw.split('/').filter(Boolean);

    for (const route of this.routes) {
      const params = route.match(segments);
      if (params) {
        this._activate(route, params);
        return;
      }
    }

    this._activate(this.fallback(), {});
  }

  async _activate(route, params) {
    const token = ++this.navToken;

    if (this.current) {
      this.current.destroy();
      this.current = null;
    }

    const view = route.view(params);
    this.current = view;

    await view.mount(this.container, params);

    if (token !== this.navToken) return;

    this._updateNav(route);
  }

  _updateNav(route) {
    const activeNav = route.nav ?? route.pattern?.[0] ?? null;
    for (const link of document.querySelectorAll('.main-nav a[data-nav]')) {
      if (link.dataset.nav === activeNav) {
        link.setAttribute('aria-current', 'page');
      } else {
        link.removeAttribute('aria-current');
      }
    }
  }

  static matchPattern(pattern, segments) {
    if (pattern.length !== segments.length) return null;
    const params = {};
    for (let i = 0; i < pattern.length; i += 1) {
      const part = pattern[i];
      if (part.startsWith(':')) {
        params[part.slice(1)] = decodeURIComponent(segments[i]);
      } else if (part !== segments[i]) {
        return null;
      }
    }
    return params;
  }
}
