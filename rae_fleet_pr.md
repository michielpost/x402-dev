# RAE x402 Fleet — Production Services

**Added:** 2026-09-14
**From:** Royal Agentic Enterprises
**Repository:** bshelby88/* (multiple)

## What's included

This PR adds the RAE fleet's x402 services to the x402 resource catalog. All 9 services advertise on **Base mainnet (chain ID `eip155:8453`)** with canonical USDC (`0x833589fcd6edb6e08f4c7c32d4f71b54bda02913`).

## Services (9 services, 14 paid routes, all x402 v2)

### sentry-forge
- `POST /api/dispute-pack` — **$0.50** USDC · `payTo: 0xfBC0eb7811D477E55261d956dF39f0046E192240` · openapi: `https://sentry-forge-x402.fly.dev/openapi.json`

### dispute-forge
- `POST /api/dispute-pack` — **$0.75** USDC · `payTo: 0xfBC0eb7811D477E55261d956dF39f0046E192240` · openapi: `https://dispute-forge-x402.fly.dev/openapi.json`

### vault-pro
- `POST /api/scaffold-project` — **$0.05** USDC · `payTo: 0xfBC0eb7811D477E55261d956dF39f0046E192240` · openapi: `https://vault-pro-x402.fly.dev/openapi.json`
- `POST /api/scaffold-agent` — **$0.05** USDC · `payTo: 0xfBC0eb7811D477E55261d956dF39f0046E192240` · openapi: `https://vault-pro-x402.fly.dev/openapi.json`

### power-pack
- `POST /api/score-email` — **$0.01** USDC · `payTo: 0xfBC0eb7811D477E55261d956dF39f0046E192240` · openapi: `https://power-pack-x402.fly.dev/openapi.json`

### nanobanana
- `POST /api/generate-image` — **$0.01** USDC · `payTo: 0xfBC0eb7811D477E55261d956dF39f0046E192240` · openapi: `https://nanobanana-x402.fly.dev/openapi.json`
- `POST /api/edit-image` — **$0.01** USDC · `payTo: 0xfBC0eb7811D477E55261d956dF39f0046E192240` · openapi: `https://nanobanana-x402.fly.dev/openapi.json`

### royal-ruby
- `POST /api/law-lookup` — **$0.05** USDC · `payTo: 0xfBC0eb7811D477E55261d956dF39f0046E192240` · openapi: `https://royal-ruby-x402.fly.dev/openapi.json`

### suprapack
- `POST /api/find-skill` — **$0.03** USDC · `payTo: 0xfBC0eb7811D477E55261d956dF39f0046E192240` · openapi: `https://suprapack-x402.fly.dev/openapi.json`
- `POST /api/get-skill` — **$0.03** USDC · `payTo: 0xfBC0eb7811D477E55261d956dF39f0046E192240` · openapi: `https://suprapack-x402.fly.dev/openapi.json`
- `POST /api/list-top` — **$0.03** USDC · `payTo: 0xfBC0eb7811D477E55261d956dF39f0046E192240` · openapi: `https://suprapack-x402.fly.dev/openapi.json`

### tradingagents
- `POST /api/analyze-arbitrage` — **$0.05** USDC · `payTo: 0xfBC0eb7811D477E55261d956dF39f0046E192240` · openapi: `https://tradingagents-x402.fly.dev/openapi.json`
- `POST /api/analyze-ticker` — **$0.05** USDC · `payTo: 0xfBC0eb7811D477E55261d956dF39f0046E192240` · openapi: `https://tradingagents-x402.fly.dev/openapi.json`

### dispatch
- `POST /dispatch` — **$0.50** USDC · `payTo: 0xfBC0eb7811D477E55261d956dF39f0046E192240` · openapi: `https://dispatch-x402.fly.dev/openapi.json`

## Verification

Each service's discovery manifest is live at `https://<service>.fly.dev/.well-known/x402` (spec-minimal x402 v2 with `accepts[].payTo`, `network`, `amount`, `asset`, and `extra: {name, version}`).

The operator address `0xfBC0eb7811D477E55261d956dF39f0046E192240` is verifiable on BaseScan:
- https://basescan.org/address/0xfBC0eb7811D477E55261d956dF39f0046E192240

## How to test

```bash
# Empty probe (should return 402)
curl -i https://sentry-forge-x402.fly.dev/api/dispute-pack \
  -H "Content-Type: application/json" \
  -d '{"customer_name":"Test","state":"VA","contract_type":"credit_card","default_date":"2024-09-15","alleged_balance":1000,"narrative":"Test","collector_letter":"x"}'
```

## x402 v2 compliance

- All services use x402Version: 2 with `accepts[]` array
- `accepts[0].scheme` is `exact`
- `accepts[0].network` is `eip155:8453` (Base mainnet)
- `accepts[0].asset` is canonical USDC on Base
- `accepts[0].maxTimeoutSeconds` is 300
- `extensions.bazaar` provides input/output examples where applicable

## Operator identity

- payTo wallet owner: bshelby88 (per Airtable prospect record x402dev "Won — distribution")
- Channel: GitHub/Web (PR-based distribution)
- Expected value: $0 (distribution, not transaction)

---

## Why RAE services fit x402dev

x402dev provides resource-testing and discovery tools; RAE services are useful live 402 integration targets. Adding RAE services to the x402dev catalog gives every x402 builder testing our services for free.

This is the channel partnership referenced in Airtable prospect record `x402dev` (Status: "Won — distribution", Funnel Stage: "Won"). The fleet audited x402dev PR #50 for free in August 2026; this is the corresponding listing from RAE.

---

**Submitted by:** RAE Fleet (Royal Agentic Enterprises)
**Contact:** jadedfocus@gmail.com
**Date:** 2026-09-14
