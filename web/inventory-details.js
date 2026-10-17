import { $, text } from './dom.js';
import { t } from './i18n.js';
import { detailsGesture, iconButton } from './item-elements.js';

export class InventoryDetails {
  constructor(dialog) {
    this.dialog = dialog; this.mode = 'items'; this.page = 0; this.data = null;
    for (const button of document.querySelectorAll('[data-inventory-page]'))
      button.onclick = () => this.select(button.dataset.inventoryPage);
    $('inventoryPrev').onclick = () => { this.page--; this.render(); };
    $('inventoryNext').onclick = () => { this.page++; this.render(); };
    window.addEventListener('languageChanged', () => this.render());
  }

  setInventory(data) { this.data = data; this.render(); }

  select(mode) {
    this.mode = mode; this.page = 0;
    for (const button of document.querySelectorAll('[data-inventory-page]'))
      button.classList.toggle('active', button.dataset.inventoryPage === mode);
    $('inventory').classList.toggle('hidden', mode !== 'items');
    $('inventoryNote').classList.toggle('hidden', mode !== 'items');
    $('inventoryDetails').classList.toggle('hidden', mode === 'items');
    this.render(); window.dispatchEvent(new Event('inventoryLayoutChanged'));
  }

  render() {
    if (this.mode === 'items') return;
    const entries = this.data?.combos || [];
    const pages = Math.max(1, Math.ceil(entries.length / 8));
    this.page = Math.max(0, Math.min(pages - 1, this.page));
    const holder = $('inventoryCards'); holder.replaceChildren();
    for (const entry of entries.slice(this.page * 8, this.page * 8 + 8)) {
      const button = iconButton(entry); button.classList.add('inventory-card');
      const name = document.createElement('span'); name.className = 'card-name'; name.textContent = entry.name;
      const value = document.createElement('span'); value.className = 'card-value';
      const target = entry.tiers?.find(tier => !tier.active)?.count ?? entry.tiers?.at(-1)?.count ?? 0;
      value.textContent = `${entry.count} / ${target}`;
      button.classList.toggle('inactive-combo', !entry.tiers?.some(tier => tier.active));
      button.append(name, value); holder.append(button);
      detailsGesture(button, () => this.dialog.showCombo(entry));
    }
    text('inventoryEmpty', entries.length ? '' : t('noCombos'));
    text('inventoryPage', `${this.page + 1} / ${pages}`);
    $('inventoryPrev').disabled = this.page === 0; $('inventoryNext').disabled = this.page === pages - 1;
    text('inventoryDetailsHint', t('comboHint'));
    this.dialog.updateCombos(this.data?.combos || []);
  }
}
