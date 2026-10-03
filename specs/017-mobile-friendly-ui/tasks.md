# Tasks: Mobile-Friendly Interface

**Input**: `/specs/017-mobile-friendly-ui/` (spec.md, plan.md, research.md, contracts/ui-contract.md)
**Tests**: No client test runner exists; verification is `tsc`, eslint, and quickstart.md. Backend untouched.
All paths relative to `medflow-client/`.

## Phase 1: Setup / Foundational

- [X] T001 Add global responsive rules in `src/index.css`: `overflow-x: hidden` guard on `body`, and a `@media (max-width: 1023px)` block giving `.btn` and `.input` `min-height: 44px` and `.input` `font-size: 16px`
- [X] T002 [P] Make shared UI responsive in `src/components/ui/index.tsx`: `PageHeader` (responsive padding `px-4 sm:px-6 lg:px-8`, wrap/stack title and actions), `SearchInput` (`w-full sm:w-56`), `Pagination` (wrap), `StatCard` (`min-w-0` / smaller min width)

## Phase 2: User Story 1 - Navigation on phone (P1)

**Test**: quickstart step 4 navigation items

- [X] T003 [US1] Rework `src/components/layout/AppLayout.tsx`: below `lg` show a top bar with labelled menu button (`aria-label`, `aria-expanded`) and render the sidebar as an off-canvas drawer with backdrop; close on link click, backdrop click and Escape; keep `lg:` desktop layout identical; use `h-dvh`-safe height
- [X] T004 [P] [US1] Verify and adjust `src/components/layout/PortalLayout.tsx` so the header fits 320px (truncate name, no overflow)

## Phase 3: User Story 2 - Lists and tables (P1)

**Test**: quickstart step 4 tables

- [X] T005 [P] [US2] `src/pages/PatientsPage.tsx`: responsive page padding, wrap table in `overflow-x-auto` with table `min-w`, patient detail header/info grid (`grid-cols-4` -> responsive), tab bar horizontally scrollable, inner tables/grids (lines ~172, 206-336) responsive
- [X] T006 [P] [US2] `src/pages/AppointmentsPage.tsx`: padding, table scroll container
- [X] T007 [P] [US2] `src/pages/BillingPrescriptionsPages.tsx`: padding, table scroll containers, stat card row
- [X] T008 [P] [US2] `src/pages/DashboardPage.tsx`: padding, `grid-cols-2` -> `grid-cols-1 lg:grid-cols-2`
- [X] T009 [P] [US2] `src/pages/PortalPage.tsx`: verify portal lists/cards fit 320px, fix overflow

## Phase 4: User Story 3 - Forms and dialogs (P2)

**Test**: quickstart step 4 forms/dialogs

- [X] T010 [P] [US3] Stack form grids (`grid-cols-2` -> `grid-cols-1 sm:grid-cols-2`) and make modals fit (`max-h-[90dvh]`, scroll, gutter) in `src/pages/PatientsPage.tsx`, `src/pages/AppointmentsPage.tsx`, `src/pages/BillingPrescriptionsPages.tsx`
- [X] T011 [P] [US3] `src/pages/AuthPages.tsx`: card padding `p-5 sm:p-8`, name grid stacking, check all auth forms at 320px
- [X] T012 [P] [US3] `src/components/attachments/AttachmentsTab.tsx`: upload and preview modals fit phone, preview min-height reduced

## Phase 5: User Story 4 - Tablets (P3)

- [X] T013 [US4] Review all pages at 768 and 1024px via class review; adjust breakpoints (`md:`/`lg:`) where content is cramped

## Phase 6: Polish

- [X] T014 Run `npm run build` and `npm run lint` in `medflow-client`; fix issues
- [X] T015 Run quickstart.md checklist as far as possible without a browser; record manual items

## Dependencies

T001, T002 first; T003 and T005-T012 then independent (different files, except T005/T006/T007 shared with T010 -> do T010 after them or together); T013-T015 last.
