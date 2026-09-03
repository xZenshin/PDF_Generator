import { useEffect, useState } from 'react'
import { api } from '../api'
import type { AiStatus, PlannedChange, TailorResponse } from '../types'

interface TailorDialogProps {
  cvId: string
  cvName: string
  onClose: () => void
  /** Called after changes land, so the editor and preview reload from the server. */
  onApplied: (summary: string) => void
}

/**
 * The tailoring loop: paste a listing, ask DeepSeek which parts of the CV fit it,
 * read what that would change, then apply. Nothing is written until Apply is clicked.
 */
export function TailorDialog({ cvId, cvName, onClose, onApplied }: TailorDialogProps) {
  const [status, setStatus] = useState<AiStatus | null>(null)
  const [jobListing, setJobListing] = useState('')
  const [result, setResult] = useState<TailorResponse | null>(null)
  const [asking, setAsking] = useState(false)
  const [applying, setApplying] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    api.aiStatus().then(setStatus).catch(() => setStatus({ configured: false, model: 'unknown' }))
  }, [])

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose()
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])

  const ask = async () => {
    setAsking(true)
    setError(null)
    setResult(null)
    try {
      setResult(await api.tailor(cvId, jobListing))
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
    } finally {
      setAsking(false)
    }
  }

  const apply = async () => {
    if (!result) return
    setApplying(true)
    setError(null)
    try {
      const { plan } = await api.applyTailoring(cvId, result.recommendation)
      const included = plan.changes.filter((c) => c.include).length
      const excluded = plan.changes.length - included
      onApplied(`Tailored ${cvName}: ${included} included, ${excluded} excluded.`)
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
      setApplying(false)
    }
  }

  const plan = result?.plan
  const included = plan?.changes.filter((c) => c.include) ?? []
  const excluded = plan?.changes.filter((c) => !c.include) ?? []
  const busy = asking || applying

  return (
    <div className="overlay" onClick={onClose}>
      <div className="dialog" onClick={(e) => e.stopPropagation()}>
        <div className="dialog-head">
          <h2>Tailor to a job listing</h2>
          <button className="icon" onClick={onClose} title="Close">
            ✕
          </button>
        </div>

        {status && !status.configured && (
          <p className="notice">
            No DeepSeek API key is configured. Set <code>DeepSeek__ApiKey</code> in the API's
            environment and restart it.
          </p>
        )}

        <label className="field">
          <span className="field-label">Job listing</span>
          <textarea
            rows={10}
            value={jobListing}
            placeholder="Paste the advert here — responsibilities, requirements, the lot."
            onChange={(e) => setJobListing(e.target.value)}
          />
        </label>

        <div className="dialog-actions">
          <button
            className="primary"
            onClick={() => void ask()}
            disabled={busy || !jobListing.trim() || status?.configured === false}
          >
            {asking ? 'Asking DeepSeek…' : 'Ask DeepSeek'}
          </button>
          {status?.configured && <span className="muted-small">model: {status.model}</span>}
        </div>

        {error && <p className="notice error">{error}</p>}

        {plan && (
          <div className="plan">
            <h3>
              {plan.changes.length === 0
                ? 'No changes suggested'
                : `${plan.changes.length} change${plan.changes.length === 1 ? '' : 's'} suggested`}
            </h3>

            {plan.changes.length === 0 && (
              <p className="muted-small">
                The model's picks already match what your CV includes.
              </p>
            )}

            {included.length > 0 && <ChangeList title="Will be included" changes={included} />}
            {excluded.length > 0 && <ChangeList title="Will be excluded" changes={excluded} />}

            <ul className="plan-notes">
              {plan.alreadyCorrect > 0 && (
                <li>{plan.alreadyCorrect} already set as recommended.</li>
              )}
              {plan.unrecognised.length > 0 && (
                <li className="warn">
                  {plan.unrecognised.length} suggestion
                  {plan.unrecognised.length === 1 ? '' : 's'} did not match anything in this CV and
                  {plan.unrecognised.length === 1 ? ' was' : ' were'} ignored.
                </li>
              )}
              {plan.contradictory.length > 0 && (
                <li className="warn">
                  {plan.contradictory.length} item{plan.contradictory.length === 1 ? '' : 's'} came
                  back as both keep and drop — treated as dropped.
                </li>
              )}
            </ul>

            <div className="dialog-actions">
              <button
                className="primary"
                onClick={() => void apply()}
                disabled={busy || plan.changes.length === 0}
              >
                {applying ? 'Applying…' : 'Apply changes'}
              </button>
              <button onClick={onClose} disabled={busy}>
                Cancel
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}

function ChangeList({ title, changes }: { title: string; changes: PlannedChange[] }) {
  return (
    <div className="change-list">
      <h4>{title}</h4>
      <ul>
        {changes.map((change) => (
          <li key={change.ref}>
            <span className={`kind kind-${change.kind}`}>{change.kind}</span>
            <span className="change-label">{change.label}</span>
            {change.cascaded && (
              <span className="cascaded" title="Added so the lines inside it can print">
                needed by a kept line
              </span>
            )}
          </li>
        ))}
      </ul>
    </div>
  )
}
