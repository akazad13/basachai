# Product overview

BasaChai (বাসা চাই) is a Bangla-first rental marketplace where owners post homes for free and tenants see the full monthly cost and call the owner directly. It pilots in Dhamrai and expands to Dhaka city next.

Version 1.0 · 13 October 2026 · Source: Master Spec v1.2, sections 2, 4 and 5

## Summary

| | |
| --- | --- |
| Promise | বাসা খুঁজুন। মালিকের সাথে সরাসরি কথা বলুন। (Find a home. Talk to the owner directly.) |
| Market | Dhamrai, Dhaka District, Bangladesh. Dhaka city after the pilot. |
| Platform | Mobile-first responsive website; no native apps in the MVP |
| Business model | Free listings; no paid placement |
| Language | Bangla first, English available through a toggle |

## Pilot areas

Spellings confirmed. These are the only areas offered at launch.

| Key | Bangla | English |
| --- | --- | --- |
| `amin` | আমিন মডেল টাউন | Amin Model Town |
| `barat` | বরাত নগর | Barat Nagor |
| `dokkhin` | দক্ষিণ পাড়া | Dokkhin Para |
| `bazar` | ধামরাই বাজার | Dhamrai Bazar |
| `islampur` | ইসলামপুর | Islampur |

## Users

| Role | Goal |
| --- | --- |
| Tenant (anonymous or member) | Find a home that fits budget, area, household and move-in date, then call the owner |
| Owner | Advertise a home for free, get genuine calls, keep the listing fresh |
| Moderator | Review new listings and reports within one business day |
| Admin | Moderator rights plus users, configuration and audit |

## Permissions

| Action | Anonymous | Member | Owner | Moderator | Admin |
| --- | :-: | :-: | :-: | :-: | :-: |
| Search and view listings | Yes | Yes | Yes | Yes | Yes |
| Reveal phone number | Yes | Yes | Yes | Yes | Yes |
| Report a listing | Yes | Yes | Yes | Yes | Yes |
| Save homes | No | Yes | Yes | Yes | Yes |
| Post and manage own listings | No | No | Yes | No | No |
| Approve, reject, request changes, hide | No | No | No | Yes | Yes |
| Handle reports | No | No | No | Yes | Yes |
| Restrict, suspend, issue password reset | No | No | No | No | Yes |
| Configure areas, types, amenities | No | No | No | No | Yes |

- A member becomes an owner the first time they choose "Post a property" while signed in.
- Moderators and admins must use two-factor sign-in with an authenticator app.
- An Agent role exists in the data model but is disabled (D2).

## MVP scope

### In

- Home, search, results and property pages
- Filters, sorting and shareable search URLs
- Registration and login with phone number and password
- Owner posting in four steps, with photo upload
- Owner dashboard with reminders and statuses
- Phone reveal and call
- Saved homes for signed-in users
- Reports and a moderation queue
- Basic admin: users, configuration, audit
- Bangla and English interface
- SEO, analytics events, Safety tips, Terms and Privacy pages

### Out

- Payments, paid listings or subscriptions
- Agent accounts
- SMS, email, push notifications and background jobs
- Phone or ownership verification
- Self-service password reset
- WhatsApp contact and in-app chat
- Areas outside Dhamrai
- Native apps, maps, sale listings, digital rental agreements
