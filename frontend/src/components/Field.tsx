interface FieldProps {
  label: string
  value: string
  onChange: (value: string) => void
  placeholder?: string
  multiline?: boolean
  rows?: number
}

export function Field({ label, value, onChange, placeholder, multiline, rows = 3 }: FieldProps) {
  return (
    <label className="field">
      <span className="field-label">{label}</span>
      {multiline ? (
        <textarea
          value={value}
          rows={rows}
          placeholder={placeholder}
          onChange={(e) => onChange(e.target.value)}
        />
      ) : (
        <input
          type="text"
          value={value}
          placeholder={placeholder}
          onChange={(e) => onChange(e.target.value)}
        />
      )}
    </label>
  )
}

interface IncludeToggleProps {
  checked: boolean
  onChange: (checked: boolean) => void
  label?: string
}

/** The core interaction: keep the text, decide whether this export prints it. */
export function IncludeToggle({ checked, onChange, label = 'In PDF' }: IncludeToggleProps) {
  return (
    <label className="include" title="Include in the exported PDF">
      <input type="checkbox" checked={checked} onChange={(e) => onChange(e.target.checked)} />
      <span>{label}</span>
    </label>
  )
}

interface MoveButtonsProps {
  onUp: () => void
  onDown: () => void
  canUp: boolean
  canDown: boolean
}

export function MoveButtons({ onUp, onDown, canUp, canDown }: MoveButtonsProps) {
  return (
    <>
      <button className="icon" onClick={onUp} disabled={!canUp} title="Move up">
        ↑
      </button>
      <button className="icon" onClick={onDown} disabled={!canDown} title="Move down">
        ↓
      </button>
    </>
  )
}
