/*
 * Local/dev safety worker.
 *
 * If an old Angular service worker was previously registered on localhost,
 * this script replaces it and immediately unregisters itself so stale caches
 * do not keep serving outdated bundles.
 */
self.addEventListener('install', event => {
  self.skipWaiting();
});

self.addEventListener('activate', event => {
  event.waitUntil(
    (async () => {
      const clients = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
      await self.registration.unregister();
      for (const client of clients) {
        client.navigate(client.url);
      }
    })()
  );
});
