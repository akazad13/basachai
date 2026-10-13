# API

A versioned REST API at `https://api.{domain}/v1`, JSON in and out, documented with OpenAPI. Errors use RFC 9457 ProblemDetails with a stable `code` the web app maps to Bangla and English messages.

Version 1.0 · 13 October 2026

## Conventions

- Auth: Identity cookie on `.{domain}`; CORS allows only `https://www.{domain}` with credentials; state-changing requests need the antiforgery header.
- IDs in URLs: `shortId` for public listing routes, `id` (uuid) for owner and admin routes.
- Lists use keyset cursors: `cursor` and `size` (max 24); responses include `nextCursor`.
- Money is whole taka integers. Dates are ISO 8601 UTC.
- Public GET responses send `Cache-Control: public, s-maxage=60`.
- `GET /listings` records `search_performed` when called with `X-Search-Source: home` or `search` (API clients). The web app's server renders send `X-Search-Source: none` and the browser reports the search through `POST /events` instead. It is recorded once per search: not for "Show more" pages (a `cursor` is present). Browser calls send the visit id (`bc_visit` cookie) as `X-Session-Id`, so a visit's searches and reveals share a session.
- Test-only hooks under `/v1/test/*` exist only in the Test and Staging environments; see [quality and testing](quality-and-testing.md#test-hooks). Staging requires the `X-Test-Key` header (`Test:HookKey`), and seeding refuses a database whose name does not contain "test" or "staging".

## Public

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/listings` | Search. Query: `area`, `min`, `max`, `beds`, `baths`, `type`, `furnish`, `occ`, `avail`, `q`, `sort`, `cursor`, `size` |
| GET | `/listings/{shortId}` | Listing detail; no phone, no address |
| POST | `/listings/{shortId}/contact` | Reveal phone; rate-limited; records a contact event |
| POST | `/listings/{shortId}/reports` | Report; body `{ reason, details? }` |
| GET | `/locations` | Area tree |
| GET | `/locations/suggest?q=` | Spelling-tolerant area suggestions |
| GET | `/property-types`, `/amenities` | Configured lists |
| GET | `/home` | `{ costExample: { card, oneTimeCosts } }` for the home page |
| POST | `/events` | Client analytics events, 300 per IP per hour: `listing_viewed` (`listingId`, `referrer`) and `search_performed` (`query`, `resultCount`, `source`), which the web app sends for each search it shows because the page may come from the edge cache. The query is parsed like a search, so only known filters are stored |

A listing that was never public (draft, in review, rejected before going live) returns 404 to visitors. One that was public but is no longer Live returns `{ available: false, status }` with no details. Its owner and staff get the full detail with `available: false`, for previews.

Public GETs never set cookies (they are cacheable). The device and session cookies are issued only by `/contact`, `/reports` and `/events`. The device cookie is signed.

### Card (search and saved results)

```json
{
  "shortId": "k7f3q2",
  "slug": "2-bedroom-flat-with-parking",
  "title": "দক্ষিণ পাড়ায় পার্কিংসহ ২ শোবার ঘরের ফ্ল্যাট",
  "area": { "slug": "dokkhin", "nameBn": "দক্ষিণ পাড়া", "nameEn": "Dokkhin Para" },
  "type": "flat",
  "bedrooms": 2, "bathrooms": 1, "floorAreaSqft": 760,
  "availableFrom": null,
  "rent": 10500, "serviceCharge": 1200,
  "monthlyOthers": [{ "label": "গ্যাস বিল (ডাবল চুলা)", "amount": 1080 }],
  "monthlyTotal": 12780,
  "advance": { "amount": 21000, "notSpecified": false },
  "cover": { "publicId": "basachai/prod/listings/...", "version": 1728800000 },
  "photoCount": 6,
  "lastConfirmedAt": "2026-10-10T08:00:00Z",
  "phoneVerified": false
}
```

Search response: `{ "items": [Card], "count": 15, "nextCursor": "eyJ..." }`.

Detail adds: `description`, `rules`, `floorNumber`, `furnishing`, `occupancy`, `amenities`, `oneTimeCosts`, `photos[]`, `owner { displayName, type: "owner" }`.

## Account

| Method | Path | Purpose |
| --- | --- | --- |
| POST | `/auth/register` | `{ name, phone, password }`; signs in |
| POST | `/auth/login`, `/auth/logout` | Session cookie. Staff with two-factor sign-in also send `code` |
| GET | `/auth/session` | `{ me }`, null when signed out (never 401) |
| GET | `/auth/csrf` | `{ token }`; also sets the readable `XSRF-TOKEN` cookie |
| POST | `/auth/password/change` | Signed-in change; ends other sessions |
| GET | `/auth/password/reset/check?token=` | `{ valid }`, so the reset page can say a link is used before anyone types |
| POST | `/auth/password/reset/confirm` | `{ token, newPassword }`; signs this device in and ends other sessions |
| GET, POST | `/me/two-factor` | Authenticator setup. The key is shown only while not enrolled; enrolling needs `{ code, password }` |
| GET | `/me/saved/ids` | Short ids of saved homes, to mark Save buttons |
| GET, PATCH | `/me` | Profile and locale |
| DELETE | `/me` | Delete account |
| GET | `/me/attention` | `{ count }` of owner listings needing action |
| GET | `/me/saved` | Saved cards |
| PUT, DELETE | `/me/saved/{listingId}` | Save, unsave (idempotent) |

## Owner

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/owner/listings` | Own listings with effective status, days left, views, reveals |
| POST | `/owner/listings` | Create draft |
| GET, PATCH | `/owner/listings/{id}` | Read, edit (draft, changes needed, or Live per D23). Fields left out are unchanged; `clear: ["floorAreaSqft", "floorNumber"]` empties optional numbers. `If-Match: "{version}"` is honoured |
| POST | `/owner/listings/{id}/submit` | Draft or Changes needed to In review |
| POST | `/owner/listings/{id}/reconfirm` | Live: fresh 30 days |
| POST | `/owner/listings/{id}/renew` | Expired to Live |
| POST | `/owner/listings/{id}/pause`, `/resume` | Live to Paused and back |
| POST | `/owner/listings/{id}/mark-rented` | Live to Rented |
| POST | `/owner/listings/{id}/archive` | Any except In review to Archived |
| POST | `/owner/listings/{id}/photos` | Multipart, one photo |
| PATCH | `/owner/listings/{id}/photos` | `{ order: [photoId] }` |
| DELETE | `/owner/listings/{id}/photos/{photoId}` | Remove a photo |

## Admin

Moderators and admins unless marked admin only.

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/admin/stats` | Queue, live, open reports, overdue counts, pilot metrics |
| GET | `/admin/listings` | Search by title, area, owner, status |
| GET | `/admin/listings/{id}/history` | Transitions, moderator actions, Live edits |
| POST | `/admin/listings/{id}/approve` | In review to Live |
| POST | `/admin/listings/{id}/request-changes`, `/reject` | Body `{ reason }` required |
| POST | `/admin/listings/{id}/hide`, `/unhide` | Hide flag |
| GET | `/admin/reports` | Open reports, oldest first |
| POST | `/admin/reports/{id}/resolve` | Body `{ action: "hide" or "dismiss" }` |
| GET | `/admin/users` | Admin only |
| POST | `/admin/users/{id}/restrict`, `/suspend`, `/reinstate` | Admin only |
| POST | `/admin/users/{id}/issue-password-reset` | Admin only; returns the one-time link |
| GET | `/admin/audit` | Filter by actor, action, entity, date |
| GET, POST, PATCH, DELETE | `/admin/locations`, `/admin/property-types`, `/admin/amenities` | Admin only; DELETE deactivates |
| PUT | `/admin/settings/home-cost-example` | Admin only; `{ shortId }` |
| GET | `/admin/photo-usage` | Admin only; `{ available, nearLimit, usage }`, Cloudinary's monthly usage (`available: false` if it cannot be read) |

Owner routes and admin routes accept either the listing's uuid or its short id. Restricted owners may pause, mark rented and archive, but not create, edit, submit, reconfirm, renew, resume or change photos. Staff without two-factor sign-in get 403 `two_factor_required` on every admin route.

## Errors

| HTTP | `code` | When |
| --- | --- | --- |
| 400 | `validation_failed` | Field errors in `errors: { field: [message keys] }` |
| 400 | `reset_used` | Reset link expired or already used |
| 401 | `unauthenticated` | Not signed in |
| 401 | `bad_credentials` | Wrong number or password |
| 401 | `two_factor_code_required`, `bad_code` | Staff sign-in needs (or got a wrong) authenticator code |
| 403 | `forbidden` | Wrong role |
| 403 | `csrf_failed` | Signed-in state-changing request without the matching `X-XSRF-TOKEN` header |
| 403 | `two_factor_required` | Staff route before two-factor sign-in is set up |
| 403 | `restricted`, `suspended` | Restricted user tried to post or change a listing; suspended user tried to sign in |
| 404 | `not_found` | Unknown listing or resource |
| 409 | `invalid_transition` | Action not allowed from the current status |
| 409 | `phone_taken` | Registering a number that already has an account |
| 409 | `photos_required` | Submit with fewer than 3 photos |
| 409 | `photo_limit` | More than 20 photos |
| 409 | `stale_listing` | Row version changed since it was read |
| 409 | `already_handled` | Another moderator acted first |
| 409 | `owner_unavailable` | Approving a listing whose owner is suspended or deleted |
| 409 | `already_enabled`, `key_taken` | Two-factor already on; configuration key already used |
| 413 | `photo_too_large` | Photo over 10 MB |
| 415 | `photo_type` | Not JPEG, PNG or WebP |
| 429 | `rate_limited` | Limit reached; `retryAfter` seconds included |
| 502 | `photo_store_unavailable` | Cloudinary failed |

## Related

[data model](data-model.md) · [listing lifecycle](../domain/listing-lifecycle.md) · [trust and safety](../domain/trust-and-safety.md#rate-limits)
