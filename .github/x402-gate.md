# x402 Gate

This repository gates pull_request and issue behind an [x402](https://x402.org) payment.

- Configuration: `.github/x402.yml`
- Mode: **hard** — the `x402/payment` check is a required status check, so a pull request cannot merge until the payment settles.
- Payment endpoint: `https://x402-gate.mailpost.workers.dev/pay/<intent>`
- Payment status: `https://x402-gate.mailpost.workers.dev/status/<intent>`

## Making the hard gate real

The `x402/payment` check is created in `queued` state and never turns green on its
own. It only blocks merges once it is a **required status check**.

### Rulesets (recommended)

1. **Settings → Rulesets → New ruleset → New branch ruleset**
2. Name it (for example `x402 gate`) and set **Enforcement status** to **Active**
3. Under **Target branches**, add your default branch
4. Enable **Require status checks to pass**
5. Add **`x402/payment`** to the list of checks
6. Optionally set that check's **source** to the x402 Gate app

Step 6 is worth doing: it means only this app can satisfy the check, so no other
integration can push a green `x402/payment` status. GitHub only offers the app as a
source once it has reported the check at least once, so open a pull request first —
this setup pull request is enough.

### Branch protection (legacy)

1. **Settings → Branches → Add branch protection rule**
2. Target your default branch
3. Enable **Require status checks to pass before merging**
4. Add **`x402/payment`**

Rulesets and branch protection layer together — where both apply, the stricter rule
wins.

### Via the API

`POST /repos/michielpost/x402-dev/rulesets` with an admin token:

```json
{
  "name": "x402 gate",
  "target": "branch",
  "enforcement": "active",
  "conditions": { "ref_name": { "include": ["~DEFAULT_BRANCH"], "exclude": [] } },
  "rules": [
    {
      "type": "required_status_checks",
      "parameters": {
        "strict_required_status_checks_policy": false,
        "required_status_checks": [{ "context": "x402/payment" }]
      }
    }
  ]
}
```

Add `"integration_id": <app id>` to the check entry to pin the source to this app.

## Auto-merge and the payment check

The Jev scoring workflow (`.github/workflows/projects-md-url-check.yml`) auto-merges
pull requests that only modify `Projects.md` and score at least 70/100. That merge is
additionally gated on **every check on the pull request head commit being green**,
which includes the `x402/payment` check run:

- `jev_pr_score.py` only reports `can_automerge=true` when the `x402/payment` check is
  present and successful and no other check is failing or still running, so an unpaid
  pull request is never merged even if it scores well.
- The workflow also listens for the `check_run` event, so a pull request that is
  already eligible is merged as soon as the payment check turns green — no new push is
  needed.
- The merge step re-reads the `x402/payment` check immediately before merging, so a
  check that went back to `queued` blocks the merge again.

The score comment states which of these conditions is still missing.

Because the gate does not create a check run for exempt pull requests (maintainers,
`payment-exempt`, and so on), those pull requests are not auto-merged; merge them
manually.

## Explaining the charge

Every gated pull request gets a payment comment. Add a `message` to `.github/x402.yml` so
contributors know *why* this repository charges — it is quoted in that comment, above the
payment link:

```yaml
message: |
  This repository asks for a small USDC payment on new pull requests.

  Triage time is limited, so the fee keeps the queue honest.
```

Markdown works, and a block scalar (`message: |`) gives you multiple lines.

## Exemptions

Maintainers are never charged. Add the `payment-exempt` label to any pull request or
issue to skip the gate.

## Paying

Open the payment link posted on the pull request, or use any x402 v2 client:

```ts
import { wrapFetchWithPaymentFromConfig } from "@x402/fetch";
import { ExactEvmScheme } from "@x402/evm";
import { privateKeyToAccount } from "viem/accounts";

const fetchWithPayment = wrapFetchWithPaymentFromConfig(fetch, {
  schemes: [{ network: "eip155:8453", client: new ExactEvmScheme(privateKeyToAccount(PRIVATE_KEY)) }],
});
await fetchWithPayment("https://x402-gate.mailpost.workers.dev/pay/<intent>");
```
