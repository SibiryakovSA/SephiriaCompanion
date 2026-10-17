import { $, text, setAssetSession, assetAvailable } from './dom.js';
import { t, setLanguage, translatePage } from './i18n.js';
import { CompanionApi } from './api.js';
import { ItemDialog } from './item-dialog.js';
import { HudView } from './hud-view.js';
import { FloorMapView } from './map-view.js';
import { InventoryView } from './inventory-view.js';
import { InventoryDetails } from './inventory-details.js';

const api = new CompanionApi();
const dialog = new ItemDialog(values => act('rotate', values));
const hud = new HudView(dialog);
const floorMap = new FloorMapView(values => act('travel', values));
const inventory = new InventoryView(values => act('swap', values), dialog);
const inventoryDetails = new InventoryDetails(dialog);
const channels = new Map();
const rendered = new Map();
let state = null, tab = 'hud', connected = false, paused = false, connecting = false;
let connectionGeneration = 0, watchdog = null, lastLease = -Infinity, leaseSending = false;
let pendingAction = null, actionSending = false, actionTimeout = null;
let errorMessage = '', actionMessage = '', actionError = false;

function armWatchdog() {
  clearTimeout(watchdog);
  watchdog = setTimeout(() => {
    if (paused) return;
    api.stopStream(); disconnected('disconnected');
  }, 3500);
}

async function startConnection() {
  if (connecting || paused || document.hidden) return;
  connecting = true;
  const generation = ++connectionGeneration;
  api.stopStream(); clearTimeout(watchdog);
  connected = false; state = null; lastLease = -Infinity;
  channels.clear(); rendered.clear();
  hud.content = null; hud.lastPlayer = null; hud.combat = null; hud.imageReady = false;
  errorMessage = ''; setBusy(false); pendingAction = null; clearTimeout(actionTimeout);
  text('connection', t('connecting'));
  try {
    await api.connect();
    if (generation !== connectionGeneration || paused) return;
    setAssetSession(api.session);
    connecting = false;
    await api.stream(receive);
  } catch (error) {
    if (generation === connectionGeneration && !paused) disconnected(error.message);
  } finally {
    if (generation === connectionGeneration) connecting = false;
  }
}

function receive(channel, data) {
  if (paused) return;
  armWatchdog();
  if (channel === 'asset') { assetAvailable(data.path); return; }
  if (channel === 'heartbeat') { sendLease(); return; }
  if (channel === 'state') {
    if (data.protocol !== 3 || data.session !== api.session) throw new Error('incompatible');
    state = data; connected = true; errorMessage = '';
    hud.update(data.player, data.combat); floorMap.update(data.player);
    inventory.update(data.player, data.status === 'playing');
    applyChannels(); acknowledge(data.lastCommand); renderStatus(); sendLease();
  } else {
    channels.set(channel, data); applyChannels();
  }
}

function applyChannels() {
  const floor = state?.player?.floorGuid;
  if (!floor) return;
  for (const [channel, data] of channels) {
    if (data.floorGuid !== floor || rendered.get(channel) === data.revision) continue;
    if (channel === 'minimap') hud.setMinimap(data);
    if (channel === 'hud-content') hud.setContent(data);
    if (channel === 'floor-map') floorMap.setMap(data);
    if (channel === 'inventory') { inventory.setInventory(data); inventoryDetails.setInventory(data); }
    rendered.set(channel, data.revision);
  }
}

function disconnected(reason) {
  api.stopStream();
  connected = false; connecting = false;
  errorMessage = reason === 'incompatible' ? 'incompatible' : 'startup';
  clearTimeout(watchdog); inventory.update(null, false); inventory.cancel();
  setBusy(false);
  if (pendingAction) { actionMessage = 'command.unconfirmed'; actionError = true; }
  pendingAction = null; clearTimeout(actionTimeout); renderStatus();
}

async function sendLease(force = false) {
  if (!state || !connected || paused || document.hidden || leaseSending) return;
  if (!force && performance.now() - lastLease < 850) return;
  lastLease = performance.now(); leaseSending = true;
  try {
    await api.command('lease', { renderedSequence: state.sequence, tab, hideHud: $('moveHud').checked,
      hudReady: hud.ready && state.status === 'playing', combatReady: hud.combatReady });
  } catch { /* The stream watchdog handles a lost connection. */ }
  finally { leaseSending = false; }
}

async function refresh() {
  resetViewport();
  if (!connected) { await startConnection(); return; }
  try { await api.command('refresh'); await sendLease(true); }
  catch { disconnected('disconnected'); }
}

function resetViewport() {
  try { window.CompanionPreferences?.resetViewport(); } catch {}
  const viewport = document.querySelector('meta[name="viewport"]');
  viewport.content = 'width=device-width,initial-scale=1,minimum-scale=1,maximum-scale=1,user-scalable=no,viewport-fit=cover';
  document.documentElement.style.removeProperty('zoom');
  document.body.style.removeProperty('zoom');
  requestAnimationFrame(() => {
    inventory.resize();
    if (tab === 'map') floorMap.fit();
    window.dispatchEvent(new Event('resize'));
  });
}

function setBusy(value) { inventory.setBusy(value); }

async function act(action, values = {}) {
  if (!connected || state?.status !== 'playing' || actionSending || pendingAction) return;
  actionSending = true;
  if (action === 'swap' || action === 'rotate') setBusy(true);
  actionMessage = ''; actionError = false; renderStatus();
  try {
    const id = await api.command(action, values);
    pendingAction = { id, action };
    actionTimeout = setTimeout(() => {
      if (pendingAction?.id !== id) return;
      pendingAction = null; setBusy(false);
      actionMessage = 'command.unconfirmed'; actionError = true; renderStatus();
    }, 7000);
    acknowledge(state?.lastCommand); // A push can arrive before the POST response.
  } catch {
    actionMessage = 'command.send_failed'; actionError = true; setBusy(false); renderStatus();
  } finally { actionSending = false; }
}

function acknowledge(result) {
  if (!pendingAction || result?.id !== pendingAction.id || result.pending) return;
  clearTimeout(actionTimeout);
  // Successful item operations are deliberately silent; keep errors and the
  // explicitly requested connection check visible.
  actionMessage = result.ok && pendingAction.action !== 'ping' ? '' : result.message;
  actionError = !result.ok;
  pendingAction = null; setBusy(false); renderStatus();
}

function selectTab(next) {
  if (tab === 'inventory') inventory.cancel();
  tab = next;
  for (const button of document.querySelectorAll('[data-tab]')) button.classList.toggle('active', button.dataset.tab === tab);
  renderPanels();
  if (tab === 'inventory') inventory.resize();
  if (connected) sendLease(true);
  renderStatus();
}

function renderPanels() {
  const waiting = !state || state.status !== 'playing';
  for (const section of document.querySelectorAll('main > section'))
    section.classList.toggle('hidden', waiting ? section.id !== 'waitingPanel' : section.id !== `${tab}Panel`);
}

function renderStatus() {
  text('connection', t(connected ? 'connected' : connecting ? 'connecting' : 'disconnected'));
  $('connection').classList.toggle('offline', !connected);
  text('floor', state?.player?.floor ? t(state.player.floor) : 'SEPHIRIA');
  text('freshness', state?.status === 'playing' && connected ? t(state.player?.inBattle ? 'battle' : 'peaceful') : t('beforeLoad'));
  text('actionResult', t(actionMessage)); $('actionResult').classList.toggle('error', actionError);
  text('waitingText', t(errorMessage || state?.message || 'startup'));
  renderPanels();
}

function pause() {
  paused = true; connectionGeneration++; connecting = false;
  clearTimeout(watchdog); api.stopStream(); inventory.cancel();
}

function resume() {
  if (!paused) return;
  paused = false;
  if (state && connected) startConnection();
}

for (const button of document.querySelectorAll('[data-tab]')) button.onclick = () => selectTab(button.dataset.tab);
for (const select of document.querySelectorAll('[data-language]')) select.onchange = () => setLanguage(select.value);
$('moveHud').onchange = () => {
  try { localStorage.setItem('moveHud', $('moveHud').checked ? '1' : '0'); } catch {}
  sendLease(true); renderStatus();
};
try { if (localStorage.getItem('moveHud') === '0') $('moveHud').checked = false; } catch {}
$('ping').onclick = () => act('ping');
$('refresh').onclick = refresh; $('startupRefresh').onclick = refresh;
document.addEventListener('visibilitychange', () => document.hidden ? pause() : resume());
window.addEventListener('companionPaused', pause); window.addEventListener('companionResumed', resume);
window.addEventListener('languageChanged', () => {
  renderStatus(); floorMap.update(state?.player); inventory.update(state?.player, connected && state?.status === 'playing');
});
document.addEventListener('contextmenu', event => event.preventDefault());

function animate(time) {
  if (!paused && !document.hidden && connected && tab === 'hud') hud.animate(time);
  if (!paused && !document.hidden && connected && tab === 'map') floorMap.animate(time);
  requestAnimationFrame(animate);
}
translatePage(); renderStatus(); requestAnimationFrame(animate); startConnection();
