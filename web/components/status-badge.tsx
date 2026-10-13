import { useTranslations } from 'next-intl';
import type { ListingStatus } from '@/lib/types';

const CLASS: Record<ListingStatus, string> = {
  draft: 'b-muted', published: 'b-green', pending: 'b-brass', changes: 'b-red', rejected: 'b-red',
  expired: 'b-red', paused: 'b-muted', rented: 'b-muted', archived: 'b-muted',
};

export function StatusBadge({ status }: { status: ListingStatus }) {
  const t = useTranslations();
  return <span className={`badge ${CLASS[status]}`}>{t(`st_${status}`)}</span>;
}
