# #380 follow-up: OpenAI career text-model adapter: review

Scope: `OpenAICareerTextModel` behind `ICareerTextModel` (ADR 0009), its registration, and the runner's handling of model errors. No UI change. No real key is available locally, so the live smoke test (`OpenAICareerTextModelLiveTests`, skipped unless `OPENAI_API_KEY` and `CAREER_AGENT_MODEL` are set) did not run; everything else is verified against a stub HTTP handler.

## What the tests prove (`OpenAICareerTextModelTests`, `CareerAgentRuntimeTests`)

| Area | Evidence |
|---|---|
| Request shape | `POST {BaseUrl}responses`, bearer key, configured model, `store: false`, `max_output_tokens`, strict `json_schema` with required `summary`, no tool definitions, `reasoning.effort` only when configured |
| Prompt injection | profile/goal values only in the user input JSON; instructions say the JSON is data |
| Parsing and cost | summary trimmed; usage tokens; cost from configured prices rounded up; missing usage charged conservatively; function call reported as a tool request |
| Errors | retryable: network, timeout, 408/409/429/5xx, incomplete/failed/unknown status, malformed output; fatal: 400/401/403/404/422, 429 `insufficient_quota`, refusal, output limit, unsupported task |
| Spend on failures | unusable paid responses carry their cost; the runner adds it and enforces `CareerCostLimit` |
| Timeout | call cancelled at the configured request timeout (lease − 5 s) and reported retryable; caller cancellation passes through |
| Logs | status, error code and provider request id only; no prompt or provider text; exception messages are fixed codes |
| Registration | configured → OpenAI in every environment; unconfigured → fake in Development/LocalDev/Testing, nothing in Production (503); partly configured → startup warning naming missing settings |
| Runner | non-retryable error fails the run once with the unit spent; retryable error requeues with backoff |

## /code-review (Standards + Spec)

| # | Axis | Severity | Finding | Status |
|---|---|---|---|---|
| R1 | Standards | P1 | A response cut off by `max_output_tokens` (common with reasoning models) was retried, burning every attempt. | Fixed: `output_limit` is fatal; optional `ReasoningEffort` setting. |
| R2 | Standards | P2 | Paid but unusable responses never counted toward the cost ceiling. | Fixed: cost carried on `CareerModelException`; runner adds it and checks the ceiling. |
| R3 | Spec | P2 | Price settings are required beyond "key and model", and a half-configured model stayed off silently. | Kept (the cost ceiling needs prices; ADR states it); startup warning added. |
| R4 | Spec | P3 | Provider `status` text interpolated into the error code. | Fixed: unknown statuses become `unknown`. |
| R5 | Spec | P3 | Missing usage would cost 0. | Fixed: conservative estimate. |
| R6 | Standards | P3 | Response not disposed when reading the body failed; misleading timeout comment. | Fixed. |
| R7 | Spec | P3 | Retryable model errors logged only as an exception type in the runner. | Fixed: code logged. |
| R8 | Spec | P3 | Live test also needs `CAREER_AGENT_MODEL`. | Accepted: no model name may live in code. |
| R9 | Standards | P3 | Three classes in one file. | Fixed: registration moved to `CareerTextModelRegistration.cs`. |

## Before production

- Set `Career:Agent:Model`, both prices (and `ReasoningEffort` for a reasoning model) and run the live smoke test once.
- Confirm the OpenAI organisation's data controls, decide on Zero Data Retention, and add OpenAI as a subprocessor in the privacy notice (ADR 0009).

Open P0/P1: **none**.
