# Deterministic Straighter posture chain integration

Added after the completion auditor identified a gap in the earlier SQL/Azurite smoke: it checked replay and balances but not the source-image, provider-prompt, provider-output, saved-byte, and lineage chain.

## Local real-service check

```sh
node /tmp/aipm-prepare-integration-compose.cjs
node /tmp/aipm-prepare-posture-provider.cjs
# start only the local fixture container with the generated override
node docs/testing/verify-posture-chain.cjs --seed-local
```

`--seed-local` changes only the isolated `aipmverify` SQL fixture account to restore test allowances when exhausted. It cannot run against production: the script requires `OpenAI__BaseUrl` to equal the Compose fixture URL and uses fixed local container names. The captured request object is saved privately with restricted permissions and is not committed.

The fixture emits two distinct 128×128 PNG patterns, captures multipart image/prompt/model data, and returns the response pattern only if the actual provider request contains the fixed `upright_posture` instruction. The API, SQL Server, Azurite, storage proxy, and real OpenAI-provider adapter are not mocked.

## Green evidence

`/tmp/aipm-posture-chain-result.txt` reported:

- selected proof pixel hash equals actual provider input pixel hash;
- provider input and selected bytes use different encoded bytes only if the provider adapter re-encodes, but decoded pixel identity is equal;
- selected proof differs from returned fixture output;
- provider received model `gpt-image-2`, the requested shoulder-posture instruction, and preservation instructions;
- output pixel hash equals the saved result pixel hash; saved encoded bytes equal the fixture response bytes;
- selected source row and bytes are unchanged; result is a distinct owned row, points to the selected proof through `ReplacesProcessedImageId`, retains the original source lineage, and records `refinement-v1:upright_posture`;
- matching replay returns the same result, makes zero further provider calls, creates no second replacement row, debits exactly one refinement, and leaves candidate/premium/legacy-credit balances unchanged.

The local provider is a deterministic plumbing fixture, not a visual-quality evaluator. It proves that the configured fixed posture prompt and the correct source/result data move through the live application seams; it does **not** prove that a paid provider visibly improves the user's posture or preserves identity. No paid call or production account mutation occurred.

## Red/green construction notes

The first fixture implementation used Node's `Request.formData()` and failed to parse the real .NET multipart body, producing `ProviderOutcomeUnknown`. It was replaced with a bounded flat multipart parser. The initial SQL allowance update failed because `QUOTED_IDENTIFIER` was not enabled; the script now sets it. These failures did not modify production or weaken assertions.
