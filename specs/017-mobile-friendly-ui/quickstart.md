# Quickstart: Verify Mobile-Friendly Interface

1. `cd medflow-client && npm install && npm run build && npm run lint` - must pass.
2. Start the API and `npm run dev`; open the app in a browser with device emulation.
3. For each of 320, 360, 390, 430, 768, 1024, 1280px, as a doctor visit Dashboard, Patients, a patient detail (every tab), Appointments, Prescriptions, Billing; as a patient visit the portal; also Login/Register/Forgot password.
4. Expect: no page-level horizontal scroll; drawer navigation below 1024px (opens, closes on link/backdrop/Escape); tables scroll inside their card only; forms single column on phones; dialogs fit and scroll; focusing an input does not zoom.
5. Toggle dark mode and re-check legibility.
6. At 1280px, confirm the layout matches the pre-change look.
