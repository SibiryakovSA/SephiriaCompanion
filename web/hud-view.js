import { $, text, bar, number, svgElement, loadImage, PositionSmoother } from './dom.js';
import { t } from './i18n.js';
import { detailsGesture, iconButton, quantityLabel } from './item-elements.js';

export class HudView {
  constructor(dialog) {
    this.dialog = dialog; this.mini = null; this.content = null; this.imageReady = false;
    this.position = new PositionSmoother(); this.lastPlayer = null; this.combat = null;
    this.potionPage = this.skillPage = this.effectPage = 0; this.page = 'overview';
    for (const button of document.querySelectorAll('[data-hud-page]')) button.onclick = () => {
      this.page = button.dataset.hudPage;
      $('hudOverview').classList.toggle('hidden', this.page !== 'overview');
      $('hudCombat').classList.toggle('hidden', this.page !== 'combat');
      for (const entry of document.querySelectorAll('[data-hud-page]')) entry.classList.toggle('active', entry === button);
    };
    $('potionsPrev').onclick = () => { this.potionPage--; this.renderPotions(); };
    $('potionsNext').onclick = () => { this.potionPage++; this.renderPotions(); };
    $('skillsPrev').onclick = () => { this.skillPage--; this.renderSkills(); };
    $('skillsNext').onclick = () => { this.skillPage++; this.renderSkills(); };
    $('effectsPrev').onclick = () => { this.effectPage--; this.renderEffects(); };
    $('effectsNext').onclick = () => { this.effectPage++; this.renderEffects(); };
    window.addEventListener('languageChanged', () => {
      this.renderPotions(); this.renderSkills(); this.renderEffects(); this.update(this.lastPlayer, this.combat);
    });
  }

  update(player, combat) {
    this.lastPlayer = player;
    if (!player) return;
    text('hp', `${number(player.hp)} / ${number(player.maxHp)}`);
    text('mp', `${number(player.mp)} / ${number(player.maxMp)}`);
    text('shield', player.shield > 0 ? `+${number(player.shield)}` : '');
    text('reservedMp', player.reservedMp > 0 ? `${t('reserve')} ${number(player.reservedMp)}` : '');
    for (const field of ['money', 'sapphire', 'dice', 'level']) text(field, number(player[field]));
    text('experience', `${number(player.exp)} / ${player.nextLevelExp ? number(player.nextLevelExp) : 'MAX'}`);
    bar('hpBar', player.hp / player.maxHp); bar('mpBar', player.mp / player.maxMp); bar('expBar', player.expRatio);
    this.position.set(player.x, player.y, `${player.floorGuid}:${player.roomId || ''}`);
    if (combat) this.updateCombat(combat);
  }

  setContent(content) {
    this.content = content;
    for (const [field, path] of Object.entries(content.icons || {})) {
      const icon = $(`${field}Icon`);
      if (icon && path) loadImage(icon, path);
    }
    this.renderPotions(); this.renderSkills(); this.renderEffects();
    if (this.combat) this.updateCombat(this.combat);
  }

  updateCombat(combat) {
    const changedPotion = this.combat?.selectedPotion !== combat.selectedPotion;
    this.combat = combat;
    text('dashCount', `${number(combat.dashAvailable)} / ${number(combat.dashMax)}`);
    bar('dashBar', combat.dashRecharge);
    if (changedPotion && combat.selectedPotion >= 0) {
      const index = this.content?.potions?.findIndex(entry => entry.slot === combat.selectedPotion) ?? -1;
      if (index >= 0 && Math.floor(index / 4) !== this.potionPage) { this.potionPage = Math.floor(index / 4); this.renderPotions(); }
    }
    for (const button of $('potions').querySelectorAll('[data-slot]')) button.classList.toggle('selected', Number(button.dataset.slot) === combat.selectedPotion);
    for (const state of combat.skills || []) {
      const node = $('skills').querySelector(`[data-skill="${state.id}"]`);
      if (!node) continue;
      node.classList.toggle('low-mana', state.insufficientMana);
      node.querySelector('.cooldown').style.height = `${Math.max(0, Math.min(1, state.cooldown)) * 100}%`;
      node.querySelector('.skill-ammo').textContent = state.maxAmmo > 1 ? `${state.ammo}/${state.maxAmmo}` : '';
      node.querySelector('.mana-cost').textContent = state.manaCost > 0 ? `${state.manaCost} MP` : '';
    }
    for (const state of combat.effects || []) {
      const node = [...$('effects').querySelectorAll('[data-effect]')].find(entry => entry.dataset.effect === state.id);
      if (!node) continue;
      node.querySelector('.effect-value').textContent = state.value || '';
      node.querySelector('.effect-fill').style.width = `${Math.max(0, Math.min(1, 1 - state.fill)) * 100}%`;
    }
  }

  renderPotions() {
    const entries = this.content?.potions || [];
    const pages = Math.max(1, Math.ceil(entries.length / 4));
    this.potionPage = Math.max(0, Math.min(pages - 1, this.potionPage));
    $('potions').replaceChildren();
    if (!entries.length) { const empty = document.createElement('span'); empty.className = 'note'; empty.textContent = t('noPotions'); $('potions').append(empty); }
    for (const entry of entries.slice(this.potionPage * 4, this.potionPage * 4 + 4)) {
      const button = iconButton(entry.item); button.dataset.slot = entry.slot; button.classList.add('potion');
      button.classList.toggle('selected', entry.slot === this.combat?.selectedPotion);
      const quantity = document.createElement('span'); quantity.className = 'quantity'; quantity.textContent = quantityLabel(entry.item);
      button.append(quantity); detailsGesture(button, () => this.dialog.show(entry.item)); $('potions').append(button);
    }
    $('potionsPrev').disabled = this.potionPage === 0; $('potionsNext').disabled = this.potionPage === pages - 1;
    $('potionsPrev').classList.toggle('hidden', pages === 1); $('potionsNext').classList.toggle('hidden', pages === 1);
  }

  renderSkills() {
    const entries = this.content?.skills || [], pages = Math.max(1, Math.ceil(entries.length / 8));
    this.skillPage = Math.max(0, Math.min(pages - 1, this.skillPage)); $('skills').replaceChildren();
    for (const skill of entries.slice(this.skillPage * 8, this.skillPage * 8 + 8)) {
      const button = iconButton(skill); button.dataset.skill = skill.id; button.classList.add('skill');
      const cooldown = document.createElement('i'); cooldown.className = 'cooldown';
      const name = document.createElement('span'); name.className = 'skill-name'; name.textContent = t(skill.name);
      const ammo = document.createElement('span'); ammo.className = 'skill-ammo';
      const cost = document.createElement('span'); cost.className = 'mana-cost';
      button.append(cooldown, name, ammo, cost); detailsGesture(button, () => this.dialog.show({ ...skill, kind: 'skill' })); $('skills').append(button);
    }
    if (!entries.length) { const empty = document.createElement('p'); empty.className = 'note'; empty.textContent = t('noSkills'); $('skills').append(empty); }
    text('skillsPage', `${this.skillPage + 1} / ${pages}`); $('skillsPrev').disabled = this.skillPage === 0; $('skillsNext').disabled = this.skillPage === pages - 1;
    if (this.combat) this.updateCombat(this.combat);
  }

  renderEffects() {
    const entries = this.content?.effects || [], pages = Math.max(1, Math.ceil(entries.length / 6));
    this.effectPage = Math.max(0, Math.min(pages - 1, this.effectPage)); $('effects').replaceChildren();
    for (const effect of entries.slice(this.effectPage * 6, this.effectPage * 6 + 6)) {
      const button = iconButton(effect); button.dataset.effect = effect.id; button.classList.add('effect');
      const value = document.createElement('span'); value.className = 'effect-value';
      const fill = document.createElement('i'); fill.className = 'effect-fill'; button.append(value, fill);
      detailsGesture(button, () => this.dialog.show({ ...effect, kind: 'effect' })); $('effects').append(button);
    }
    if (!entries.length) { const empty = document.createElement('p'); empty.className = 'note'; empty.textContent = t('noEffects'); $('effects').append(empty); }
    text('effectsPage', `${this.effectPage + 1} / ${pages}`); $('effectsPrev').disabled = this.effectPage === 0; $('effectsNext').disabled = this.effectPage === pages - 1;
  }

  setMinimap(mini) {
    this.mini = mini; this.imageReady = false;
    const svg = $('minimap'); svg.replaceChildren();
    $('miniEmpty').classList.toggle('hidden', Boolean(mini.available));
    text('miniEmpty', t(mini.error || 'mini.loading'));
    if (!mini.available || !mini.image || mini.width <= 0 || mini.height <= 0) return;
    svg.setAttribute('viewBox', `-3 -3 ${mini.width + 6} ${mini.height + 6}`);
    const image = svgElement('image', { x: 0, y: 0, width: mini.width, height: mini.height, preserveAspectRatio: 'none' });
    svg.append(image); loadImage(image, mini.image, () => { if (this.mini === mini) this.imageReady = true; });
    this.dot = svgElement('circle', { r: Math.max(.6, Math.min(mini.width, mini.height) / 55), class: 'player-dot' }); svg.append(this.dot);
  }

  get ready() { return Boolean(this.lastPlayer && this.imageReady && this.mini?.available && this.mini.floorGuid === this.lastPlayer.floorGuid); }
  get combatReady() { return Boolean(this.combat?.available && this.content?.revision > 0 && this.content.floorGuid === this.lastPlayer?.floorGuid); }

  animate(time) {
    if (this.page !== 'overview') return;
    const player = this.position.step(time), mini = this.mini;
    if (!player || !mini?.available || !this.dot || !player.identity.startsWith(mini.floorGuid + ':')) return;
    this.dot.setAttribute('cx', player.x - mini.x); this.dot.setAttribute('cy', mini.height - (player.y - mini.y));
  }
}
