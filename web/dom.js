export const $ = id => document.getElementById(id);
export const number = value => Number.isFinite(value) ? Math.round(value).toLocaleString('ru-RU') : '—';
let assetSession = '';
const readyAssets = new Set();
const waitingImages = new Map();
const imageRequests = new WeakMap();
export function setAssetSession(session) {
  if (assetSession === session) return;
  assetSession = session || ''; readyAssets.clear(); waitingImages.clear();
}
export function assetAvailable(path) {
  readyAssets.add(path);
  const callbacks = waitingImages.get(path);
  waitingImages.delete(path);
  if (callbacks) for (const callback of callbacks) callback();
}
const ns = 'http://www.w3.org/2000/svg';
export function svgElement(tag, attributes = {}) {
  const element = document.createElementNS(ns, tag);
  for (const [name, value] of Object.entries(attributes)) element.setAttribute(name, value);
  return element;
}
export function text(id, value) {
  const node = $(id);
  const next = String(value ?? '—');
  if (node.textContent !== next) node.textContent = next;
}
export function bar(id, value) {
  $(id).style.width = `${Math.max(0, Math.min(1, value || 0)) * 100}%`;
}

// Assets are encoded gradually by the game. A temporary 404 must not leave a
// permanently blank icon. Successful images use the browser's immutable cache.
export function loadImage(element, url, onLoaded = () => {}) {
  if (!url) return;
  const assetUrl = `${url}?session=${encodeURIComponent(assetSession)}`;
  const previous = imageRequests.get(element);
  if (previous?.url === assetUrl) {
    previous.onLoaded = onLoaded;
    if (previous.loaded) onLoaded();
    return;
  }
  previous?.cancel();
  element.classList.remove('loaded');
  const controller = new AbortController();
  const request = { url: assetUrl, onLoaded, loaded: false, cancel: () => {
    controller.abort();
    const callbacks = waitingImages.get(url);
    callbacks?.delete(loadReady);
    if (callbacks?.size === 0) waitingImages.delete(url);
  } };
  imageRequests.set(element, request);
  const property = element.namespaceURI === ns ? 'href' : 'src';
  let retried = false;
  const loadReady = () => {
    if (!element.isConnected || retried) return;
    retried = true;
    element.setAttribute(property, `${assetUrl}&ready=1`);
  };
  element.addEventListener('load', () => {
    request.loaded = true; request.cancel(); element.classList.add('loaded'); request.onLoaded();
  }, { signal: controller.signal });
  element.addEventListener('error', () => {
    if (readyAssets.has(url)) { loadReady(); return; }
    if (!waitingImages.has(url)) waitingImages.set(url, new Set());
    waitingImages.get(url).add(loadReady);
  }, { signal: controller.signal });
  element.setAttribute(property, assetUrl);
}

export class PositionSmoother {
  constructor() { this.current = null; this.target = null; this.time = 0; }
  set(x, y, identity) {
    const next = { x, y, identity };
    if (!this.current || this.current.identity !== identity || Math.hypot(this.current.x - x, this.current.y - y) > 100)
      this.current = { ...next };
    this.target = next;
  }
  step(time) {
    if (!this.target) return null;
    const fraction = 1 - Math.exp(-Math.min(80, time - (this.time || time)) / 65);
    this.time = time;
    this.current.x += (this.target.x - this.current.x) * fraction;
    this.current.y += (this.target.y - this.current.y) * fraction;
    return this.current;
  }
}
