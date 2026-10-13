# Open questions

Ten questions remain. Each has a working default, and none blocks the build. The VPS choice is deferred until the first deploy.

Version 1.0 · 13 October 2026 · Source: Master Spec v1.2, section 19

Resolved on 13 October 2026: .NET version (D15), Next.js host (D14), Cloudinary plan (D17), re-moderation of edits (D23).

| ID | Question | Working default | Needed by |
| --- | --- | --- | --- |
| Q1 | VPS provider and region (deferred) | Develop locally with the same Docker Compose file; choose by price and latency from Dhamrai mobile networks before the first staging deploy | First deploy |
| Q2 | Netlify plan limits and function region | Start on the free plan; check bandwidth and function limits and the latency of server-rendered pages from Bangladesh | Before launch |
| Q3 | Off-site backup storage | Any low-cost S3-compatible storage | Before launch |
| Q4 | Exact phone-reveal limits | 10 per hour and 30 per day per IP and device | Before launch |
| Q5 | Spelling aliases for the five areas | Collect common Bangla and English spellings in Phase 0 | Before launch |
| Q6 | Anonymous reports | Allowed, rate-limited per IP | Build start |
| Q7 | Identity check before an admin password reset | Match name, listings and number on a support call; record it in the audit log | Before launch |
| Q8 | When and how to add phone verification | Phase 2; WhatsApp code or moderator call-back | Phase 2 |
| Q9 | Dhaka expansion timing | After the pilot metrics are met | Phase 2 |
| Q10 | Brand, domain, trademark and data-privacy legal review | Not started | Before launch |
