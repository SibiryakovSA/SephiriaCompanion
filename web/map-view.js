import { $, text, svgElement, loadImage } from './dom.js';
import { t } from './i18n.js';

export class FloorMapView {
  constructor(travel) {
    this.travel = travel;
    this.svg = $('map');
    this.stage = $('mapStage');
    this.data = null;
    this.view = null;
    this.gesture = null;
    this.animations = []; this.frame = -1; this.activeRoom = '';
    this.svg.addEventListener('pointerdown', event => this.down(event));
    this.svg.addEventListener('pointermove', event => this.move(event));
    this.svg.addEventListener('pointerup', event => this.up(event));
    this.svg.addEventListener('pointercancel', () => this.cancel());
    window.addEventListener('blur', () => this.cancel());
    $('zoomIn').onclick = () => this.zoom(.75);
    $('zoomOut').onclick = () => this.zoom(1.33);
    $('center').onclick = () => this.fit();
    new ResizeObserver(() => this.applyView()).observe(this.stage);
  }

  update(player) {
    this.player = player;
    text('mapHint', t(player?.canTravel ? 'mapHint' : 'mapBlocked'));
    this.placeMarker();
  }

  setMap(data) {
    // Do not interrupt an ongoing map gesture with a refreshed DOM.
    if (this.gesture) { this.deferred = data; return; }
    const changedFloor = this.data?.floorGuid !== data.floorGuid;
    this.data = data;
    this.animations = []; this.frame = -1;
    this.svg.replaceChildren();
    $('mapEmpty').classList.toggle('hidden', Boolean(data.available));
    text('mapEmpty', t(data.error || 'map.loading'));
    if (!data.available) return;
    const graphics = data.graphics || [];
    for (const graphic of graphics) {
      const attributes = { x: graphic.x - graphic.width / 2, y: -graphic.y - graphic.height / 2,
        width: graphic.width, height: graphic.height };
      if (graphic.image) {
        const frames = graphic.frames?.length > 1 ? graphic.frames : [graphic.image];
        const createFrames = paths => paths.map(path => {
          const image = svgElement('image', { ...attributes, preserveAspectRatio: 'none', opacity: colorAlpha(graphic.color) });
          image.classList.add('hidden'); this.svg.append(image); loadImage(image, path); return image;
        });
        const normal = createFrames(frames), selected = createFrames(graphic.selectedFrames || []);
        normal[0].classList.remove('hidden');
        if (normal.length > 1 || selected.length) this.animations.push({ normal, selected, roomId: graphic.roomId, visible: normal[0] });
      } else this.svg.append(svgElement('rect', { ...attributes, fill: graphic.color || '#817084' }));
    }
    for (const room of data.rooms || []) {
      const attributes = { x: room.x - room.width / 2, y: -room.y - room.height / 2,
        width: room.width, height: room.height };
      if (!graphics.length) this.svg.append(svgElement('rect', { ...attributes, class: 'room-fallback' }));
      if (room.travel) {
        const hit = svgElement('rect', { ...attributes, class: 'room-hit', 'data-room': room.id });
        const title = svgElement('title'); title.textContent = t('travelRoom'); hit.append(title);
        this.svg.append(hit);
      }
    }
    this.outline = svgElement('rect', { class: 'current-room-outline hidden' });
    this.svg.append(this.outline);
    this.dot = data.playerIcon ? svgElement('image', { width: 14, height: 14, class: 'room-player-icon' }) : svgElement('circle', { r: 4, class: 'player-dot' });
    this.svg.append(this.dot);
    if (data.playerIcon) loadImage(this.dot, data.playerIcon);
    this.markerRoom = '';
    this.placeMarker();
    if (changedFloor || !this.view) this.fit();
    else this.applyView();
  }

  animate(time) {
    const frame = Math.floor(time / 200);
    if (this.frame === frame) return;
    this.frame = frame;
    for (const animation of this.animations) {
      const nodes = animation.roomId === this.activeRoom && animation.selected.length ? animation.selected : animation.normal;
      const next = nodes[frame % nodes.length];
      if (next === animation.visible || !next.classList.contains('loaded')) continue;
      animation.visible.classList.add('hidden'); next.classList.remove('hidden'); animation.visible = next;
    }
  }

  cancel() {
    if (this.gesture && this.svg.hasPointerCapture(this.gesture.id)) this.svg.releasePointerCapture(this.gesture.id);
    this.gesture = null;
    if (this.deferred) { const data = this.deferred; this.deferred = null; this.setMap(data); }
  }

  fit() {
    const shapes = [...(this.data?.rooms || []), ...(this.data?.graphics || [])];
    if (!shapes.length) return;
    const left = Math.min(...shapes.map(s => s.x - s.width / 2)) - 14;
    const right = Math.max(...shapes.map(s => s.x + s.width / 2)) + 14;
    const top = Math.min(...shapes.map(s => -s.y - s.height / 2)) - 14;
    const bottom = Math.max(...shapes.map(s => -s.y + s.height / 2)) + 14;
    this.view = { x: left, y: top, width: Math.max(32, right - left), height: Math.max(32, bottom - top) };
    this.applyView();
  }

  applyView() {
    if (!this.view) return;
    const v = this.view;
    this.svg.setAttribute('viewBox', `${v.x} ${v.y} ${v.width} ${v.height}`);
  }

  zoom(factor) {
    if (!this.view) return;
    const v = this.view;
    const width = Math.max(30, Math.min(5000, v.width * factor));
    const height = v.height * width / v.width;
    v.x += (v.width - width) / 2; v.y += (v.height - height) / 2;
    v.width = width; v.height = height;
    this.applyView();
  }

  down(event) {
    if (!this.view || !event.isPrimary) return;
    const matrix = this.svg.getScreenCTM();
    this.gesture = { id: event.pointerId, x: event.clientX, y: event.clientY, view: { ...this.view },
      room: event.target.closest('[data-room]')?.dataset.room, moved: false,
      scaleX: matrix?.a || 1, scaleY: matrix?.d || 1 };
    this.svg.setPointerCapture(event.pointerId);
  }

  move(event) {
    const gesture = this.gesture;
    if (!gesture || gesture.id !== event.pointerId) return;
    const x = event.clientX - gesture.x, y = event.clientY - gesture.y;
    if (Math.hypot(x, y) > 8) gesture.moved = true;
    if (!gesture.moved) return;
    this.view.x = gesture.view.x - x / gesture.scaleX;
    this.view.y = gesture.view.y - y / gesture.scaleY;
    this.applyView();
  }

  up(event) {
    const gesture = this.gesture;
    if (!gesture || gesture.id !== event.pointerId) return;
    this.gesture = null;
    if (this.svg.hasPointerCapture(event.pointerId)) this.svg.releasePointerCapture(event.pointerId);
    if (!gesture.moved && gesture.room && this.player?.canTravel)
      this.travel({ floorGuid: this.data.floorGuid, roomId: gesture.room });
    if (this.deferred) { const data = this.deferred; this.deferred = null; this.setMap(data); }
  }

  placeMarker() {
    if (!this.dot || !this.data) return;
    const id = this.player?.floorGuid === this.data.floorGuid ? this.player.roomId : this.data.currentRoomId;
    if (id === this.markerRoom) return;
    this.markerRoom = id;
    this.activeRoom = id; this.frame = -1;
    const room = this.data.rooms?.find(r => r.id === id);
    this.dot.classList.toggle('hidden', !room);
    this.outline.classList.toggle('hidden', !room);
    if (!room) return;
    const padding = 3;
    for (const [name, value] of Object.entries({ x: room.x - room.width / 2 - padding,
      y: -room.y - room.height / 2 - padding, width: room.width + padding * 2, height: room.height + padding * 2 }))
      this.outline.setAttribute(name, value);
    const isImage = this.dot.tagName === 'image';
    this.dot.setAttribute(isImage ? 'x' : 'cx', room.x - (isImage ? 7 : 0));
    this.dot.setAttribute(isImage ? 'y' : 'cy', -room.y - (isImage ? 7 : 0));
  }
}

function colorAlpha(color) { return color?.length === 9 ? parseInt(color.slice(7, 9), 16) / 255 : 1; }
