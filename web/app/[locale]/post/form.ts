import { toLatinDigits } from '@/lib/format';
import type { Card, OneTimeLine, OtherCost, OwnerListing } from '@/lib/types';

/** The posting form as typed: strings, so half-filled fields survive re-renders. */
export type Draft = {
  title: string; type: string; area: string; beds: string; baths: string; sqft: string; floor: string;
  furnish: string; occ: string; availNow: boolean; availDate: string; amenities: string[];
  desc: string; rules: string; address: string;
  rent: string; service: string; advance: string; advNone: boolean;
  others: { label: string; amount: string; period: 'monthly' | 'one_time' }[];
};

export const emptyDraft = (): Draft => ({
  title: '', type: 'flat', area: '', beds: '2', baths: '1', sqft: '', floor: '', furnish: 'unfurnished', occ: 'any',
  availNow: true, availDate: '', amenities: [], desc: '', rules: '', address: '',
  rent: '', service: '', advance: '', advNone: false, others: [],
});

const s = (v: number | null | undefined) => (v == null ? '' : String(v));

export function fromListing(l: OwnerListing): Draft {
  return {
    title: l.title, type: l.type ?? 'flat', area: l.area ?? '', beds: String(l.bedrooms), baths: s(l.bathrooms),
    sqft: s(l.floorAreaSqft), floor: s(l.floorNumber), furnish: l.furnishing, occ: l.occupancy,
    availNow: !l.availableFrom, availDate: l.availableFrom ?? '', amenities: l.amenities,
    desc: l.description ?? '', rules: l.rules ?? '', address: l.fullAddress ?? '',
    rent: s(l.monthlyRent), service: s(l.serviceCharge),
    advance: s(l.advance?.amount), advNone: !!l.advance?.notSpecified,
    others: l.others.map(o => ({ label: o.label, amount: s(o.amount), period: o.period })),
  };
}

const int = (v: string) => { const n = parseInt(toLatinDigits(v.trim()), 10); return Number.isFinite(n) ? n : null; };

export type FieldErrors = Partial<Record<keyof Draft | 'photos', string>>;

/** Message keys for the fields of one step that block "Next". */
export function validate(step: number, d: Draft, photoCount: number): FieldErrors {
  const e: FieldErrors = {};
  if (step === 1) {
    const len = d.title.trim().length;
    if (len === 0) e.title = 'required'; else if (len < 10 || len > 90) e.title = 'title_length';
    if (!d.area) e.area = 'required';
    if (!d.availNow && !d.availDate) e.availDate = 'required';
  }
  if (step === 2) {
    if ((int(d.rent) ?? 0) <= 0) e.rent = 'required';
    if (int(d.service) == null || int(d.service)! < 0) e.service = 'required';
    if (!d.advNone && (int(d.advance) ?? 0) <= 0) e.advance = 'required';
  }
  if (step === 3 && photoCount < 3) e.photos = 'need_photos';
  return e;
}

/** The first step that still needs something: where "Continue" reopens a draft. */
export function firstIncompleteStep(d: Draft, photoCount: number): number {
  for (const step of [1, 2, 3]) if (Object.keys(validate(step, d, photoCount)).length) return step;
  return 4;
}

/** Fields worth saving now: anything filled in and valid enough for the API. Blank optional text is sent as "" to clear it. */
export function toInput(d: Draft, step: number): Record<string, unknown> {
  const out: Record<string, unknown> = {};
  if (step >= 1) {
    if (d.title.trim()) out.title = d.title.trim();
    out.type = d.type;
    if (d.area) out.area = d.area;
    out.bedrooms = int(d.beds) ?? 0;
    if (int(d.baths)) out.bathrooms = int(d.baths);
    if (int(d.sqft)) out.floorAreaSqft = int(d.sqft);
    if (int(d.floor) != null) out.floorNumber = int(d.floor);
    // Left out means "unchanged" to the API, so emptied optional numbers are named.
    const clear = [!int(d.sqft) && 'floorAreaSqft', int(d.floor) == null && 'floorNumber'].filter(Boolean);
    if (clear.length) out.clear = clear;
    out.furnishing = d.furnish;
    out.occupancy = d.occ;
    if (d.availNow) out.availableNow = true; else if (d.availDate) out.availableFrom = d.availDate;
    out.amenities = d.amenities;
    out.description = d.desc;
    out.rules = d.rules;
    out.fullAddress = d.address;
  }
  if (step >= 2) {
    if ((int(d.rent) ?? 0) > 0) out.monthlyRent = int(d.rent);
    if (int(d.service) != null && int(d.service)! >= 0) out.serviceCharge = int(d.service);
    if (d.advNone) out.advance = { amount: null, notSpecified: true };
    else if ((int(d.advance) ?? 0) > 0) out.advance = { amount: int(d.advance), notSpecified: false };
    out.others = d.others.filter(o => o.label.trim()).map<OtherCost>(o => ({ label: o.label.trim(), amount: int(o.amount), period: o.period }));
  }
  return out;
}

/** A card built from the form, for the live cost preview and the review step. */
export function previewCard(d: Draft, area: { slug: string; nameBn: string; nameEn: string } | undefined, cover: Card['cover'], photoCount: number): { card: Card; oneTime: OneTimeLine[] } {
  const rent = int(d.rent) ?? 0, service = int(d.service) ?? 0;
  const others = d.others.filter(o => o.label.trim());
  const monthly = others.filter(o => o.period === 'monthly').map(o => ({ label: o.label.trim(), amount: int(o.amount) }));
  const advance = d.advNone ? null : int(d.advance);
  return {
    card: {
      shortId: 'preview', slug: '', title: d.title.trim(), area: area ?? { slug: '', nameBn: '', nameEn: '' }, type: d.type,
      bedrooms: int(d.beds) ?? 0, bathrooms: int(d.baths), floorAreaSqft: int(d.sqft),
      availableFrom: d.availNow ? null : d.availDate || null, rent, serviceCharge: service, monthlyOthers: monthly,
      monthlyTotal: rent + service + monthly.reduce((a, o) => a + (o.amount ?? 0), 0),
      advance: { amount: advance, notSpecified: advance == null }, cover, photoCount, lastConfirmedAt: null, phoneVerified: false,
    },
    oneTime: [
      { label: 'advance', amount: advance, isAdvance: true },
      ...others.filter(o => o.period === 'one_time').map(o => ({ label: o.label.trim(), amount: int(o.amount), isAdvance: false })),
    ],
  };
}
