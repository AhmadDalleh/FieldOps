import { DestroyRef, Signal, inject, signal } from '@angular/core';

/** Whether the browser says it is online, updated on the `online` and `offline` events. Call in an injection context. */
export function onlineStatus(): Signal<boolean> {
  const online = signal(typeof navigator === 'undefined' ? true : navigator.onLine);
  const update = () => online.set(navigator.onLine);
  window.addEventListener('online', update);
  window.addEventListener('offline', update);
  inject(DestroyRef).onDestroy(() => {
    window.removeEventListener('online', update);
    window.removeEventListener('offline', update);
  });
  return online.asReadonly();
}
