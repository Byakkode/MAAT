import { MapPin } from 'lucide-react'
import { type FormEvent, useState } from 'react'
import { ApiError } from '../../api/authApi'
import * as vsmeApi from '../../api/vsmeApi'
import type { CompanySite, CompanySiteInput, SiteTenure } from '../../api/vsmeApi'
import { SITE_TENURE_LABELS } from '../../constants/vsme'
import { LIST_INPUT_CLASS } from './VsmeFields'

// docs/specs/norme-volontaire.md : sites de l'entreprise (B1 géolocalisation, B5 zones
// sensibles). Chaque site s'enregistre à part, tout de suite : le serveur le géocode (ADR 0013)
// et dit s'il a trouvé l'adresse, ce que l'écran affiche aussitôt.

const EMPTY_SITE: CompanySiteInput = {
  name: '',
  address: '',
  tenure: 'Owned',
  inOrNearSensitiveArea: null,
  sensitiveAreaName: null,
}

function SiteForm({
  initial,
  onSubmit,
  onCancel,
}: {
  initial: CompanySiteInput
  onSubmit: (input: CompanySiteInput) => Promise<void>
  onCancel: () => void
}) {
  const [site, setSite] = useState(initial)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setSaving(true)
    setError(null)
    try {
      await onSubmit(site)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Impossible d'enregistrer le site.")
      setSaving(false)
    }
  }

  return (
    <form onSubmit={(e) => void handleSubmit(e)} className="flex flex-col gap-2 rounded-lg border border-border bg-bg p-3">
      <div className="flex flex-wrap gap-2">
        <label className="flex min-w-0 flex-1 basis-40 flex-col gap-1 text-[12.5px] text-text-muted">
          Nom du site
          <input
            value={site.name}
            onChange={(e) => setSite({ ...site, name: e.target.value })}
            required
            maxLength={120}
            className={LIST_INPUT_CLASS}
          />
        </label>
        <label className="flex min-w-0 flex-1 basis-40 flex-col gap-1 text-[12.5px] text-text-muted">
          Statut
          <select
            value={site.tenure}
            onChange={(e) => setSite({ ...site, tenure: e.target.value as SiteTenure })}
            className={LIST_INPUT_CLASS}
          >
            {Object.entries(SITE_TENURE_LABELS).map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
        </label>
      </div>
      <label className="flex flex-col gap-1 text-[12.5px] text-text-muted">
        Adresse postale complète
        <input
          value={site.address}
          onChange={(e) => setSite({ ...site, address: e.target.value })}
          required
          maxLength={300}
          placeholder="12 rue de la Paix 75002 Paris"
          className={LIST_INPUT_CLASS}
        />
      </label>
      <fieldset className="flex flex-wrap items-center gap-3 text-[12.5px] text-text-muted">
        <legend className="mb-1">Site situé dans ou près d&apos;une zone sensible pour la biodiversité (Natura 2000, ZNIEFF, réserve…) ?</legend>
        {[true, false].map((option) => (
          <label key={String(option)} className="flex items-center gap-1.5 text-[13px] text-text">
            <input
              type="radio"
              name="site-sensitive"
              checked={site.inOrNearSensitiveArea === option}
              onChange={() => setSite({ ...site, inOrNearSensitiveArea: option })}
              className="accent-blue-maat"
            />
            {option ? 'Oui' : 'Non'}
          </label>
        ))}
      </fieldset>
      {site.inOrNearSensitiveArea === true && (
        <label className="flex flex-col gap-1 text-[12.5px] text-text-muted">
          Nom de la zone sensible
          <input
            value={site.sensitiveAreaName ?? ''}
            onChange={(e) => setSite({ ...site, sensitiveAreaName: e.target.value || null })}
            maxLength={200}
            className={LIST_INPUT_CLASS}
          />
        </label>
      )}
      {error && (
        <p role="alert" className="text-[12.5px] text-red">
          {error}
        </p>
      )}
      <div className="flex gap-3">
        <button
          type="submit"
          disabled={saving}
          className="rounded-lg bg-blue-maat px-3 py-1.5 text-[13px] font-semibold text-white disabled:opacity-40"
        >
          {saving ? 'Enregistrement…' : 'Enregistrer le site'}
        </button>
        <button type="button" onClick={onCancel} className="text-[13px] font-medium text-text-muted hover:underline">
          Annuler
        </button>
      </div>
    </form>
  )
}

export function SitesEditor({
  sites,
  onChange,
  readOnly,
}: {
  sites: CompanySite[]
  onChange: (sites: CompanySite[]) => void
  readOnly: boolean
}) {
  // null : aucun formulaire ouvert ; 'new' : ajout ; sinon identifiant du site modifié.
  const [editing, setEditing] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  async function handleCreate(input: CompanySiteInput) {
    const created = await vsmeApi.createSite(input)
    onChange([...sites, created])
    setEditing(null)
  }

  async function handleUpdate(id: string, input: CompanySiteInput) {
    const updated = await vsmeApi.updateSite(id, input)
    onChange(sites.map((s) => (s.id === id ? updated : s)))
    setEditing(null)
  }

  async function handleDelete(id: string) {
    setError(null)
    try {
      await vsmeApi.deleteSite(id)
      onChange(sites.filter((s) => s.id !== id))
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Impossible de supprimer le site.')
    }
  }

  return (
    <div className="border-b border-border py-2.5 last:border-0">
      <p className="text-[13px] text-text-muted">Sites détenus, loués ou gérés</p>
      <p className="text-[11.5px] font-light text-text-muted">
        L&apos;adresse est localisée automatiquement (service public de l&apos;IGN) pour la géolocalisation demandée par B1.
      </p>
      {sites.length === 0 && editing !== 'new' && (
        <p className="mt-2 text-[13px] text-text-muted">Aucun site déclaré.</p>
      )}
      <ul className="mt-2 flex flex-col gap-2">
        {sites.map((site) =>
          editing === site.id ? (
            <li key={site.id}>
              <SiteForm initial={site} onSubmit={(input) => handleUpdate(site.id, input)} onCancel={() => setEditing(null)} />
            </li>
          ) : (
            <li key={site.id} className="flex flex-wrap items-start gap-3 rounded-lg border border-border px-3 py-2">
              <MapPin className="mt-0.5 h-4 w-4 shrink-0 text-text-muted" aria-hidden />
              <div className="min-w-0 flex-1">
                <p className="text-[13px] font-medium text-text">
                  {site.name} <span className="font-normal text-text-muted">· {SITE_TENURE_LABELS[site.tenure]}</span>
                </p>
                <p className="text-[12.5px] text-text-muted">{site.geocodedLabel ?? site.address}</p>
                <p className={`text-[12px] ${site.geocoded ? 'text-green-maat-text' : 'text-amber'}`}>
                  {site.geocoded
                    ? `Localisé (${site.latitude?.toFixed(4)} ; ${site.longitude?.toFixed(4)})`
                    : 'Adresse non localisée : vérifiez-la pour compléter B1'}
                </p>
                {site.inOrNearSensitiveArea === true && (
                  <p className="text-[12px] text-text-muted">Zone sensible : {site.sensitiveAreaName ?? 'nom à préciser'}</p>
                )}
              </div>
              {!readOnly && (
                <div className="flex gap-3">
                  <button type="button" onClick={() => setEditing(site.id)} className="text-[12.5px] font-medium text-blue-maat-text hover:underline">
                    Modifier
                  </button>
                  <button type="button" onClick={() => void handleDelete(site.id)} className="text-[12.5px] font-medium text-red hover:underline">
                    Supprimer
                  </button>
                </div>
              )}
            </li>
          ),
        )}
      </ul>
      {error && (
        <p role="alert" className="mt-2 text-[12.5px] text-red">
          {error}
        </p>
      )}
      {!readOnly &&
        (editing === 'new' ? (
          <div className="mt-2">
            <SiteForm initial={EMPTY_SITE} onSubmit={handleCreate} onCancel={() => setEditing(null)} />
          </div>
        ) : (
          <button
            type="button"
            onClick={() => setEditing('new')}
            className="mt-2 text-[12.5px] font-medium text-blue-maat-text hover:underline"
          >
            Ajouter un site
          </button>
        ))}
    </div>
  )
}
