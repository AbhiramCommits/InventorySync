const DEFAULT_TIMEOUT_MS = 30000;

export class ProblemError extends Error {
  constructor({ status, title, detail, errors, message }) {
    super(message ?? detail ?? title ?? `Request failed with status ${status ?? 'unknown'}`);
    this.name = 'ProblemError';
    this.status = status;
    this.title = title;
    this.detail = detail;
    this.errors = errors ?? null;
  }

  static async fromResponse(response) {
    let payload = null;
    try {
      payload = await response.json();
    } catch {
      payload = null;
    }

    const { title, detail, errors, status } = payload ?? {};
    return new ProblemError({
      status: status ?? response.status,
      title,
      detail,
      errors,
      message: detail || title || `Request failed with status ${response.status}`,
    });
  }
}

export class ApiClient {
  constructor({ baseUrl = '', timeoutMs = DEFAULT_TIMEOUT_MS, onActivity } = {}) {
    this.baseUrl = baseUrl.replace(/\/$/, '');
    this.timeoutMs = timeoutMs;
    this.onActivity = onActivity ?? null;
    this._active = 0;
  }

  _activityChanged() {
    if (this.onActivity) this.onActivity(this._active);
  }

  async _request(method, path, { body, signal, silent = false } = {}) {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort('timeout'), this.timeoutMs);

    if (signal) {
      if (signal.aborted) {
        controller.abort(signal.reason ?? 'aborted');
      } else {
        signal.addEventListener('abort', () => controller.abort(signal.reason ?? 'aborted'), { once: true });
      }
    }

    this._active += 1;
    if (!silent) this._activityChanged();

    try {
      const response = await fetch(`${this.baseUrl}${path}`, {
        method,
        headers: body !== undefined ? { 'Content-Type': 'application/json' } : undefined,
        body: body !== undefined ? JSON.stringify(body) : undefined,
        signal: controller.signal,
      });

      if (!response.ok) {
        throw await ProblemError.fromResponse(response);
      }

      if (response.status === 204) return null;

      const text = await response.text();
      return text ? JSON.parse(text) : null;
    } catch (err) {
      if (err instanceof DOMException && err.name === 'AbortError') {
        if (controller.signal.reason === 'timeout') {
          throw new ProblemError({ status: 0, title: 'Request timed out', detail: `The request to ${method} ${path} timed out.` });
        }
        throw err;
      }
      throw err;
    } finally {
      clearTimeout(timeout);
      this._active -= 1;
      if (!silent) this._activityChanged();
    }
  }

  get(path, options) {
    return this._request('GET', path, options);
  }

  post(path, body, options) {
    return this._request('POST', path, { ...options, body });
  }

  put(path, body, options) {
    return this._request('PUT', path, { ...options, body });
  }

  delete(path, options) {
    return this._request('DELETE', path, options);
  }

  static queryString(params) {
    const search = new URLSearchParams();
    for (const [key, value] of Object.entries(params)) {
      if (value === null || value === undefined || value === '') continue;
      search.set(key, String(value));
    }
    const qs = search.toString();
    return qs ? `?${qs}` : '';
  }

  /* ---- Inventory --------------------------------------------------------- */

  getInventory({ search, warehouseCode, sort, page, pageSize, signal, silent } = {}) {
    const qs = ApiClient.queryString({ search, warehouseCode, sort, page, pageSize });
    return this.get(`/api/inventory${qs}`, { signal, silent });
  }

  getInventoryItem(id, options) {
    return this.get(`/api/inventory/${encodeURIComponent(id)}`, options);
  }

  createInventoryItem(payload, options) {
    return this.post('/api/inventory', payload, options);
  }

  updateInventoryItem(id, payload, options) {
    return this.put(`/api/inventory/${encodeURIComponent(id)}`, payload, options);
  }

  deleteInventoryItem(id, options) {
    return this.delete(`/api/inventory/${encodeURIComponent(id)}`, options);
  }

  /* ---- Purchase orders --------------------------------------------------- */

  getOrders({ search, vendorCode, status, sort, page, pageSize, signal, silent } = {}) {
    const qs = ApiClient.queryString({ search, vendorCode, status, sort, page, pageSize });
    return this.get(`/api/purchaseorders${qs}`, { signal, silent });
  }

  getOrder(id, options) {
    return this.get(`/api/purchaseorders/${encodeURIComponent(id)}`, options);
  }

  createOrder(payload, options) {
    return this.post('/api/purchaseorders', payload, options);
  }

  submitOrder(id, options) {
    return this.post(`/api/purchaseorders/${encodeURIComponent(id)}/submit`, undefined, options);
  }

  receiveOrderLine(orderId, lineId, quantity, options) {
    return this.post(
      `/api/purchaseorders/${encodeURIComponent(orderId)}/lines/${encodeURIComponent(lineId)}/receive`,
      { quantity },
      options,
    );
  }

  cancelOrder(id, options) {
    return this.post(`/api/purchaseorders/${encodeURIComponent(id)}/cancel`, undefined, options);
  }

  /* ---- Sync -------------------------------------------------------------- */

  runInventorySync(triggeredBy, options) {
    const qs = ApiClient.queryString({ triggeredBy });
    return this.post(`/api/sync/inventory${qs}`, undefined, options);
  }

  runPurchaseOrderSync(triggeredBy, options) {
    const qs = ApiClient.queryString({ triggeredBy });
    return this.post(`/api/sync/purchase-orders${qs}`, undefined, options);
  }

  getRuns({ entityType, status, page, pageSize, signal, silent } = {}) {
    const qs = ApiClient.queryString({ entityType, status, page, pageSize });
    return this.get(`/api/sync/runs${qs}`, { signal, silent });
  }

  getRun(id, options) {
    return this.get(`/api/sync/runs/${encodeURIComponent(id)}`, options);
  }

  getRunAudit(id, { action, page, pageSize, signal, silent } = {}) {
    const qs = ApiClient.queryString({ action, page, pageSize });
    return this.get(`/api/sync/runs/${encodeURIComponent(id)}/audit${qs}`, { signal, silent });
  }

  retryRun(id, options) {
    return this.post(`/api/sync/runs/${encodeURIComponent(id)}/retry`, undefined, options);
  }

  /* ---- Health ------------------------------------------------------------ */

  getHealth(options) {
    return this.get('/health', options);
  }
}
