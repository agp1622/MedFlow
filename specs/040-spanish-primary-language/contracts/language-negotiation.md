# Contract: Language Negotiation

## Request
Every client request to `/api/*` carries `Accept-Language: es` or `Accept-Language: en` (the selected language). Other clients may send standard `Accept-Language` lists.

## Resolution (server)
1. Walk the header values in quality order; the first whose primary subtag is `es` or `en` wins.
2. Otherwise (missing, malformed, unsupported such as `fr`): `es`.

## Response
- Status codes, JSON field names (`error`, `errors`, `message`, `code`, `statusCode`) and structure are unchanged for every endpoint.
- Only the text of user-facing messages varies with the resolved language.
- Machine-readable `code` values (for example `patient_account`) are language independent.
- Responses with no user-facing text (DTO payloads) are unaffected; stored user content is never altered.
- Uniform-response endpoints (password reset request, invitation link validity) return the same status and shape in both languages.

## Client fallback
If a response has no usable message or the call fails without response (network, 5xx), the client shows a generic message from its own resources in the current language.
