import { loadImage, number } from './dom.js';
import { t } from './i18n.js';

export function quantityLabel(item) {
  return item.maxQuantity > 1 ? `${number(item.quantity)} / ${number(item.maxQuantity)}` : number(item.quantity);
}

export function iconButton(item) {
  const button = document.createElement('button');
  button.className = 'hud-icon'; button.setAttribute('aria-label', t(item.name));
  if (item.icon) {
    const image = document.createElement('img'); image.alt = ''; button.append(image); loadImage(image, item.icon);
  } else button.textContent = t(item.name)?.slice(0, 1) || '◆';
  return button;
}

// Both a tap and a deliberate hold open details. Moving away cancels the hold.
export function detailsGesture(button, show) {
  let timer = null, held = false, origin = null;
  const clear = () => { clearTimeout(timer); timer = null; };
  button.addEventListener('pointerdown', event => {
    if (!event.isPrimary) return;
    clear(); held = false; origin = { x: event.clientX, y: event.clientY };
    timer = setTimeout(() => { if (button.isConnected) { held = true; show(); } }, 500);
  });
  button.addEventListener('pointermove', event => {
    if (origin && Math.hypot(event.clientX - origin.x, event.clientY - origin.y) > 8) clear();
  });
  for (const name of ['pointerup', 'pointercancel', 'pointerleave', 'lostpointercapture']) button.addEventListener(name, clear);
  button.onclick = () => { clear(); if (!held) show(); };
}
