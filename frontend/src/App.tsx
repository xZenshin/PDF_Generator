import { useCallback, useEffect, useRef, useState } from 'react'
import { api } from './api'
import { Editor } from './components/Editor'
import { Preview } from './components/Preview'
import { TailorDialog } from './components/TailorDialog'
import { useCvEditor } from './useCvEditor'
import { CV_STYLES, type Cv, type CvSummary } from './types'

const LAST_CV_KEY = 'cvbuilder.lastCvId'

export default function App() {
  const [cvs, setCvs] = useState<CvSummary[]>([])
  const [cvId, setCvId] = useState<string | null>(() => localStorage.getItem(LAST_CV_KEY))
  const [busy, setBusy] = useState(false)
  const [problem, setProblem] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [tailoring, setTailoring] = useState(false)

  const { cv, loading, status, error, dismissError, flush, reload, actions } = useCvEditor(cvId)

  const selectCv = useCallback((id: string | null) => {
    setCvId(id)
    if (id) localStorage.setItem(LAST_CV_KEY, id)
    else localStorage.removeItem(LAST_CV_KEY)
  }, [])

  const saveFileInput = useRef<HTMLInputElement>(null)

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
          setCvs([summaryOf(created)])
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
      setCvs((list) => [summaryOf(created), ...list])
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
      // Both exports are rendered from stored data, so pending edits must land first.
      await flush()
      await saveBlob(api.pdfUrl(cvId), `${slug(cv?.fullName || cv?.name || 'my')}-cv.pdf`)
    } catch (err) {
      setProblem(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  const saveToFile = async () => {
    if (!cvId) return
    setBusy(true)
    try {
      await flush()
      await saveBlob(api.exportUrl(cvId), `${slug(cv?.name || cv?.fullName || 'my')}.cvjson`)
    } catch (err) {
      setProblem(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  const openSaveFile = async (file: File) => {
    setBusy(true)
    try {
      await flush()
      const imported = await api.importCv(await file.text())
      setCvs((list) => [summaryOf(imported), ...list])
      selectCv(imported.id)
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

        <button
          onClick={() => void saveToFile()}
          disabled={busy || !cv}
          title="Download this CV as a save file you can import later"
        >
          Save to file
        </button>
        <button
          onClick={() => saveFileInput.current?.click()}
          disabled={busy}
          title="Import a save file as a new CV"
        >
          Open file
        </button>
        <input
          ref={saveFileInput}
          type="file"
          accept=".cvjson,.json,application/json"
          hidden
          onChange={(e) => {
            const file = e.target.files?.[0]
            // Cleared so picking the same file twice still fires a change.
            e.target.value = ''
            if (file) void openSaveFile(file)
          }}
        />

        <button
          onClick={() => setTailoring(true)}
          disabled={busy || !cv}
          title="Ask DeepSeek which parts of this CV fit a job listing"
        >
          Tailor…
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

      {notice && (
        <div className="banner good">
          <span>{notice}</span>
          <button className="icon" onClick={() => setNotice(null)}>
            ✕
          </button>
        </div>
      )}

      {tailoring && cv && (
        <TailorDialog
          cvId={cv.id}
          cvName={cv.name}
          onClose={() => setTailoring(false)}
          onApplied={(summary) => {
            setTailoring(false)
            setNotice(summary)
            void reload()
          }}
        />
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

const summaryOf = (cv: Cv): CvSummary => ({
  id: cv.id,
  name: cv.name,
  fullName: cv.fullName,
  updatedAt: cv.updatedAt,
})

/** Fetches a URL and hands the bytes to the browser as a download. */
async function saveBlob(url: string, filename: string) {
  const res = await fetch(url)
  if (!res.ok) throw new Error(`Export failed (${res.status})`)

  const href = URL.createObjectURL(await res.blob())
  const link = document.createElement('a')
  link.href = href
  link.download = filename
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(href)
}

const statusText = (status: string) =>
  status === 'saving' ? 'Saving…' : status === 'saved' ? 'Saved' : status === 'error' ? 'Not saved' : ''

const slug = (value: string) =>
  value.trim().toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') || 'my'
