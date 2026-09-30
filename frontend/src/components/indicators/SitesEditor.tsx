import { CheckCircle2, Leaf, MapPin, Pencil, Plus, Trash2, TriangleAlert } from 'lucide-react'
import { type FormEvent, useState } from 'react'
import { ApiError } from '../../api/authApi'
import * as vsmeApi from '../../api/vsmeApi'
import type { CompanySite, CompanySiteInput, SiteTenure } from '../../api/vsmeApi'
import { SITE_TENURE_LABELS } from '../../constants/vsme'
import { INPUT_CLASS, YesNoField } from './VsmeFields'

// docs/specs/norme-volontaire.md : sites de l'entreprise (B1 géolocalisation, B5 zones
// sensibles). Chaque site s'enregistre à part, tout de suite : le serveur le géocode (ADR 0013)
// et dit s'il a trouvé l'adresse, ce que la tuile affiche aussitôt.

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
    <form onSubmit={(e) => void handleSubmit(e)} className="grid gap-4 rounded-xl border border-blue-maat/30 bg-kpi-blue/60 p-4 sm:col-span-2 sm:grid-cols-2">
      <label className="block text-[13px] font-medium text-text">
        Nom du site
        <input value={site.name} onChange={(e) => setSite({ ...site, name: e.target.value })} required maxLength={120} className={`${INPUT_CLASS} mt-1.5`} />
      </label>
      <label className="block text-[13px] font-medium text-text">
        Statut
        <select value={site.tenure} onChange={(e) => setSite({ ...site, tenure: e.target.value as SiteTenure })} className={`${INPUT_CLASS} mt-1.5`}>
          {Object.entries(SITE_TENURE_LABELS).map(([value, label]) => (
            <option key={value} value={value}>
              {label}
            </option>
          ))}
        </select>
      </label>
      <label className="block text-[13px] font-medium text-text sm:col-span-2">
        Adresse postale complète
        <input
          value={site.address}
          onChange={(e) => setSite({ ...site, address: e.target.value })}
          required
          maxLength={300}
          placeholder="12 rue de la Paix 75002 Paris"
          className={`${INPUT_CLASS} mt-1.5`}
        />
      </label>
      <YesNoField
        name="site-sensitive"
        label="Dans ou près d’une zone sensible pour la biodiversité ?"
        hint="Natura 2000, ZNIEFF, réserve naturelle, site Ramsar…"
        value={site.inOrNearSensitiveArea}
        onChange={(value) => setSite({ ...site, inOrNearSensitiveArea: value, sensitiveAreaName: value ? site.sensitiveAreaName : null })}
        readOnly={false}
      />
      {site.inOrNearSensitiveArea === true && (
        <label className="block text-[13px] font-medium text-text sm:col-span-2">
          Nom de la zone sensible
          <input
            value={site.sensitiveAreaName ?? ''}
            onChange={(e) => setSite({ ...site, sensitiveAreaName: e.target.value || null })}
            maxLength={200}
            className={`${INPUT_CLASS} mt-1.5`}
          />
        </label>
      )}
      {error && (
        <p role="alert" className="text-[12.5px] text-red sm:col-span-2">
          {error}
        </p>
      )}
      <div className="flex gap-2 sm:col-span-2">
        <button
          type="submit"
          disabled={saving}
          className="rounded-button bg-blue-maat px-4 py-2 text-[13px] font-semibold text-white shadow-button-primary transition-colors hover:bg-blue-maat-text disabled:opacity-50"
        >
          {saving ? 'Localisation…' : 'Enregistrer le site'}
        </button>
        <button type="button" onClick={onCancel} className="rounded-button px-4 py-2 text-[13px] font-medium text-text-muted transition-colors hover:bg-white hover:text-text">
          Annuler
        </button>
      </div>
    </form>
  )
}

function SiteTile({
  site,
  readOnly,
  onEdit,
  onDelete,
}: {
  site: CompanySite
  readOnly: boolean
  onEdit: () => void
  onDelete: () => void
}) {
  return (
    <li className="flex flex-col gap-2 rounded-xl border border-border bg-white p-4">
      <div className="flex items-start gap-3">
        <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-blue-maat/10 text-blue-maat" aria-hidden>
          <MapPin className="h-4 w-4" />
        </span>
        <div className="min-w-0 flex-1">
          <p className="truncate text-[13.5px] font-semibold text-text">{site.name}</p>
          <p className="text-[12px] text-text-muted">{SITE_TENURE_LABELS[site.tenure]}</p>
        </div>
        {!readOnly && (
          <div className="flex shrink-0">
            <button type="button" onClick={onEdit} aria-label={`Modifier le site ${site.name}`} className="rounded-button p-1.5 text-text-muted hover:bg-bg hover:text-text">
              <Pencil className="h-4 w-4" aria-hidden />
            </button>
            <button type="button" onClick={onDelete} aria-label={`Supprimer le site ${site.name}`} className="rounded-button p-1.5 text-text-muted hover:bg-red/10 hover:text-red">
              <Trash2 className="h-4 w-4" aria-hidden />
            </button>
          </div>
        )}
      </div>
      <p className="text-[12.5px] text-text">{site.geocodedLabel ?? site.address}</p>
      <div className="flex flex-wrap gap-1.5">
        {site.geocoded ? (
          <span className="inline-flex items-center gap-1 rounded-full bg-green-maat/15 px-2 py-0.5 text-[11.5px] font-medium text-green-maat-text">
            <CheckCircle2 className="h-3 w-3" aria-hidden />
            Localisé
          </span>
        ) : (
          <span className="inline-flex items-center gap-1 rounded-full bg-orange/15 px-2 py-0.5 text-[11.5px] font-medium text-amber">
            <TriangleAlert className="h-3 w-3" aria-hidden />
            Adresse non localisée
          </span>
        )}
        {site.inOrNearSensitiveArea === true && (
          <span className="inline-flex items-center gap-1 rounded-full bg-green-maat/10 px-2 py-0.5 text-[11.5px] font-medium text-green-maat-text">
            <Leaf className="h-3 w-3" aria-hidden />
            {site.sensitiveAreaName ?? 'Zone sensible, nom à préciser'}
          </span>
        )}
        {site.inOrNearSensitiveArea === null && (
          <span className="rounded-full bg-orange/15 px-2 py-0.5 text-[11.5px] font-medium text-amber">Zone sensible : à préciser</span>
        )}
      </div>
    </li>
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

  const editedSite = sites.find((s) => s.id === editing)

  return (
    <div className="sm:col-span-2">
      <p className="text-[13px] font-medium text-text">Sites détenus, loués ou gérés</p>
      <p className="mt-0.5 text-[12px] font-light text-text-muted">
        L’adresse est localisée automatiquement par le service public de l’IGN : c’est la géolocalisation que demande B1.
      </p>

      {sites.length === 0 && editing !== 'new' && <p className="mt-2 text-[13px] text-text-muted">Aucun site déclaré.</p>}

      {sites.length > 0 && (
        <ul className="mt-3 grid gap-3 md:grid-cols-2">
          {sites.map((site) => (
            <SiteTile
              key={site.id}
              site={site}
              readOnly={readOnly}
              onEdit={() => setEditing(site.id)}
              onDelete={() => void handleDelete(site.id)}
            />
          ))}
        </ul>
      )}

      {error && (
        <p role="alert" className="mt-2 text-[12.5px] text-red">
          {error}
        </p>
      )}

      {!readOnly && editedSite && (
        <div className="mt-3">
          <SiteForm key={editedSite.id} initial={editedSite} onSubmit={(input) => handleUpdate(editedSite.id, input)} onCancel={() => setEditing(null)} />
        </div>
      )}

      {!readOnly && editing === 'new' && (
        <div className="mt-3">
          <SiteForm initial={EMPTY_SITE} onSubmit={handleCreate} onCancel={() => setEditing(null)} />
        </div>
      )}

      {!readOnly && editing === null && (
        <button
          type="button"
          onClick={() => setEditing('new')}
          className="mt-3 inline-flex items-center gap-1.5 rounded-button border border-dashed border-border-strong px-3 py-1.5 text-[12.5px] font-medium text-blue-maat-text transition-colors hover:border-blue-maat hover:bg-blue-maat/5"
        >
          <Plus className="h-3.5 w-3.5" aria-hidden />
          Ajouter un site
        </button>
      )}
    </div>
  )
}
