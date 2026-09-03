import { useRef, useState } from 'react'
import { api } from './api'
import { toEditable } from './cvFile'
import { Editor } from './components/Editor'
import { Preview } from './components/Preview'
import { TailorDialog } from './components/TailorDialog'
import { useCvEditor } from './useCvEditor'
import { CV_STYLES } from './types'

export default function App() {
  const [busy, setBusy] = useState(false)
  const [problem, setProblem] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [tailoring, setTailoring] = useState(false)
  const saveFileInput = useRef<HTMLInputElement>(null)

  const { cv, ready, error, unsaved, dismissError, adopt, markSaved, startNew, actions } =
    useCvEditor()

  const guard = async (work: () => Promise<void>) => {
    setBusy(true)
    setProblem(null)
    try {
      await work()
    } catch (err) {
      setProblem(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  const newCv = () =>
    guard(async () => {
      if (unsaved && !confirm('Start a new CV? Anything not saved to a file will be lost.')) return
      await startNew()
      setNotice(null)
    })

  const downloadPdf = () =>
    guard(async () => {
      if (!cv) return
      await saveResponse(await api.pdf(cv), `${slug(cv.fullName || cv.name || 'my')}-cv.pdf`)
    })

  const saveToFile = () =>
    guard(async () => {
      if (!cv) return
      await saveResponse(await api.export(cv), `${slug(cv.name || cv.fullName || 'my')}.cvjson`)
      markSaved()
      setNotice('Saved to file. That file is the only lasting copy — keep it somewhere safe.')
    })

  const openSaveFile = (file: File) =>
    guard(async () => {
      if (unsaved && !confirm('Open this file? Anything not saved to a file will be lost.')) return
      adopt(toEditable(await api.import(await file.text())), false)
      setNotice(`Opened ${file.name}.`)
    })

  const message = problem ?? error

  return (
    <div className="app">
      <header className="topbar">
        <div className="brand">CV Builder</div>

        <button onClick={() => void newCv()} disabled={busy}>
          New
        </button>
        <button
          onClick={() => saveFileInput.current?.click()}
          disabled={busy}
          title="Open a .cvjson save file"
        >
          Open file
        </button>
        <button
          onClick={() => void saveToFile()}
          disabled={busy || !cv}
          title="Download this CV as a save file you can open later"
        >
          Save to file
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

        <span className="status" title="Edits are kept in this browser only until you save a file">
          {unsaved ? 'Not saved to a file' : 'Draft in this browser'}
        </span>

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
          cv={cv}
          onClose={() => setTailoring(false)}
          onApplied={(next, summary) => {
            setTailoring(false)
            adopt(next)
            setNotice(summary)
          }}
        />
      )}

      <main className="panes">
        <div className="pane pane-editor">
          {!ready && <p className="empty">Loading…</p>}
          {cv && <Editor cv={cv} actions={actions} />}
        </div>
        <div className="pane pane-preview">{cv && <Preview cv={cv} />}</div>
      </main>
    </div>
  )
}

/** Hands a response body to the browser as a download. */
async function saveResponse(res: Response, filename: string) {
  const href = URL.createObjectURL(await res.blob())
  const link = document.createElement('a')
  link.href = href
  link.download = filename
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(href)
}

const slug = (value: string) =>
  value.trim().toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') || 'my'
