'use client';

import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { useTranslations } from 'next-intl';
import { api, ApiError } from '@/lib/api';
import { useToast } from '@/components/toast';

type Item = { key?: string; slug?: string; kind?: string; nameBn: string; nameEn: string; isActive: boolean };
type Kind = 'locations' | 'property-types' | 'amenities';
const TITLE: Record<Kind, string> = { locations: 'areas_t', 'property-types': 'types_t', amenities: 'amenities_t' };

/** Admin only: areas (with spelling aliases), property types and amenities. Items are deactivated, never deleted. */
export default function ConfigPage() {
  const t = useTranslations();
  return (
    <>
      <div className="dash-head"><h1 style={{ fontSize: 28 }}>{t('config_t')}</h1></div>
      {(['locations', 'property-types', 'amenities'] as Kind[]).map(k => <ConfigList key={k} kind={k} title={t(TITLE[k] as 'areas_t')} />)}
    </>
  );
}

function ConfigList({ kind, title }: { kind: Kind; title: string }) {
  const t = useTranslations();
  const toast = useToast();
  const [items, setItems] = useState<Item[]>([]);
  const [error, setError] = useState('');
  const load = useCallback(async () => {
    const all = await api<Item[]>(`/v1/admin/${kind}`);
    setItems(kind === 'locations' ? all.filter(i => i.kind === 'area') : all);
  }, [kind]);
  useEffect(() => { void load(); }, [load]);

  const keyOf = (i: Item) => i.key ?? i.slug ?? '';

  async function add(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const form = e.currentTarget;
    const f = new FormData(form);
    const aliases = String(f.get('aliases') ?? '').split(',').map(s => s.trim()).filter(Boolean);
    try {
      await api(`/v1/admin/${kind}`, { method: 'POST', json: { key: f.get('key'), nameBn: f.get('nameBn'), nameEn: f.get('nameEn'), aliases } });
      form.reset(); setError('');
      toast(t('t_config_saved'));
      await load();
    } catch (err) {
      setError(err instanceof ApiError && err.code === 'validation_failed' ? t('invalid') : err instanceof ApiError && err.code === 'key_taken' ? t('invalid') : t('error_generic'));
    }
  }

  async function toggle(i: Item) {
    if (i.isActive) await api(`/v1/admin/${kind}/${keyOf(i)}`, { method: 'DELETE' });
    else await api(`/v1/admin/${kind}/${keyOf(i)}`, { method: 'PATCH', json: { isActive: true } });
    toast(t('t_config_saved'));
    await load();
  }

  const id = (n: string) => `${kind}-${n}`;
  return (
    <section className="panel" aria-labelledby={id('t')} style={{ marginBottom: 16 }}>
      <h2 id={id('t')}>{title}</h2>
      <div className="table-wrap" style={{ marginBottom: 12 }}>
        <table className="data">
          <thead><tr><th>{t('key')}</th><th>{t('name_bn')}</th><th>{t('name_en')}</th><th>{t('active')}</th></tr></thead>
          <tbody>
            {items.map(i => (
              <tr key={keyOf(i)}>
                <td><code>{keyOf(i)}</code></td><td lang="bn">{i.nameBn}</td><td>{i.nameEn}</td>
                <td><button type="button" className="btn btn-ghost btn-sm" onClick={() => toggle(i)}>{t(i.isActive ? 'deactivate' : 'reinstate')}</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <form onSubmit={add} className="row3" noValidate>
        <div className="field"><label className="f" htmlFor={id('key')}>{t('key')}</label><input id={id('key')} name="key" required pattern="[a-z0-9_-]{2,40}" /></div>
        <div className="field"><label className="f" htmlFor={id('bn')}>{t('name_bn')}</label><input id={id('bn')} name="nameBn" lang="bn" required /></div>
        <div className="field"><label className="f" htmlFor={id('en')}>{t('name_en')}</label><input id={id('en')} name="nameEn" required /></div>
        {kind === 'locations' && <div className="field"><label className="f" htmlFor={id('al')}>{t('aliases')}</label><input id={id('al')} name="aliases" /></div>}
        <div className="field" style={{ alignSelf: 'end' }}><button className="btn btn-primary" type="submit">{t('add')}</button></div>
      </form>
      {error && <p className="err" role="alert">{error}</p>}
    </section>
  );
}
