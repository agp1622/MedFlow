# Implementation Plan: Mobile-Friendly Interface

**Branch**: `claude/project-thread-7f0eby` | **Date**: 2026-10-03 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/017-mobile-friendly-ui/spec.md` (GitHub issue #38)

## Summary

Make the React client responsive using Tailwind breakpoints already available (`sm` 640, `md` 768, `lg` 1024). Replace the fixed 220px sidebar below `lg` with an off-canvas drawer opened from a top bar; make page headers, paddings, grids, tabs, search boxes, tables and modals fit narrow screens; add small global rules for touch target size and input font size. Presentation only: no API, DTO, data, or auth changes.

## Technical Context

**Language/Version**: TypeScript, React 18, Vite
**Primary Dependencies**: Tailwind CSS, react-router-dom, lucide-react (all existing; none added)
**Storage**: N/A
**Testing**: `npm run build` (tsc), `npm run lint`; backend `dotnet test` unaffected. Client has no test runner and adding one is out of scope (constitution V); responsive behaviour verified manually per quickstart.
**Target Platform**: Mobile and tablet browsers (320-1023px) plus existing desktop
**Project Type**: web application (client-only change)
**Performance Goals**: None beyond existing
**Constraints**: Desktop (>=1024px) rendering unchanged; no horizontal page scroll from 320px
**Scale/Scope**: 6 page files, 2 layouts, shared UI, 1 stylesheet

## Constitution Check

- I. Git workflow: work on a branch off `dev`, committed locally, PR by requester. Branch name `claude/project-thread-7f0eby` was mandated by the requester and differs from the `NNN-name` convention: justified deviation, documented.
- II. Layered architecture: no backend change; no ad hoc fetch added. PASS.
- III. API contracts: no endpoint change. PASS.
- IV. Security: no auth/data change; layout only. PASS.
- V. Simplicity: reuse Tailwind utilities and existing components; one small new state (drawer open) in AppLayout; no new dependency. PASS.

Post-design re-check: PASS.

## Project Structure

### Documentation (this feature)

```text
specs/017-mobile-friendly-ui/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/ui-contract.md
└── tasks.md
```

### Source Code

```text
medflow-client/
├── index.html                          # viewport already set
└── src/
    ├── index.css                       # touch targets, 16px inputs, overflow guard
    ├── components/
    │   ├── layout/AppLayout.tsx        # top bar + drawer below lg
    │   ├── layout/PortalLayout.tsx     # verify header fits 320px
    │   ├── ui/index.tsx                # PageHeader/SearchInput/Pagination/StatCard responsive
    │   └── attachments/AttachmentsTab.tsx  # modals
    └── pages/                          # paddings, grids, tables, tabs, modals
```

**Structure Decision**: Edit existing client files in place. No backend projects touched.

## Complexity Tracking

No violations beyond the documented branch-name deviation.
