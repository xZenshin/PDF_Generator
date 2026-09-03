import { useCallback, useEffect, useRef, useState } from 'react'
import { api } from './api'
import { Editor } from './components/Editor'
import { Preview } from './components/Preview'
import { useCvEditor } from './useCvEditor'
import { CV_STYLES, type CvSummary } from './types'

const LAST_CV_KEY = 'cvbuilder.lastCvId'

export default function App() {
  const [cvs, setCvs] = useState<CvSummary[]>([])
  const [cvId, setCvId] = useState<string | null>(() => localStorage.getItem(LAST_CV_KEY))
  const [busy, setBusy] = useState(false)
  const [problem, setProblem] = useState<string | null>(null)

  const { cv, loading, status, error, dismissError, flush, actions } = useCvEditor(cvId)

  const selectCv = useCallback((id: string | null) => {
    setCvId(id)
    if (id) localStorage.setItem(LAST_CV_KEY, id)
    else localStorage.removeItem(LAST_CV_KEY)
  }, [])

  // Load the list once, and make sure something is selected. The guard keeps
  // StrictMode's double-invoke from creating two CVs on a fresh database.
  const initialised = useRef(false)
  useEffect(() => {
    if (initialised.current) return
    initialised.current = true

    let cancelled = false
    api
      .listCvs()
      .then(async (list) => {
        if (cancelled) return
        if (list.length === 0) {
          const created = await api.createCv()
          if (cancelled) return
          setCvs([{ id: created.id, name: created.name, fullName: created.fullName, updatedAt: created.updatedAt }])
          selectCv(created.id)
          return
        }
        setCvs(list)
        setCvId((current) => {
          const valid = current && list.some((c) => c.id === current) ? current : list[0].id
          localStorage.setItem(LAST_CV_KEY, valid)
          return valid
        })
      })
      .catch((err: unknown) => {
        if (!cancelled) setProblem(err instanceof Error ? err.message : String(err))
      })
    return () => {
      cancelled = true
    }
  }, [selectCv])

  // Keep the picker's label in step with edits to the CV's own name.
  useEffect(() => {
    if (!cv) return
    setCvs((list) => list.map((c) => (c.id === cv.id ? { ...c, name: cv.name, fullName: cv.fullName } : c)))
  }, [cv?.id, cv?.name, cv?.fullName])

  const newCv = async () => {
    setBusy(true)
    try {
      await flush()
      const created = await api.createCv()
      setCvs((list) => [
        { id: created.id, name: created.name, fullName: created.fullName, updatedAt: created.updatedAt },
        ...list,
      ])
      selectCv(created.id)
    } catch (err) {
      setProblem(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  const deleteCv = async () => {
    if (!cvId || !confirm('Delete this CV and everything in it?')) return
    setBusy(true)
    try {
      await api.deleteCv(cvId)
      const remaining = cvs.filter((c) => c.id !== cvId)
      setCvs(remaining)
      selectCv(remaining[0]?.id ?? null)
    } catch (err) {
      setProblem(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  const downloadPdf = async () => {
    if (!cvId) return
    setBusy(true)
    try {
      // The PDF is rendered from stored data, so pending edits must land first.
      await flush()
      const res = await fetch(api.pdfUrl(cvId))
      if (!res.ok) throw new Error(`PDF export failed (${res.status})`)

      const url = URL.createObjectURL(await res.blob())
      const link = document.createElement('a')
      link.href = url
      link.download = `${slug(cv?.fullName || cv?.name || 'my')}-cv.pdf`
      document.body.appendChild(link)
      link.click()
      link.remove()
      URL.revokeObjectURL(url)
    } catch (err) {
      setProblem(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  const message = problem ?? error

  return (
    <div className="app">
      <header className="topbar">
        <div className="brand">CV Builder</div>

        <select value={cvId ?? ''} onChange={(e) => selectCv(e.target.value)} disabled={busy}>
          {cvs.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
              {c.fullName ? ` — ${c.fullName}` : ''}
            </option>
          ))}
        </select>

        <button onClick={() => void newCv()} disabled={busy}>
          New CV
        </button>
        <button onClick={() => void deleteCv()} disabled={busy || !cvId}>
          Delete
        </button>

        {cv && (
          <div className="segmented" role="group" aria-label="CV style">
            {CV_STYLES.map((option) => (
              <button
                key={option.value}
                title={option.hint}
                className={cv.style === option.value ? 'seg active' : 'seg'}
                onClick={() => actions.updateHeader({ style: option.value })}
              >
                {option.label}
              </button>
            ))}
          </div>
        )}

        <span className={`status status-${status}`}>{statusText(status)}</span>

        <button className="primary" onClick={() => void downloadPdf()} disabled={busy || !cv}>
          Download PDF
        </button>
      </header>

      {message && (
        <div className="banner">
          <span>{message}</span>
          <button
            className="icon"
            onClick={() => {
              setProblem(null)
              dismissError()
            }}
          >
            ✕
          </button>
        </div>
      )}

      <main className="panes">
        <div className="pane pane-editor">
          {loading && !cv && <p className="empty">Loading…</p>}
          {cv && <Editor cv={cv} actions={actions} />}
        </div>
        <div className="pane pane-preview">{cv && <Preview cv={cv} />}</div>
      </main>
    </div>
  )
}

const statusText = (status: string) =>
  status === 'saving' ? 'Saving…' : status === 'saved' ? 'Saved' : status === 'error' ? 'Not saved' : ''

const slug = (value: string) =>
  value.trim().toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') || 'my'
