# UI Contract: Responsive Behaviour

| Viewport | Doctor app navigation | Page padding | Forms |
|----------|----------------------|--------------|-------|
| < 640px | Top bar + drawer | 16px | single column |
| 640-1023px | Top bar + drawer | 24px | two columns where today |
| >= 1024px | Fixed 220px sidebar (unchanged) | 32px (unchanged) | unchanged |

- Drawer: button `aria-label="Open navigation"`, `aria-expanded`; backdrop and Escape close it; selecting a link closes it.
- Tables: horizontal scroll confined to the table container.
- Dialogs: max height of viewport, content scrolls, 16px side gutter.
- No API contract changes.
