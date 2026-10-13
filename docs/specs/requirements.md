# Requirements

Eighteen of 22 functional requirements are Must for the MVP; three are Should, and phone verification (FR-08) is deferred. The launch checklist at the end decides when the pilot goes live.

Version 1.0 · 13 October 2026 · Source: Master Spec v1.2, sections 6 and 17

## Functional requirements

| ID | Requirement | Priority | Spec |
| --- | --- | --- | --- |
| FR-01 | Browse published rentals without logging in | Must | [search](features/search.md), [home](features/home.md) |
| FR-02 | Search by area and keywords | Must | [search](features/search.md) |
| FR-03 | Filter by rent, bedrooms, bathrooms, type, furnishing, who can rent, move-in | Must | [search](features/search.md) |
| FR-04 | Sort by recently confirmed, newest, rent low to high, rent high to low | Must | [search](features/search.md) |
| FR-05 | Cards show photo, total monthly cost, key facts, freshness | Must | [search](features/search.md), [design system](design/design-system.md) |
| FR-06 | Property page shows all facts, cost breakdown, photos, advertiser | Must | [property page](features/property-page.md) |
| FR-07 | Register, log in, log out, manage profile | Must | [auth and account](features/auth-and-account.md) |
| FR-08 | Verify advertiser phone numbers | Deferred (D9) | [open questions](open-questions.md) Q8 |
| FR-09 | Create and preview a listing before submitting | Must | [post a property](features/post-property.md) |
| FR-10 | Edit, pause, resume, renew, reconfirm, archive, mark rented | Must | [owner dashboard](features/owner-dashboard.md), [lifecycle](domain/listing-lifecycle.md) |
| FR-11 | Upload, reorder and remove photos | Must | [post a property](features/post-property.md), [photos](tech/photos.md) |
| FR-12 | Moderators approve, reject, request changes, hide | Must | [moderation](features/moderation-and-admin.md) |
| FR-13 | Reveal or call the advertiser's number | Must | [property page](features/property-page.md) |
| FR-14 | Save and unsave homes | Should | [saved homes](features/saved-homes.md) |
| FR-15 | Report suspicious or wrong listings | Must | [property page](features/property-page.md), [trust and safety](domain/trust-and-safety.md) |
| FR-16 | Rented, rejected, expired, paused, hidden and archived listings never appear in public results | Must | [lifecycle](domain/listing-lifecycle.md) |
| FR-17 | Admins search listings, users and reports | Must | [moderation](features/moderation-and-admin.md) |
| FR-18 | Audit trail for moderation, admin actions and live-listing edits | Must | [data model](tech/data-model.md) |
| FR-19 | Readable URLs and SEO metadata | Should | [content and localization](design/content-and-localization.md) |
| FR-20 | Track searches, views, reveals and submissions | Must | [analytics](tech/analytics.md) |
| FR-21 | Bangla-first interface with English available | Should | [content and localization](design/content-and-localization.md) |
| FR-22 | Data model supports sale listings later | Must | [data model](tech/data-model.md) |

## Launch checklist

The pilot launches when every item is checked. Each one maps to end-to-end tests in [quality and testing](tech/quality-and-testing.md).

- [ ] A visitor can search and filter live rentals in all five pilot areas, in Bangla and English.
- [ ] A tenant sees the cost breakdown, the correct monthly total and one-time costs on every listing.
- [ ] A tenant can reveal and call a number without logging in, within the rate limits.
- [ ] An owner can register, create a listing, upload at least 3 photos and submit it.
- [ ] An owner sees moderation results and 3-day expiry reminders on the dashboard, and can reconfirm, renew, pause and mark rented.
- [ ] A moderator can approve, reject or request changes (with a reason) and hide a reported listing.
- [ ] Rented, expired, paused, hidden and archived listings never appear in public results.
- [ ] Every contact number is labelled "not verified" and no page implies verification.
- [ ] Photos have no GPS metadata after upload.
- [ ] All analytics events are recorded.
- [ ] Pages meet the performance and accessibility targets on a mid-range Android phone.
- [ ] Backups run nightly, a full restore has been rehearsed, and the uptime monitor alerts.
- [ ] Terms, Privacy and Safety pages are published; brand, domain and trademark are cleared.
