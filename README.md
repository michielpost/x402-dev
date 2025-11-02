# x402-dev
List of x402 facilitators and awesome projects

Discover live facilitators and a curated list of awesome **x402 projects**.   
 
Did you develop a x402 project?   
Add it to the [`Projects.md`](./Projects.md) file. Send a PR and it will be merged and published on the website.

https://www.x402dev.com

## Live Facilitator Dashboard

View **live facilitator activity** directly in the portal — no refresh needed.  
This keeps the community up to date with active developers and facilitations.

View facilitator dashboard: https://www.x402dev.com

## Overview

The **x402 Developer Portal** is an open platform that showcases:

- **Live facilitators** — see which facilitators are currently active.
- **Awesome x402 projects** — a curated list of tools, experiments, and open-source projects built around x402.

Live updates for facilitators are available directly from the portal.

Everything is published on https://www.x402dev.com

## How to Contribute

Contributions are **welcome**! Did you develop a cool x402 project? Add it to the list!  

### Add a Project
To add a new project, edit the [`Projects.md`](./Projects.md) file and open a Pull Request (PR).  
Provide a name, link and a short 1 description or tagline.

### Add a Facilitator
To add a new facilitator, edit the [`facilitations.json`](./facilitations.json) file and open a Pull Request (PR).  
Make sure to include the required fields and follow the existing structure.

---

## Development
This project uses dotnet and EF Core with SQLite

### Migrations
Run in src/x402dev/Server:
```
dotnet ef migrations add MIGRATION_NAME
```




