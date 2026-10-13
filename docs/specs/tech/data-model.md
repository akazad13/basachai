# Data model

Nineteen PostgreSQL tables cover users, listings, moderation, audit, analytics, a few admin settings and the hash salts. Money is whole taka (`integer`), times are UTC (`timestamptz`), and listings carry a `purpose` so sale listings can be added later.

Version 1.0 · 13 October 2026 · Covers FR-18, FR-22

## Tables

### Users and access

| Table | Columns | Notes |
| --- | --- | --- |
| `users` | `id` uuid, `phone_e164` unique, `phone_verified_at` null, `display_name`, `password_hash`, `status` (`active`, `restricted`, `suspended`), `preferred_locale` (`bn`, `en`), `created_at`, `deleted_at` | ASP.NET Core Identity, snake_case; roles in Identity role tables (`member`, `owner`, `moderator`, `admin`, `agent` disabled) |
| `password_reset_tokens` | `id`, `user_id`, `token_hash`, `issued_by`, `expires_at`, `consumed_at` | Single use, 24 hours |

### Reference data

| Table | Columns | Notes |
| --- | --- | --- |
| `locations` | `id`, `parent_id`, `path` ltree, `kind` (`division`, `district`, `upazila`, `area`), `name_bn`, `name_en`, `slug`, `is_active` | Seeded with Dhaka > Dhaka District > Dhamrai > 5 pilot areas |
| `location_aliases` | `location_id`, `alias`, `normalized` | Bangla and English spellings (Q5) |
| `property_types` | `id`, `key`, `name_bn`, `name_en`, `is_active` | `flat`, `house` |
| `amenities` | `id`, `key`, `name_bn`, `name_en`, `is_active` | Lift, parking, generator, gas, balcony, security, roof, yard |

### Listings

| Table | Columns | Notes |
| --- | --- | --- |
| `property_listings` | `id` uuid, `short_id` unique, `owner_id`, `purpose` (`rent`, `sale`), `property_type_id`, `location_id`, `title`, `slug`, `description`, `rules`, `monthly_rent_bdt`, `service_charge_bdt`, `bedrooms`, `bathrooms`, `floor_area_sqft`, `floor_number`, `furnishing`, `occupancy`, `available_from` date null, `full_address_enc` bytea, `advertiser_type` (`owner`), `status`, `is_hidden`, `submitted_at`, `published_at`, `last_confirmed_at`, `expires_at`, `moderator_reason`, `sale_details` jsonb null, `amenity_text`, `search_vector` tsvector (generated), `views_count`, `reveals_count`, `created_at`, `updated_at`, `version` integer | Status values in [lifecycle](../domain/listing-lifecycle.md#statuses) |
| `listing_charges` | `id`, `listing_id`, `kind` (`advance`, `other_monthly`, `other_one_time`), `label`, `amount_bdt` null, `not_specified` bool, `sort_order` | Advance stored here with `not_specified` when the owner chose not to state it |
| `listing_amenities` | `listing_id`, `amenity_id` | |
| `listing_photos` | `id`, `listing_id`, `cloudinary_public_id`, `version`, `width`, `height`, `bytes`, `sort_order`, `created_at` | Only uploads Cloudinary accepted; URLs are built, never stored |
| `saved_listings` | `user_id`, `listing_id`, `created_at` | Composite primary key |

### Moderation, audit and analytics

| Table | Columns | Notes |
| --- | --- | --- |
| `listing_reports` | `id`, `listing_id`, `reporter_id` null, `reporter_ip_hash`, `reason`, `details`, `status` (`open`, `hidden`, `dismissed`), `created_at`, `resolved_at`, `resolved_by` | |
| `moderation_actions` | `id`, `listing_id`, `report_id` null, `moderator_id`, `action`, `reason`, `created_at` | Append-only |
| `listing_availability_history` | `id`, `listing_id`, `from_status`, `to_status`, `actor_id` null, `actor_type` (`owner`, `moderator`, `system`), `created_at` | Every transition, including read-time expiry |
| `audit_log` | `id`, `actor_id`, `action`, `entity_type`, `entity_id`, `before` jsonb, `after` jsonb, `ip_hash`, `created_at` | Append-only; the app role has no UPDATE or DELETE. Live edits store changed fields (D23) |
| `contact_events` | `id`, `listing_id`, `user_id` null, `device_hash`, `ip_hash`, `created_at` | Phone reveals |
| `analytics_events` | `id`, `name`, `session_id`, `user_id` null, `props` jsonb, `created_at` | See [analytics](analytics.md) |

### Settings

| Table | Columns | Notes |
| --- | --- | --- |
| `app_settings` | `key`, `value` | Small admin choices, such as the home page cost example (`home.cost_example`) |
| `hash_salts` | `period` (`yyyy-MM`), `salt`, `created_at` | The monthly random salt for stored IP, device and session hashes; only the current and previous month are kept, and backups leave this table out |

## Indexes

```sql
CREATE INDEX ix_listings_search ON property_listings
  (location_id, bedrooms, monthly_rent_bdt, last_confirmed_at DESC, id DESC)
  WHERE status = 'published' AND NOT is_hidden AND purpose = 'rent';
CREATE INDEX ix_listings_owner     ON property_listings (owner_id, status);
CREATE INDEX ix_listings_expiring  ON property_listings (owner_id, expires_at) WHERE status = 'published';
CREATE INDEX ix_listings_queue     ON property_listings (submitted_at) WHERE status = 'pending';
CREATE INDEX ix_listings_text      ON property_listings USING gin (search_vector);
CREATE INDEX ix_locations_path     ON locations USING gist (path);
CREATE INDEX ix_aliases_trgm       ON location_aliases USING gin (normalized gin_trgm_ops);
CREATE INDEX ix_reports_open       ON listing_reports (created_at) WHERE status = 'open';
CREATE INDEX ix_audit_entity       ON audit_log (entity_type, entity_id, created_at);
```

## Rules

- `search_vector` is a stored generated column using the `simple` text configuration over title, description and `amenity_text` (amenity names in both scripts, kept by the API). PostgreSQL has no Bangla stemmer.
- `full_address_enc` is encrypted with ASP.NET Core Data Protection; the key ring is backed up with the database.
- Optimistic concurrency uses the app-managed `version` column, raised on every edit or status change (including changes to a listing's charges, photos and amenities). View and reveal counters are raised with atomic `UPDATE ... SET n = n + 1` and never touch `version`, so busy listings do not cause false conflicts. Clients editing a Live listing send `If-Match: "{version}"`; a mismatch returns 409 `stale_listing`.
- Entity ids are created by the API (`Guid`), not by the database.
- Separate database roles for the API and for migrations.
- Seed data: locations, aliases, property types, amenities.

## Related

[API](api.md) · [listing and pricing](../domain/listing-and-pricing.md) · [listing lifecycle](../domain/listing-lifecycle.md)
