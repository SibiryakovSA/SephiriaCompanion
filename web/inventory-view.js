import { $, text, loadImage } from './dom.js';
import { t } from './i18n.js';
import { cellLabel } from './item-dialog.js';
import { quantityLabel } from './item-elements.js';

export class InventoryView {
  constructor(swap, dialog) {
    this.swap = swap; this.dialog = dialog; this.grid = $('inventory');
    this.gesture = null; this.data = null; this.busy = false; this.enabled = false;
    this.grid.addEventListener('pointerdown', event => this.down(event));
    this.grid.addEventListener('pointermove', event => this.move(event));
    this.grid.addEventListener('pointerup', event => this.up(event));
    this.grid.addEventListener('pointercancel', () => this.cancel());
    window.addEventListener('blur', () => this.cancel());
    window.addEventListener('inventoryLayoutChanged', () => { this.cancel(); this.resize(); });
    new ResizeObserver(() => this.resize()).observe(this.grid.parentElement);
  }

  update(player, connected) {
    this.enabled = connected && Boolean(player && !player.dead && !player.inBattle);
    this.dialog.canEdit = this.enabled;
    text('inventoryNote', t(this.enabled ? 'inventoryHint' : 'inventoryBattle'));
  }

  setInventory(data) {
    if (this.gesture) { this.deferred = data; return; }
    this.data = data; this.grid.replaceChildren();
    text('capacity', data.available ? `${data.items.length} / ${data.capacity}` : '—');
    if (!data.available) { text('inventoryNote', t(data.error || 'inventory.loading')); return; }
    this.grid.style.gridTemplateColumns = `repeat(${data.width}, 1fr)`;
    this.grid.style.gridTemplateRows = `repeat(${data.height}, 1fr)`;
    const items = new Map(data.items.map(item => [`${item.x},${item.y}`, item]));
    for (let y = 0; y < data.height; y++) for (let x = 0; x < data.width; x++) {
      const slot = document.createElement('button'), item = items.get(`${x},${y}`);
      slot.className = `slot ${item ? '' : 'empty-slot'} ${y * data.width + x >= data.capacity ? 'locked' : ''}`;
      slot.dataset.x = x; slot.dataset.y = y;
      slot.setAttribute('aria-label', item ? item.name : `${x + 1}, ${y + 1}`);
      slot.draggable = false;
      if (item?.icon) {
        const image = document.createElement('img'); image.alt = ''; image.draggable = false;
        image.style.transform = `rotate(${-90 * (item.rotation || 0)}deg)`;
        slot.append(image); loadImage(image, item.icon);
      } else if (item) {
        const letter = document.createElement('span'); letter.className = 'item-letter';
        letter.textContent = item.name?.slice(0, 1) || '◆'; slot.append(letter);
      }
      if (item?.quantity > 1 || item?.maxQuantity > 1 || item?.maxLevel >= 0) {
        const label = document.createElement('span'); label.className = 'quantity';
        label.textContent = item.maxLevel >= 0 ? `${item.level}/${item.maxLevel}` : quantityLabel(item);
        slot.append(label);
      }
      if (item && (item.kind === 'artifact' || item.kind === 'tablet') && !item.active) slot.classList.add('inactive-item');
      this.grid.append(slot);
    }
    this.showTabletEffects();
    this.dialog.updateInventory(data);
    this.resize();
  }

  resize() {
    if (!this.data?.available || this.grid.classList.contains('hidden') || this.grid.parentElement.classList.contains('hidden')) return;
    const panel = this.grid.parentElement;
    const height = panel.clientHeight - panel.querySelector('.section-title').offsetHeight - $('inventoryNote').offsetHeight - $('inventoryTabs').offsetHeight - 40;
    const cell = Math.max(16, Math.min(68, (height - (this.data.height - 1) * 3) / this.data.height,
      (panel.clientWidth - 20 - (this.data.width - 1) * 3) / this.data.width));
    this.grid.style.width = `${cell * this.data.width + (this.data.width - 1) * 3}px`;
    this.grid.style.height = `${cell * this.data.height + (this.data.height - 1) * 3}px`;
    this.grid.style.flex = 'none';
  }

  itemAt(x, y) { return this.data?.items.find(item => item.x === x && item.y === y); }

  showTabletEffects() {
    const effects = new Map();
    for (const tablet of this.data.items) {
      if (tablet.kind !== 'tablet' || !tablet.active) continue;
      for (const cell of tablet.cells || []) {
        if (!cell.effect || cell.effect === 'None') continue;
        const key = `${cell.x},${cell.y}`;
        if (!effects.has(key)) effects.set(key, { positive: 0, negative: 0, other: [] });
        const value = effects.get(key);
        if (cell.effect === 'IncreaseConstLevel') {
          if (cell.amount > 0) value.positive += cell.amount; else value.negative += cell.amount;
        } else value.other.push(cellLabel(cell));
      }
    }
    for (const slot of this.grid.children) {
      const effect = effects.get(`${slot.dataset.x},${slot.dataset.y}`);
      if (!effect) continue;
      const badge = document.createElement('span'); badge.className = 'tablet-bonus';
      const add = (label, negative) => {
        const value = document.createElement('b'); value.textContent = label;
        value.className = negative ? 'negative' : 'positive'; badge.append(value);
      };
      if (effect.positive) add(`+${effect.positive}`, false);
      if (effect.negative) add(String(effect.negative), true);
      for (const label of new Set(effect.other)) add(label, label === '×');
      slot.append(badge);
    }
  }

  down(event) {
    const slot = event.target.closest('.slot:not(.locked)');
    if (!slot || !event.isPrimary || this.busy || !this.data) return;
    const x = Number(slot.dataset.x), y = Number(slot.dataset.y), item = this.itemAt(x, y);
    if (!item) return;
    event.preventDefault();
    this.gesture = { pointer: event.pointerId, clientX: event.clientX, clientY: event.clientY,
      x, y, item, slot, moved: false, revision: this.data.revision, floorGuid: this.data.floorGuid };
    this.grid.setPointerCapture(event.pointerId);
    this.grid.classList.add('previewing');
    this.footprint(item, 0, 0);
  }

  move(event) {
    const gesture = this.gesture;
    if (!gesture || gesture.pointer !== event.pointerId || !this.enabled) return;
    if (!gesture.moved && Math.hypot(event.clientX - gesture.clientX, event.clientY - gesture.clientY) > 8) {
      gesture.moved = true; gesture.slot.classList.add('drag-source');
      this.ghost = document.createElement(gesture.item.icon ? 'img' : 'div'); this.ghost.className = 'drag-ghost';
      if (gesture.item.icon) loadImage(this.ghost, gesture.item.icon); else this.ghost.textContent = gesture.item.name?.slice(0, 1) || '◆';
      document.body.append(this.ghost);
    }
    if (!gesture.moved) return;
    this.ghost.style.left = `${event.clientX}px`; this.ghost.style.top = `${event.clientY}px`;
    this.dropTarget?.classList.remove('drop-target');
    this.dropTarget = document.elementFromPoint(event.clientX, event.clientY)?.closest('.slot:not(.locked)');
    if (this.dropTarget && !this.grid.contains(this.dropTarget)) this.dropTarget = null;
    this.dropTarget?.classList.add('drop-target');
    if (this.dropTarget) this.footprint(gesture.item, Number(this.dropTarget.dataset.x) - gesture.x, Number(this.dropTarget.dataset.y) - gesture.y);
  }

  footprint(item, dx, dy) {
    for (const slot of this.grid.children) { slot.classList.remove('affected', 'required', 'unmet'); delete slot.dataset.effect; }
    for (const cell of item.cells || []) {
      const x = cell.x + (cell.worldX ? 0 : dx), y = cell.y + (cell.worldY ? 0 : dy);
      const slot = this.grid.querySelector(`[data-x="${x}"][data-y="${y}"]`);
      if (!slot) continue;
      if (cell.effect && cell.effect !== 'None') { slot.classList.add('affected'); slot.dataset.effect = cellLabel(cell); }
      if (cell.criteria && cell.criteria !== 'None') slot.classList.add(cell.met ? 'required' : 'unmet');
    }
  }

  up(event) {
    const gesture = this.gesture;
    if (!gesture || gesture.pointer !== event.pointerId) return;
    const target = this.dropTarget, targetX = Number(target?.dataset.x), targetY = Number(target?.dataset.y);
    const targetItem = this.itemAt(targetX, targetY);
    const command = gesture.moved && target && target !== gesture.slot && this.enabled ? {
      floorGuid: gesture.floorGuid, revision: gesture.revision, fromX: gesture.x, fromY: gesture.y,
      toX: targetX, toY: targetY, sourceId: gesture.item.id, targetId: targetItem?.id || 0
    } : null;
    const showItem = !gesture.moved;
    this.cancel();
    if (command) this.swap(command); else if (showItem) this.dialog.show(gesture.item, this.data);
  }

  cancel() {
    if (this.gesture && this.grid.hasPointerCapture(this.gesture.pointer)) this.grid.releasePointerCapture(this.gesture.pointer);
    this.gesture?.slot.classList.remove('drag-source'); this.dropTarget?.classList.remove('drop-target');
    this.ghost?.remove(); this.ghost = this.dropTarget = this.gesture = null;
    this.grid.classList.remove('previewing');
    this.footprint({}, 0, 0);
    if (this.deferred) { const data = this.deferred; this.deferred = null; this.setInventory(data); }
  }

  setBusy(value) { this.busy = value; this.grid.classList.toggle('busy', value); this.dialog.setBusy(value); }
}
