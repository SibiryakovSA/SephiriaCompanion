const messages = {
  ru: {
    connected:'Подключено', disconnected:'Нет связи', connecting:'Подключение…', refresh:'Обновить',
    hud:'HUD', map:'Карта', inventory:'Рюкзак', settings:'Настройки', overview:'Обзор', combat:'Умения и эффекты',
    language:'Язык интерфейса', hudTransfer:'HUD на нижнем экране',
    ping:'Проверить подключение', startup:'Запусти игру, загрузи сохранение и нажми «Обновить».',
    items:'Предметы', combos:'Комбо', combo:'Комбо', noCombos:'Пока нет комбо',
    comboHint:'Коснись комбо или удерживай его, чтобы посмотреть все уровни.',
    comboTier:'Порог: {count}', comboCount:'Предметов: {count}', nextTier:'Ещё не открыто',
    beforeLoad:'Ожидание загрузки сохранения', battle:'В бою', peaceful:'Вне боя', level:'Ур.', exp:'Опыт', reserve:'резерв',
    dash:'Рывки', potions:'Зелья', noPotions:'Зелий нет', potionHint:'Удерживай зелье, чтобы прочитать его действие.',
    noSkills:'Нет назначенных умений', noEffects:'Нет активных эффектов', skills:'Умения', effects:'Эффекты',
    inventoryHint:'Перетащи предмет в ячейку. Касание — эффекты и условия.', inventoryBattle:'Перестановка доступна вне боя. Касание — описание.',
    floorMap:'Карта этажа', mapHint:'Коснись открытой комнаты для перемещения · перетаскивание двигает карту', mapBlocked:'Перемещение недоступно: бой, диалог или переход',
    zoomIn:'Увеличить', zoomOut:'Уменьшить', fitMap:'Вписать карту', travelRoom:'Переместиться в комнату',
    description:'Описание', diagram:'Схема', rotate:'Повернуть', close:'Закрыть', previous:'‹ Назад', next:'Далее ›',
    item:'Предмет', artifact:'Артефакт', tablet:'Табличка', potion:'Зелье', skill:'Умение', effect:'Эффект',
    quantity:'Количество', itemLevel:'Уровень', active:'Активен', inactive:'Неактивен', unique:'Уникальный эффект', conditions:'Условия', penalty:'Штраф',
    noDescription:'Описание отсутствует', tabletLegend:'◆ Табличка · зелёный: влияние · жёлтый: условие · красный: условие не выполнено',
    IncreaseConstLevel:'Уровень', Disable:'Отключение', IgnoreCriteria:'Без условий', MultiplyConstLevel:'Множитель',
    AnyItem:'Нужен предмет', OnlyCharm:'Нужен артефакт', Placed:'Нужно разместить табличку', None:'',
    'status.load_save':'Загрузи сохранение', 'status.loading':'Переход между локациями', 'status.read_error':'Ошибка чтения игры',
    'map.loading':'Игра ещё создаёт карту этажа', 'map.visit_room':'Посети комнату: она появится на карте после открытия',
    'mini.loading':'Миникарта этой комнаты ещё не готова', 'inventory.loading':'Рюкзак ещё не загружен',
    'inventory.unavailable':'Перекладывать предметы можно вне боя и перехода между локациями',
    'inventory.stale':'Рюкзак изменился. Повтори действие', 'inventory.invalid_cell':'Выбери другую доступную ячейку',
    'inventory.rotation_unavailable':'Эту табличку сейчас нельзя повернуть',
    'command.invalid':'Некорректная команда', 'connection.stale':'Соединение устарело. Нажми «Обновить»',
    'command.busy':'Дождись завершения предыдущего действия', 'command.pending':'', 'command.ping_ok':'Сообщение показано на игровом экране',
    'command.travel_ok':'', 'command.unconfirmed':'Игра не подтвердила действие. Проверь результат в игре',
    'command.send_failed':'Не удалось передать действие. Проверь подключение', incompatible:'Обнови компаньон и дополнение для игры.',
    'skill.weapon_0':'Основная атака', 'skill.weapon_1':'Особая атака', 'skill.weapon_2':'Перезарядка',
    'Текущий этаж':'Текущий этаж', 'Миникарта этой комнаты ещё не готова':'Миникарта этой комнаты ещё не готова',
    'travel.unavailable':'Сейчас перемещение недоступно: бой, диалог или переход', 'travel.stale':'Этаж сменился. Выбери комнату на новой карте',
    'travel.locked':'Эта комната пока недоступна', 'travel.choose_room':'Выбери открытую комнату или доступную точку перемещения'
  },
  en: {
    connected:'Connected', disconnected:'Disconnected', connecting:'Connecting…', refresh:'Refresh',
    hud:'HUD', map:'Map', inventory:'Backpack', settings:'Settings', overview:'Overview', combat:'Skills & effects',
    language:'Interface language', hudTransfer:'HUD on the lower screen',
    ping:'Check connection', startup:'Launch the game, load a save, then tap Refresh.',
    items:'Items', combos:'Combos', combo:'Combo', noCombos:'No combos yet',
    comboHint:'Tap or hold a combo to view all its tiers.',
    comboTier:'{count} items', comboCount:'Items: {count}', nextTier:'Not unlocked yet',
    beforeLoad:'Waiting for a loaded save', battle:'In combat', peaceful:'Out of combat', level:'Lv.', exp:'Experience', reserve:'reserved',
    dash:'Dashes', potions:'Potions', noPotions:'No potions', potionHint:'Hold a potion to read its effects.',
    noSkills:'No skills assigned', noEffects:'No active effects', skills:'Skills', effects:'Effects',
    inventoryHint:'Drag items between cells. Tap for effects and conditions.', inventoryBattle:'Rearranging is available outside combat. Tap for details.',
    floorMap:'Floor map', mapHint:'Tap a revealed room to travel · drag to pan the map', mapBlocked:'Travel unavailable: combat, dialogue or transition',
    zoomIn:'Zoom in', zoomOut:'Zoom out', fitMap:'Fit map', travelRoom:'Travel to this room',
    description:'Description', diagram:'Diagram', rotate:'Rotate', close:'Close', previous:'‹ Back', next:'Next ›',
    item:'Item', artifact:'Artifact', tablet:'Tablet', potion:'Potion', skill:'Skill', effect:'Effect',
    quantity:'Quantity', itemLevel:'Level', active:'Active', inactive:'Inactive', unique:'Unique effect', conditions:'Conditions', penalty:'Penalty',
    noDescription:'No description', tabletLegend:'◆ Tablet · green: effect · yellow: requirement · red: unmet requirement',
    IncreaseConstLevel:'Level', Disable:'Disabled', IgnoreCriteria:'Ignore requirements', MultiplyConstLevel:'Multiplier',
    AnyItem:'Item required', OnlyCharm:'Artifact required', Placed:'Tablet must be placed here', None:'',
    'status.load_save':'Load a save', 'status.loading':'Changing locations', 'status.read_error':'Unable to read game data',
    'map.loading':'The game is preparing this floor map', 'map.visit_room':'Visit a room to reveal it on the map',
    'mini.loading':'This room’s minimap is not ready yet', 'inventory.loading':'The backpack is not loaded yet',
    'inventory.unavailable':'Items can be rearranged outside combat and location transitions',
    'inventory.stale':'The backpack changed. Try the action again', 'inventory.invalid_cell':'Choose a different available cell',
    'inventory.rotation_unavailable':'This tablet cannot be rotated right now',
    'command.invalid':'Invalid command', 'connection.stale':'Session expired. Tap Refresh',
    'command.busy':'Wait for the previous action to finish', 'command.pending':'', 'command.ping_ok':'Message shown on the game screen',
    'command.travel_ok':'', 'command.unconfirmed':'The game did not confirm the action. Check the result in the game',
    'command.send_failed':'Unable to send the action. Check the connection', incompatible:'Update the companion and the game add-on.',
    'skill.weapon_0':'Basic attack', 'skill.weapon_1':'Special attack', 'skill.weapon_2':'Reload',
    'Текущий этаж':'Current floor', 'Миникарта этой комнаты ещё не готова':'This room’s minimap is not ready yet',
    'travel.unavailable':'Travel unavailable: combat, dialogue or transition', 'travel.stale':'The floor changed. Choose a room on the new map',
    'travel.locked':'This room is not available yet', 'travel.choose_room':'Choose a revealed room or an available travel point'
  }
};

let language = 'ru';
try {
  const requested = new URLSearchParams(location.search).get('lang') || window.CompanionPreferences?.getLanguage() || localStorage.getItem('language');
  language = requested === 'en' ? 'en' : 'ru';
} catch { /* Browser previews can disable storage. */ }

export function t(key, values = {}) {
  let value = messages[language][key] ?? key ?? '';
  for (const [name, replacement] of Object.entries(values)) value = value.replaceAll(`{${name}}`, replacement);
  return value;
}
export const currentLanguage = () => language;
export function setLanguage(value) {
  language = value === 'en' ? 'en' : 'ru';
  try { localStorage.setItem('language', language); window.CompanionPreferences?.setLanguage(language); } catch {}
  translatePage();
  window.dispatchEvent(new Event('languageChanged'));
}
export function translatePage() {
  document.documentElement.lang = language;
  for (const element of document.querySelectorAll('[data-i18n]')) element.textContent = t(element.dataset.i18n);
  for (const element of document.querySelectorAll('[data-i18n-label]')) element.setAttribute('aria-label', t(element.dataset.i18nLabel));
  for (const select of document.querySelectorAll('[data-language]')) select.value = language;
}
