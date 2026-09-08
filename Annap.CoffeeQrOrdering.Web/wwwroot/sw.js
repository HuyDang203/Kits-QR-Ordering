/* Annap guest shell — install only; menu fetch interception removed (was stalling some mobile / SW edge cases). */
const CACHE = "annap-menu-v3";

self.addEventListener("install", () => {
    self.skipWaiting();
});

self.addEventListener("activate", (event) => {
    event.waitUntil(
        (async () => {
            const keys = await caches.keys();
            await Promise.all(keys.filter((k) => k.startsWith("annap-menu-") && k !== CACHE).map((k) => caches.delete(k)));
            await self.clients.claim();
        })()
    );
});
