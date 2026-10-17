import { currentLanguage } from './i18n.js';

export class CompanionApi {
  constructor() { this.key = null; this.session = null; this.streamController = null; }

  async request(path, options = {}) {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 2500);
    try {
      const response = await fetch(path, { ...options, signal: controller.signal,
        headers: { 'X-Companion-Key': this.key || '', ...options.headers }, cache: 'no-store' });
      if (response.status === 403) this.key = null;
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      return await response.json();
    } finally { clearTimeout(timeout); }
  }

  async connect() {
    const session = await this.request('/session');
    this.key = session.key;
    this.session = session.session;
  }

  stopStream() { this.streamController?.abort(); this.streamController = null; }

  async stream(onEvent) {
    this.stopStream();
    const controller = new AbortController();
    this.streamController = controller;
    const handshake = setTimeout(() => controller.abort(), 3000);
    try {
      const response = await fetch('/events', { headers: { 'X-Companion-Key': this.key },
        signal: controller.signal, cache: 'no-store' });
      clearTimeout(handshake);
      if (!response.ok || !response.body) throw new Error(response.status === 404 ? 'incompatible' : 'disconnected');
      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = '';
      while (!controller.signal.aborted) {
        const chunk = await reader.read();
        if (chunk.done) break;
        buffer += decoder.decode(chunk.value, { stream: true });
        let boundary;
        while ((boundary = buffer.indexOf('\n\n')) >= 0) {
          const frame = buffer.slice(0, boundary); buffer = buffer.slice(boundary + 2);
          const lines = frame.split('\n');
          const type = lines.find(line => line.startsWith('event: '))?.slice(7);
          const data = lines.filter(line => line.startsWith('data: ')).map(line => line.slice(6)).join('\n');
          if (type && data) onEvent(type, JSON.parse(data));
        }
        if (buffer.length > 1024 * 1024) throw new Error('disconnected');
      }
      if (!controller.signal.aborted) throw new Error('disconnected');
    } finally { clearTimeout(handshake); }
  }

  command(action, values = {}) {
    const id = `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 9)}`;
    return this.request('/command', { method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ ...values, id, action, session: this.session, language: currentLanguage() }) }).then(() => id);
  }
}
