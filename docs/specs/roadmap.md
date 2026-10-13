# Roadmap

Phase 1 is the rental MVP in these specs. After the pilot, each MVP shortcut is replaced only when its trigger fires, following the [post-MVP design](../BasaChai-Technical-Design-Post-MVP.html).

Version 1.0 · 13 October 2026 · Source: Master Spec v1.2, section 18

## Phases

| Phase | Focus |
| --- | --- |
| 0 Discovery | Interview tenants and owners, seed the five areas and their spelling aliases, test the name, rank the backlog |
| 1 Rental MVP | Everything in these specs |
| 2 Quality | Phone verification, freshness, saved searches, notifications, duplicate detection, Dhaka expansion |
| 3 Property sales | Sale listings, seller flows, sale-price filters |
| 4 Growth | Featured listings, subscriptions, agent tools, analytics, app-like web |

## Replacing MVP shortcuts

| MVP shortcut | Replace with | When |
| --- | --- | --- |
| Dashboard reminders (D11) | Job worker, in-app inbox, web push | Sampled freshness falls below 80% |
| Read-time expiry (D12) | Scheduled expiry job | Needed for reminders, or stale stored status causes reporting problems |
| Cloudinary free plan (D17) | Paid plan, or own storage with malware scanning | Usage above 80% of the allowance two months running |
| In-memory rate limits | Redis | More than one API instance |
| Database on the VPS (D16) | Managed PostgreSQL with point-in-time restore | A day of data loss becomes unacceptable, or the database needs more than 1.5 GB RAM |
| One VPS (D13) | Managed containers or several servers | CPU above 70%, or uptime below 99% for two months |
| No re-moderation of edits (D23) | Re-review changes to price, area or photos | Moderators find misleading edits, or reports point to bait-and-switch |
| Logs and uptime monitor | OpenTelemetry and APM | Incidents take more than an hour to diagnose |
| On-demand admin metrics | Nightly rollups | Admin dashboard queries take more than 2 seconds |
