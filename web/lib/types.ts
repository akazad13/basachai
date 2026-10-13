/** Shapes returned by the BasaChai API (see specs/tech/api.md). */

export type Area = { slug: string; nameBn: string; nameEn: string };
export type MoneyLine = { label: string; amount: number | null };
export type OneTimeLine = { label: string; amount: number | null; isAdvance: boolean };
export type PhotoRef = { publicId: string; version: number };
export type Photo = PhotoRef & { id: string; width: number; height: number };
export type Named = { key: string; nameBn: string; nameEn: string };

export type Card = {
  shortId: string;
  slug: string;
  title: string;
  area: Area;
  type: string;
  bedrooms: number;
  bathrooms: number | null;
  floorAreaSqft: number | null;
  availableFrom: string | null;
  rent: number;
  serviceCharge: number;
  monthlyOthers: MoneyLine[];
  monthlyTotal: number;
  advance: { amount: number | null; notSpecified: boolean };
  cover: PhotoRef | null;
  photoCount: number;
  lastConfirmedAt: string | null;
  phoneVerified: boolean;
};

export type SearchPage = { items: Card[]; count: number; nextCursor: string | null };

export type ListingDetail = {
  available: boolean;
  status: ListingStatus;
  card: Card | null;
  description: string | null;
  rules: string | null;
  floorNumber: number | null;
  furnishing: string | null;
  occupancy: string | null;
  amenities: Named[];
  oneTimeCosts: OneTimeLine[];
  photos: Photo[];
  owner: { displayName: string; type: string } | null;
  isHidden: boolean;
};

export type ListingStatus = 'draft' | 'pending' | 'changes' | 'rejected' | 'published' | 'paused' | 'rented' | 'expired' | 'archived';

export type OtherCost = { label: string; amount: number | null; period: 'monthly' | 'one_time' };

export type OwnerListing = {
  id: string;
  shortId: string;
  title: string;
  status: ListingStatus;
  isHidden: boolean;
  daysLeft: number | null;
  views: number;
  reveals: number;
  monthlyTotal: number;
  cover: PhotoRef | null;
  moderatorReason: string | null;
  submittedAt: string | null;
  lastConfirmedAt: string | null;
  type: string | null;
  area: string | null;
  bedrooms: number;
  bathrooms: number | null;
  floorAreaSqft: number | null;
  floorNumber: number | null;
  furnishing: string;
  occupancy: string;
  availableFrom: string | null;
  description: string | null;
  rules: string | null;
  fullAddress: string | null;
  monthlyRent: number | null;
  serviceCharge: number | null;
  advance: { amount: number | null; notSpecified: boolean } | null;
  others: OtherCost[];
  amenities: string[];
  photos: Photo[];
  /** Sent back as If-Match so a save from a second tab cannot silently overwrite this one. */
  version: number;
};

export type Me = {
  id: string;
  name: string;
  phone: string;
  locale: 'bn' | 'en';
  roles: string[];
  twoFactorEnabled: boolean;
  status: 'active' | 'restricted' | 'suspended';
};

export type Locations = { slug: string; nameBn: string; nameEn: string; areas: Area[] };

export type QueueItem = {
  id: string;
  shortId: string;
  status: ListingStatus;
  isHidden: boolean;
  card: Card;
  description: string | null;
  ownerName: string;
  ownerId: string;
  submittedAt: string | null;
  overdue: boolean;
  oneTimeCosts: OneTimeLine[];
};

export type Report = {
  id: string;
  listingShortId: string;
  listingId: string;
  listingTitle: string;
  reason: 'suspicious' | 'inaccurate' | 'duplicate' | 'unavailable';
  details: string | null;
  createdAt: string;
  overdue: boolean;
  anonymous: boolean;
};

/** Cloudinary's monthly usage, for admins (photos spec, Free-plan budget). */
export type PhotoUsage = {
  available: boolean;
  nearLimit: boolean;
  usage: { plan: string | null; creditsUsed: number; creditsLimit: number; usedPercent: number; storageBytes: number; bandwidthBytes: number; transformations: number; updatedAt: string } | null;
};

export type Stats = {
  pending: number;
  live: number;
  openReports: number;
  overdue: number;
  metrics: {
    completeness: number | null;
    freshness: number | null;
    searchToContact: number | null;
    ownersActivated: number;
    reportHandling: number | null;
  };
};

export type HistoryItem = {
  at: string;
  kind: 'status' | 'moderation' | 'edit';
  action: string;
  actorType: string | null;
  actorId: string | null;
  reason: string | null;
  before: Record<string, unknown> | null;
  after: Record<string, unknown> | null;
};

export type AdminUser = {
  id: string;
  name: string;
  phone: string;
  status: 'active' | 'restricted' | 'suspended';
  roles: string[];
  createdAt: string;
  listings: number;
};

export type Problem = {
  status: number;
  code: string;
  title?: string;
  errors?: Record<string, string[]>;
  retryAfter?: number;
};
