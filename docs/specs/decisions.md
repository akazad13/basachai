# Decisions

Twenty-three decisions are settled for the MVP. They apply in every spec; change them in the [Master Spec](../BasaChai-Master-Spec.html#decisions) first.

Version 1.0 · 13 October 2026

| ID | Area | Decision | Date |
| --- | --- | --- | --- |
| D1 | Language | Bangla first; English available through a toggle and structured for later expansion | 12 Oct 2026 |
| D2 | Who posts | Owners only at launch; clearly labelled agents later | 12 Oct 2026 |
| D3 | Contact | Phone reveal and call; no WhatsApp or in-app chat | 12 Oct 2026 |
| D4 | Reveal access | No login needed to reveal a number; reveals are rate-limited | 12 Oct 2026 |
| D5 | Money | Free listings | 12 Oct 2026 |
| D6 | Coverage | Dhamrai first (five pilot areas), then Dhaka city | 12 Oct 2026 |
| D7 | Freshness | Owners reconfirm every 30 days or the listing expires | 12 Oct 2026 |
| D8 | Photos | At least 3 photos to submit; at most 20 | 12 Oct 2026 |
| D9 | SMS | No SMS of any kind; no phone verification at launch; numbers labelled "not verified" | 12 Oct 2026 |
| D10 | Password reset | Admin-assisted, single-use link valid 24 hours | 12 Oct 2026 |
| D11 | Notifications | Dashboard reminders only; no push, email or SMS | 13 Oct 2026 |
| D12 | Background jobs | None; expiry is applied when data is read | 13 Oct 2026 |
| D13 | Hosting | One Linux VPS (2 vCPU, 4 GB) with Docker Compose behind Cloudflare's free plan; not Azure. Provider deferred (Q1). | 13 Oct 2026 |
| D14 | Web app | Next.js with server rendering, hosted on Netlify | 13 Oct 2026 |
| D15 | API | ASP.NET Core on .NET 10 (LTS), supported until November 2028 | 13 Oct 2026 |
| D16 | Database | PostgreSQL 17 on the VPS | 13 Oct 2026 |
| D17 | Photo storage | Cloudinary free plan; uploads go through the API; usage watched against the monthly allowance | 13 Oct 2026 |
| D18 | Fonts | Baloo Da 2 for headings and money; Noto Sans Bengali for Bangla body; Nunito for English body | 13 Oct 2026 |
| D19 | Theme | Light theme only, even when the device prefers dark | 13 Oct 2026 |
| D20 | Home page | Shows the 4 most recently confirmed homes (4 across, 2 x 2, or 1 per row) | 13 Oct 2026 |
| D21 | Libraries | Moq and AutoMapper are banned; use NSubstitute or fakes, and explicit mapping | 12 Oct 2026 |
| D22 | Availability | About 99% monthly on one server; 99.5% after the post-MVP move | 13 Oct 2026 |
| D23 | Live listing edits | Edits to a Live listing stay live without going back to moderation, for now; every edit is logged | 13 Oct 2026 |
