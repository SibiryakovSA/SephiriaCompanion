import { $, text, loadImage } from './dom.js';
import { t } from './i18n.js';
import { quantityLabel } from './item-elements.js';

export function cellLabel(cell) {
  if (!cell.effect || cell.effect === 'None') return '';
  if (cell.effect === 'IncreaseConstLevel') return `${cell.amount >= 0 ? '+' : ''}${cell.amount}`;
  if (cell.effect === 'MultiplyConstLevel') return `×${cell.amount}`;
  return cell.effect === 'Disable' ? '×' : '✓';
}

export class ItemDialog {
  constructor(rotate) {
    this.rotate = rotate;
    this.page = 0;
    this.pages = [];
    this.diagram = false;
    $('closeItem').onclick = () => $('itemDialog').close();
    $('itemPrev').onclick = () => { this.page = Math.max(0, this.page - 1); this.showPage(); };
    $('itemNext').onclick = () => { this.page = Math.min(this.pages.length - 1, this.page + 1); this.showPage(); };
    $('itemTextTab').onclick = () => this.selectMode(false);
    $('itemDiagramTab').onclick = () => this.selectMode(true);
    $('rotateItem').onclick = () => {
      if (!this.item?.canRotate || !this.context || this.busy || !this.canEdit) return;
      const item = this.item, context = this.context;
      this.rotate({ floorGuid: context.floorGuid, revision: context.revision, fromX: item.x, fromY: item.y,
        sourceId: item.id, rotation: item.rotation });
    };
    window.addEventListener('languageChanged', () => {
      if ($('itemDialog').open) { if (this.combo) this.showCombo(this.combo); else this.render(); }
    });
    new ResizeObserver(() => { if ($('itemDialog').open) this.selectMode(this.diagram); }).observe($('itemDialog'));
  }

  show(item, context = null) {
    this.combo = null;
    this.item = item; this.context = context; this.page = 0; this.diagram = false;
    if (!$('itemDialog').open) $('itemDialog').showModal();
    this.render();
  }

  showCombo(combo) {
    this.show({ name: combo.name, icon: combo.icon, kind: 'combo', description: combo.description,
      effects: combo.tiers.map(tier => `${tier.active ? '✓' : '◇'} ${t('comboTier', { count: tier.count })} · ${t(tier.active ? 'active' : 'nextTier')}\n${tier.description}`).join('\n\n') });
    this.combo = combo;
    text('itemMeta', `${t('combo')} · ${t('comboCount', { count: combo.count })}`);
  }

  updateCombos(combos) {
    if (!$('itemDialog').open || !this.combo) return;
    const next = combos.find(combo => combo.id === this.combo.id);
    if (!next) { $('itemDialog').close(); return; }
    const page = this.page; this.showCombo(next); this.page = Math.min(page, this.pages.length - 1); this.showPage();
  }

  updateInventory(data) {
    if (!$('itemDialog').open || !this.context) return;
    const item = data.items?.find(value => value.id === this.item?.id);
    if (!item) { $('itemDialog').close(); return; }
    this.context = data; this.item = item; this.render();
  }

  setBusy(value) { this.busy = value; $('rotateItem').disabled = value || !this.canEdit; }

  render() {
    const item = this.item;
    if (!item) return;
    text('itemName', t(item.name));
    const parts = [t(item.kind || 'item')];
    if (item.maxLevel >= 0) parts.push(`${t('itemLevel')} ${item.level} / ${item.maxLevel}`);
    if (item.quantity > 0) parts.push(`${t('quantity')}: ${quantityLabel(item)}`);
    if (item.kind === 'artifact' || item.kind === 'tablet') parts.push(t(item.active ? 'active' : 'inactive'));
    if (item.unique) parts.push(t('unique'));
    text('itemMeta', parts.join(' · '));
    $('itemIcon').classList.toggle('hidden', !item.icon);
    if (item.icon) loadImage($('itemIcon'), item.icon);
    $('itemIcon').style.transform = `rotate(${-90 * (item.rotation || 0)}deg)`;
    $('itemDiagramTab').classList.toggle('hidden', !item.cells?.length);
    $('rotateItem').classList.toggle('hidden', !item.canRotate || !this.context);
    $('rotateItem').disabled = this.busy || !this.context?.available || !this.canEdit;
    const sections = [];
    if (item.effects) sections.push(item.effects);
    if (item.cells?.length) {
      const effects = [...new Set(item.cells.filter(cell => cell.effect && cell.effect !== 'None')
        .map(cell => `${t(cell.effect)} ${cellLabel(cell)}`))];
      if (effects.length) sections.push(`${t('effects')}: ${effects.join(' · ')}`);
    }
    if (item.conditions) sections.push(`${t('conditions')}\n${item.conditions}`);
    if (item.penalty) sections.push(`${t('penalty')}\n${item.penalty}`);
    if (item.description) sections.push(item.description);
    this.description = sections.join('\n\n') || t('noDescription');
    this.selectMode(this.diagram);
  }

  selectMode(diagram) {
    this.diagram = diagram && Boolean(this.item?.cells?.length);
    $('itemDescription').classList.toggle('hidden', this.diagram);
    $('tabletDiagram').classList.toggle('hidden', !this.diagram);
    $('itemTextTab').classList.toggle('active', !this.diagram);
    $('itemDiagramTab').classList.toggle('active', this.diagram);
    document.querySelector('.description-pages').classList.toggle('hidden', this.diagram);
    if (this.diagram) this.drawDiagram(); else this.paginate();
  }

  paginate() {
    this.pages = [];
    const description = $('itemDescription');
    let remaining = this.description;
    while (remaining.length) {
      let low = 1, high = remaining.length;
      while (low < high) {
        const middle = Math.ceil((low + high) / 2);
        description.textContent = remaining.slice(0, middle);
        if (description.scrollHeight <= description.clientHeight + 1) low = middle; else high = middle - 1;
      }
      let count = Math.max(1, low);
      if (count < remaining.length) {
        const space = remaining.lastIndexOf(' ', count);
        if (space > count * .65) count = space + 1;
      }
      this.pages.push(remaining.slice(0, count)); remaining = remaining.slice(count);
    }
    this.page = Math.min(this.page, Math.max(0, this.pages.length - 1));
    this.showPage();
  }

  showPage() {
    text('itemDescription', this.pages[this.page] || '');
    text('itemPage', `${this.page + 1} / ${Math.max(1, this.pages.length)}`);
    $('itemPrev').disabled = this.page === 0;
    $('itemNext').disabled = this.page >= this.pages.length - 1;
  }

  drawDiagram() {
    const holder = $('tabletDiagram'); holder.replaceChildren();
    const width = this.context?.width || Math.max(6, ...this.item.cells.map(cell => cell.x + 1));
    const height = this.context?.height || Math.max(1, ...this.item.cells.map(cell => cell.y + 1));
    const grid = document.createElement('div'); grid.className = 'tablet-grid';
    grid.style.gridTemplateColumns = `repeat(${width},1fr)`;
    grid.style.gridTemplateRows = `repeat(${height},1fr)`;
    for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
      const square = document.createElement('div'); square.className = 'tablet-cell';
      const cells = this.item.cells.filter(cell => cell.x === x && cell.y === y);
      const requirement = cells.find(cell => cell.criteria && cell.criteria !== 'None');
      if (cells.some(cell => cell.effect && cell.effect !== 'None')) square.classList.add('affected');
      if (requirement) square.classList.add(requirement.met ? 'required' : 'unmet');
      const label = cells.map(cellLabel).filter(Boolean).join('/');
      if (x === this.item.x && y === this.item.y) { square.classList.add('origin'); square.textContent = '◆'; }
      else square.textContent = label;
      square.title = `${x + 1}, ${y + 1}: ${cells.map(cell => t(cell.criteria || cell.effect)).join(', ')} ${label}`;
      grid.append(square);
    }
    const legend = document.createElement('p'); legend.className = 'note'; legend.textContent = t('tabletLegend');
    holder.append(grid, legend);
    const size = Math.max(10, Math.min(32, (holder.clientHeight - legend.offsetHeight - 10 - (height - 1) * 2) / height,
      (holder.clientWidth - (width - 1) * 2) / width));
    grid.style.width = `${width * size + (width - 1) * 2}px`;
    grid.style.height = `${height * size + (height - 1) * 2}px`;
  }
}
