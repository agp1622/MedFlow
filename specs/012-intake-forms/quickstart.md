# Quickstart / Validation

Prereqs: `dotnet build MedFlow.sln`, `cd medflow-client && npm install`.

Automated: `dotnet test MedFlow.Api.Tests --filter IntakeTests`.

Manual:
1. Run API and client; sign in as doctor; open a patient with an email; click "Send intake form". Capture the link from the log/SMTP sink.
2. Open `/intake/<token>` in a private window; submit with consent and a signature name. Expect confirmation; reopening the link shows "not valid".
3. As doctor open "Intake forms" list; open the pending item; confirm the record is unchanged; Accept; confirm the patient record updated and Notes has the Intake block.
4. Repeat and Reject; confirm record unchanged.
