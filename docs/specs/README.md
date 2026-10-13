# BasaChai specs

Individual specifications for the BasaChai rental MVP (Phase 1, Dhamrai pilot), split out of the [Master Spec](../BasaChai-Master-Spec.html) v1.2 so each area can be built, reviewed and ticketed on its own.

Version 1.0 · 13 October 2026

## Source of truth

The [Master Spec](../BasaChai-Master-Spec.html) wins if anything here disagrees with it. To change a decision, update the master spec's decision log first, then the matching file here.

## Index

### Product

| File | What it covers |
| --- | --- |
| [product-overview.md](product-overview.md) | Product summary, pilot areas, scope, roles and permissions |
| [decisions.md](decisions.md) | All 23 settled decisions (D1 to D23) |
| [requirements.md](requirements.md) | Functional requirements FR-01 to FR-22 and the launch checklist |
| [journey.md](journey.md) | Seven end-to-end journeys across roles, each with an automated journey test |
| [roadmap.md](roadmap.md) | Phases and the triggers for replacing MVP shortcuts |
| [open-questions.md](open-questions.md) | Questions still open, with working defaults |
| [glossary.md](glossary.md) | Terms used across the specs |

### Features (one per screen or flow)

| File | Route | Who |
| --- | --- | --- |
| [features/home.md](features/home.md) | `/` | Everyone |
| [features/search.md](features/search.md) | `/search` | Everyone |
| [features/property-page.md](features/property-page.md) | `/listing/{shortId}` | Everyone |
| [features/auth-and-account.md](features/auth-and-account.md) | Sign-in dialog, `/account` | Everyone, staff |
| [features/saved-homes.md](features/saved-homes.md) | `/saved` | Signed-in users |
| [features/post-property.md](features/post-property.md) | `/post` | Owners |
| [features/owner-dashboard.md](features/owner-dashboard.md) | `/dashboard` | Owners |
| [features/moderation-and-admin.md](features/moderation-and-admin.md) | `/admin` | Moderators, admins |

### Domain rules

| File | What it covers |
| --- | --- |
| [domain/listing-and-pricing.md](domain/listing-and-pricing.md) | Listing fields, validation, cost breakdown and totals |
| [domain/listing-lifecycle.md](domain/listing-lifecycle.md) | Statuses, transitions, read-time expiry, edits |
| [domain/trust-and-safety.md](domain/trust-and-safety.md) | Moderation, reports, wording, privacy, rate limits |

### Design

| File | What it covers |
| --- | --- |
| [design/design-system.md](design/design-system.md) | Colour, type, components, layout, accessibility |
| [design/content-and-localization.md](design/content-and-localization.md) | Bangla and English rules, formats, UI copy |

### Technical

| File | What it covers |
| --- | --- |
| [tech/architecture.md](tech/architecture.md) | Stack, hosting, server layout, cost, environments |
| [tech/data-model.md](tech/data-model.md) | Tables, columns, indexes |
| [tech/api.md](tech/api.md) | Endpoints, payloads, errors |
| [tech/photos.md](tech/photos.md) | Cloudinary upload, delivery and limits |
| [tech/security-and-operations.md](tech/security-and-operations.md) | Security controls, backups, monitoring, deploys |
| [tech/analytics.md](tech/analytics.md) | Events and pilot metrics |
| [tech/quality-and-testing.md](tech/quality-and-testing.md) | Non-functional targets and the test plan |

## Conventions

- **IDs.** Decisions `D1`, requirements `FR-01`, open questions `Q1`. Each feature spec numbers its acceptance criteria with a prefix, for example `SRCH-1`.
- **Acceptance criteria** use Given / When / Then. Every spec with behaviour has a **Test cases** section: E2E test cases (preconditions, steps, expected) and the automation (Playwright for the browser, xUnit for API and domain). Shared test data, hooks and fixtures are in [tech/quality-and-testing.md](tech/quality-and-testing.md).
- **Copy** is given in Bangla first, then English. Exact strings live in the web app's message files.
- **Placeholders.** `{domain}` is the production domain, still to be chosen (Q10).
