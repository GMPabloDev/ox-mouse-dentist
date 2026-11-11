/**
 * Entorno de pruebas. Node 22 define `localStorage` como `undefined` cuando no
 * arranca con `--localstorage-file`, y ese global tapa la implementación de
 * jsdom. Instalamos un Storage en memoria con la misma interfaz para poder
 * probar la sesión que se conserva entre cargas.
 */
if (typeof globalThis.localStorage === 'undefined') {
  const items = new Map<string, string>();
  const storage: Storage = {
    get length() {
      return items.size;
    },
    clear: () => items.clear(),
    getItem: (key) => items.get(key) ?? null,
    key: (index) => [...items.keys()][index] ?? null,
    removeItem: (key) => {
      items.delete(key);
    },
    setItem: (key, value) => {
      items.set(key, String(value));
    },
  };

  const targets = new Set<object>([globalThis]);
  if (typeof document !== 'undefined' && document.defaultView) {
    targets.add(document.defaultView);
  }

  for (const target of targets) {
    Object.defineProperty(target, 'localStorage', {
      configurable: true,
      value: storage,
      writable: true,
    });
  }
}

/**
 * jsdom no implementa `scrollIntoView` y el select de Spartan lo llama al
 * resaltar la opción activa.
 */
if (typeof Element !== 'undefined' && !Element.prototype.scrollIntoView) {
  Element.prototype.scrollIntoView = () => {};
}
