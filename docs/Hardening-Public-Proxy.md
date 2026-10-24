# Hardening the public request proxy

Operators who expose the open request proxy (`POST /api/Proxy/get-x402` and
the pass-through route) accept a caller-supplied URL and fetch it
server-side. Without controls this is a textbook server-side request
forgery (SSRF) surface: the service becomes a free relay into whatever
network it sits in. This guide lists the layers that keep a public
deployment safe. The companion detection ruleset lives in
`contrib/detection/suricata/x402-public-proxy.rules`.

## 1. Outbound allowlist, fail closed

- Resolve and pin the address **at connect time**. A DNS pre-check followed
  by the runtime resolver is rebindable (TOCTOU): the first lookup can
  return a public address and the second a private one. The handler used by
  the proxy connects to the addresses it classified itself.
- Allow only absolute `http`/`https` URIs with no userinfo. Reject every
  other scheme (`file:`, `gopher:`, `dict:`, `ftp:`, `jar:`, `netdoc:`,
  `tftp:`, `ldap:`) - several URI parsers accept more schemes than
  operators expect.
- Reject loopback (`127.0.0.0/8`, `::1`), RFC1918 (`10/8`, `172.16/12`,
  `192.168/16`), link-local (`169.254/16`, `fe80::/10`), CGNAT
  (`100.64/10`), ULA (`fc00::/7`), multicast, broadcast, and the
  unspecified address (`0.0.0.0`).
- Treat **every** IP literal notation the same way: dotted quad, decimal
  integer (`2130706433`), hex octets (`0x7f.0.0.1`), octal
  (`0177.0.0.1`), IPv4-mapped IPv6 (`::ffff:127.0.0.1`). If the platform
  parser accepts a form, the policy must classify it, otherwise smugglers
  use the cheapest encoding that slips through.
- Reject `localhost`, `*.localhost`, `*.local`, `*.internal`, `*.invalid`,
  and `*.test` host names, and any host containing `%` (zone-ID trickery).
- Block the cloud metadata endpoints explicitly: `169.254.169.254` (all
  clouds), `metadata.google.internal` (GCP). A leaked instance-identity
  token from a metadata service is a full account compromise on most
  providers.

## 2. Redirects and environment proxies

- Never follow a 3xx server-side without re-checking `Location` against the
  same policy. Returning the redirect to the caller is safe; fetching it
  is not. `AllowAutoRedirect = false`.
- Disable environment/system HTTP proxies for the outbound client
  (`UseProxy = false`). A configured `HTTP_PROXY` bypasses the pinned
  socket check entirely because the proxy server performs the fetch.
- Disable cookie persistence on the outbound client; nothing about a
  proxied fetch should carry state.

## 3. Egress firewalling (the layer that survives a code regression)

The application policy is one bug away from a bypass. Put a second wall in
front of it:

- Run the service in a namespace or subnet whose egress rules deny
  RFC1918/link-local/metadata destinations outright (host firewall, cloud
  security group, or a sidecar that only permits a domain allowlist).
- On cloud VMs use IMDSv2 (or the provider equivalent) so a plain GET to
  the metadata address cannot mint tokens.
- Give the service account no instance identity, or an identity with no
  useful scopes - so even a successful metadata fetch yields nothing.

## 4. Request hygiene at the edge

- Keep the existing rate limiter (`proxy-3sec`) and add a per-source-IP
  concurrency cap; SSRF scanning is volumetric.
- Cap the response body the proxy will read (the bounded reader already
  does this - keep the ceiling).
- Set an explicit outbound timeout and a per-request deadline; a
  slow-loris target otherwise pins a connection slot.

## 5. Detection and logging

- Log every rejected URL verbatim (with the rejecting rule name) at
  warning level. Legitimate callers get a 400 either way; probes produce a
  clean alert stream.
- Ship `contrib/detection/suricata/x402-public-proxy.rules` if the
  deployment sits behind a Suricata sensor. The rules fire on
  private-literal, encoded-octet, metadata, userinfo-smuggle and
  non-HTTP-scheme patterns in the `url` field of proxied POST bodies.
- Alert on a single source touching more than N distinct private targets
  per hour - that is an enumeration, not a user.

## 6. Response-side checks

- Return a generic `400` for policy rejections; do not echo which octet or
  rule tripped. Detail leaks let a probe converge on the one encoding you
  forgot.
- Bound response size and content type: do not relay binary payloads to
  callers who only asked for a payment-required probe.
