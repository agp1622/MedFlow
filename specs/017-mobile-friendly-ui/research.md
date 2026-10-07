# Research: Mobile-Friendly Interface

- **Drawer vs bottom tab bar**: Decision: off-canvas drawer with top bar. Rationale: 5 destinations plus profile/theme/sign-out fit a drawer, reuses the existing sidebar markup. Alternative: bottom tab bar (no room for profile actions).
- **Breakpoint**: Decision: `lg` (1024px). Rationale: below this the 220px sidebar takes over 20% of tablet width. Alternative: `md` (rejected: cramped portrait tablets).
- **Tables**: Decision: keep tables, wrap in `overflow-x-auto` container with a `min-w` on the table so only the region scrolls. Alternative: card-per-row (rejected: duplicates markup per table, higher regression risk).
- **Modals**: Decision: keep centred modals, ensure `max-h` uses dynamic viewport and internal scroll, `mx-4` gutter retained; stack form grids to one column under `sm`. 
- **Viewport height**: Decision: `h-screen` -> `h-dvh` (Tailwind 3.4+ if available, else `h-[100dvh]`) so mobile browser bars do not clip the layout.
- **Touch targets / zoom**: Decision: CSS media query `(max-width: 1023px)` giving `.btn`, `.input` a min-height of 44px and inputs 16px font to avoid iOS focus zoom.
- **Testing**: no client test runner exists; no dependency added. Verified by tsc, eslint, and manual quickstart.
