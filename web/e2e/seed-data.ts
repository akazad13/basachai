import pilot from '../../deploy/seed/pilot.json';

/** The pilot dataset the API seeds, so tests and the database agree on every value (quality-and-testing spec). */
type PilotUser = (typeof pilot.users)[number];
type PilotListing = (typeof pilot.listings)[number] & { fullAddress?: string };

export const USERS = Object.fromEntries(pilot.users.map(u => [u.key, u])) as Record<
  'tenant' | 'owner' | 'owner2' | 'moderator' | 'admin' | 'moderator_new' | `owner${number}`, PilotUser>;

/** Owner-written text is shown as typed, so a title reads the same in both languages. */
export function listing(id: string) {
  const l = pilot.listings.find(x => x.id === id) as PilotListing | undefined;
  if (!l) throw new Error(`No listing ${id} in the pilot dataset`);
  const owner = pilot.users.find(u => u.key === l.owner)!;
  const same = (s: string | null) => ({ bn: s ?? '', en: s ?? '' });
  return { ...l, title: same(l.title), reason: same(l.reason), owner, fullAddress: l.fullAddress ?? '' };
}

export const LIVE_IDS = ['A1', 'A2', 'A3', 'B1', 'B2', 'B3', 'D1', 'D2', 'D3', 'Z1', 'Z2', 'Z3', 'I1', 'I2', 'I3'];
export const AREAS = ['amin', 'barat', 'dokkhin', 'bazar', 'islampur'] as const;
