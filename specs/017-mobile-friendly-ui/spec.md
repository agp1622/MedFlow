# Feature Specification: Mobile-Friendly Interface

**Feature Branch**: `claude/project-thread-7f0eby` (branch name set by the requester; spec folder is `017-mobile-friendly-ui`)

**Created**: 2026-10-03

**Status**: Draft

**Input**: GitHub issue #38 - "Make the page mobile friendly. As a user, I would like to access through mobile devices. The page must be responsive to screen sizes and adapted to common mobile screens."

## Clarifications

### Session 2026-10-03

- Q: How should wide data tables behave on phones: convert to stacked cards, or keep the table and scroll inside its own region? → A: Keep the table and scroll only inside its own region (lowest risk, preserves all columns and actions); page itself never scrolls sideways.
- Q: What navigation pattern below the desktop breakpoint? → A: Off-canvas drawer opened by a labelled menu button in a top bar, closing on selection, on backdrop tap or on Escape.
- Q: Which breakpoint separates "mobile" from "desktop" layout? → A: Below 1024px uses the drawer; 1024px and above keeps today's sidebar.
- Q: Do dialogs become full-screen on phones? → A: No; they stay centred with a max height of the viewport and internal scrolling, so context is preserved.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Navigate the app on a phone (Priority: P1)

A doctor or patient opens MedFlow on a phone. The persistent side navigation, which takes most of a phone's width, is replaced by a compact control that opens and closes the navigation, so the content gets the full screen width. Every page remains reachable.

**Why this priority**: Without usable navigation nothing else on a phone is reachable.

**Independent Test**: Open the app at 360px width, log in, open the navigation, visit each section, and confirm content is never covered or pushed off-screen.

**Acceptance Scenarios**:

1. **Given** a viewport of 390px or narrower, **When** a signed-in user opens any page, **Then** the navigation is collapsed and a visible control opens it.
2. **Given** the navigation is open on a phone, **When** the user selects a destination, **Then** the navigation closes and the destination is shown.
3. **Given** a viewport of 1024px or wider, **When** a user opens any page, **Then** the existing desktop layout is unchanged.
4. **Given** a patient using the patient portal on a phone, **When** they move between portal sections, **Then** the portal navigation is equally usable.

---

### User Story 2 - Read and act on lists and tables on a phone (Priority: P1)

Lists of patients, appointments, billing items, prescriptions, records, and portal data remain readable and operable on narrow screens, with the whole page never scrolling sideways.

**Why this priority**: Lists are the core daily workflow.

**Independent Test**: At 320px, open each list page with data and confirm every row's key information and actions are reachable without the page scrolling horizontally.

**Acceptance Scenarios**:

1. **Given** a list with many columns, **When** viewed on a phone, **Then** the user can see key row information and reach every row action, either by wrapped/stacked rows or by scrolling the table region only.
2. **Given** page headers, filters and search boxes, **When** viewed on a phone, **Then** they stack or wrap instead of overflowing.
3. **Given** pagination controls, **When** viewed on a phone, **Then** they fit the screen width.

---

### User Story 3 - Complete forms and dialogs on a phone (Priority: P2)

Forms (login, registration, patient, appointment, billing, prescription, notes, intake, booking) and modal dialogs fit within the screen, fields are full-width and easy to tap, and dialog actions are reachable.

**Why this priority**: Data entry is the other main phone task, but needs navigation and lists first.

**Independent Test**: At 320px, open each form and dialog, fill it, and submit it with the on-screen keyboard open.

**Acceptance Scenarios**:

1. **Given** a dialog opened on a phone, **When** its content is taller than the screen, **Then** the dialog fits the viewport, its content scrolls, and its action buttons remain reachable.
2. **Given** a multi-column form, **When** viewed on a phone, **Then** fields stack in one column at full width.
3. **Given** any tappable control on a phone, **When** the user taps it, **Then** its touch target is at least 44x44 CSS pixels.
4. **Given** the user focuses a text input on a phone, **When** focus lands, **Then** the browser does not auto-zoom the page.

---

### User Story 4 - Comfortable on tablets (Priority: P3)

On tablet widths (about 768 to 1023px) the layout uses the space sensibly and is not a stretched phone view or a cramped desktop view.

**Why this priority**: Secondary to phones, which the issue emphasises.

**Independent Test**: Open the main pages at 768px and 1024px and confirm no overflow and a usable layout.

**Acceptance Scenarios**:

1. **Given** a viewport between 768 and 1023px, **When** any page is opened, **Then** no content is clipped and no horizontal page scroll appears.

### Edge Cases

- Very narrow screens (320px) and long unbroken text such as emails, names or reference numbers: text wraps or truncates, never overflows the page.
- Rotating between portrait and landscape: layout adapts without losing entered form data or navigation state.
- Dark and light theme: both remain legible at all sizes.
- Large browser text size / zoom up to 200%: content remains reachable.
- Existing user-entered form values and notes must never be altered or overwritten by layout changes.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: On viewports from 320px upward, no page (public, doctor, or patient portal) MUST scroll horizontally at page level.
- **FR-002**: Below the desktop breakpoint, the primary navigation of the doctor app and of the patient portal MUST be collapsed behind an accessible control (labelled, keyboard operable) and MUST close after a destination is chosen.
- **FR-003**: At desktop widths the current layout and behaviour MUST remain unchanged.
- **FR-004**: Data tables and lists MUST remain usable on narrow screens: key information and all row actions reachable, with any horizontal scrolling confined to the table region.
- **FR-005**: Page headers, toolbars, filters, tabs and pagination MUST wrap or stack to fit narrow screens.
- **FR-006**: Forms MUST present fields in a single full-width column on phones.
- **FR-007**: Modal dialogs MUST fit within the viewport on phones, scroll internally when content is taller than the screen, and keep primary actions reachable.
- **FR-008**: Interactive controls MUST have touch targets of at least 44x44 CSS pixels on touch-size screens, and text inputs MUST NOT trigger automatic page zoom on focus.
- **FR-009**: The change MUST NOT alter any data, permissions or API behaviour; doctor-facing data remains visible only to the requesting doctor exactly as before, and no stored or user-entered text is modified or overwritten.
- **FR-010**: Both light and dark themes MUST remain legible at all supported sizes.

### Key Entities

None. This is a presentation-only change; no data is added or changed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: At 320, 360, 390, 430, 768 and 1024px widths, 100% of pages show no page-level horizontal scrolling.
- **SC-002**: A phone user can reach every section of the app and portal in at most 2 taps from any page.
- **SC-003**: A user can complete the main tasks (sign in, find a patient, book/see an appointment, fill and submit a form) on a 360px-wide screen without zooming or sideways scrolling.
- **SC-004**: All interactive controls on phone-size screens have touch targets of at least 44x44 CSS pixels.
- **SC-005**: Desktop appearance at 1280px is visually unchanged.

## Assumptions

- "Common mobile screens" means viewports of 320 to 430px wide, plus tablets of 768 to 1023px.
- Scope is the existing web client only; no native app, offline mode, or PWA install.
- No backend, API, database or data-contract changes are required.
- Existing visual style, theme tokens and component library are kept; this extends them with responsive behaviour.
- Verification is by automated build/lint/tests plus a manual viewport pass per quickstart, as the client has no browser test harness.
