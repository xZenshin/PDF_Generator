import { SECTION_KINDS, type Bullet, type Cv, type Item, type Section } from '../types'
import type { CvActions } from '../useCvEditor'
import { Field, IncludeToggle, MoveButtons } from './Field'

/** What a section's children are called, per layout. */
const ITEM_LABELS: Record<Section['kind'], string> = {
  Timeline: 'Entry',
  Grouped: 'Category',
  Bullets: 'Group',
  FreeForm: 'Text block',
}

const CHILD_LABELS: Record<Section['kind'], string> = {
  Timeline: 'Bullet',
  Grouped: 'Skill',
  Bullets: 'Bullet',
  FreeForm: 'Paragraph',
}

export function Editor({ cv, actions }: { cv: Cv; actions: CvActions }) {
  return (
    <div className="editor">
      <HeaderCard cv={cv} actions={actions} />

      {cv.sections.map((section, index) => (
        <SectionCard
          key={section.uid}
          section={section}
          actions={actions}
          canUp={index > 0}
          canDown={index < cv.sections.length - 1}
        />
      ))}

      <div className="add-section">
        <span>Add section:</span>
        {SECTION_KINDS.map((kind) => (
          <button key={kind.value} onClick={() => actions.addSection(kind.value)} title={kind.hint}>
            + {kind.label}
          </button>
        ))}
      </div>
    </div>
  )
}

function HeaderCard({ cv, actions }: { cv: Cv; actions: CvActions }) {
  const set = actions.updateHeader
  return (
    <section className="card">
      <div className="card-head">
        <h2>Details</h2>
      </div>
      <div className="grid">
        <Field
          label="CV name (not printed)"
          value={cv.name}
          placeholder="Master CV"
          onChange={(name) => set({ name })}
        />
        <Field label="Full name" value={cv.fullName} onChange={(fullName) => set({ fullName })} />
        <Field
          label="Headline"
          value={cv.headline}
          placeholder="Backend Developer"
          onChange={(headline) => set({ headline })}
        />
        <Field label="Email" value={cv.email} onChange={(email) => set({ email })} />
        <Field label="Phone" value={cv.phone} onChange={(phone) => set({ phone })} />
        <Field label="Location" value={cv.location} onChange={(location) => set({ location })} />
        <Field label="Website" value={cv.website} onChange={(website) => set({ website })} />
      </div>
      <Field
        label="Summary"
        value={cv.summary}
        multiline
        placeholder="A couple of sentences about you."
        onChange={(summary) => set({ summary })}
      />
    </section>
  )
}

interface SectionCardProps {
  section: Section
  actions: CvActions
  canUp: boolean
  canDown: boolean
}

function SectionCard({ section, actions, canUp, canDown }: SectionCardProps) {
  return (
    <section className={`card${section.included ? '' : ' excluded'}`}>
      <div className="card-head">
        <input
          className="title-input"
          value={section.title}
          onChange={(e) => actions.updateSection(section.uid, { title: e.target.value })}
        />
        <select
          value={section.kind}
          title="How this section is laid out in the PDF"
          onChange={(e) =>
            actions.updateSection(section.uid, { kind: e.target.value as Section['kind'] })
          }
        >
          {SECTION_KINDS.map((kind) => (
            <option key={kind.value} value={kind.value}>
              {kind.label}
            </option>
          ))}
        </select>
        {section.kind === 'Bullets' && (
          <label className="include" title="Run this section's bullets in two columns">
            <input
              type="checkbox"
              checked={section.twoColumns}
              onChange={(e) =>
                actions.updateSection(section.uid, { twoColumns: e.target.checked })
              }
            />
            <span>2 cols</span>
          </label>
        )}
        <IncludeToggle
          checked={section.included}
          onChange={(included) => actions.updateSection(section.uid, { included })}
        />
        <MoveButtons
          canUp={canUp}
          canDown={canDown}
          onUp={() => actions.moveSection(section.uid, -1)}
          onDown={() => actions.moveSection(section.uid, 1)}
        />
        <button className="icon danger" title="Delete section" onClick={() => actions.removeSection(section.uid)}>
          ✕
        </button>
      </div>

      {section.items.map((item, index) => (
        <ItemRow
          key={item.uid}
          section={section}
          item={item}
          actions={actions}
          canUp={index > 0}
          canDown={index < section.items.length - 1}
        />
      ))}

      <button className="add" onClick={() => actions.addItem(section.uid)}>
        + {ITEM_LABELS[section.kind]}
      </button>
    </section>
  )
}

interface ItemRowProps {
  section: Section
  item: Item
  actions: CvActions
  canUp: boolean
  canDown: boolean
}

function ItemRow({ section, item, actions, canUp, canDown }: ItemRowProps) {
  const set = (patch: Parameters<CvActions['updateItem']>[1]) => actions.updateItem(item.uid, patch)
  const timeline = section.kind === 'Timeline'

  return (
    <div className={`item${item.included ? '' : ' excluded'}`}>
      <div className="item-head">
        <IncludeToggle
          checked={item.included}
          onChange={(included) => actions.updateItem(item.uid, { included })}
        />
        <MoveButtons
          canUp={canUp}
          canDown={canDown}
          onUp={() => actions.moveItem(section.uid, item.uid, -1)}
          onDown={() => actions.moveItem(section.uid, item.uid, 1)}
        />
        <button className="icon danger" title="Delete entry" onClick={() => actions.removeItem(item.uid)}>
          ✕
        </button>
      </div>

      {section.kind !== 'Bullets' && section.kind !== 'FreeForm' && (
        <div className="grid">
          <Field
            label={timeline ? 'Title' : 'Label'}
            value={item.title}
            placeholder={timeline ? 'Senior Developer' : 'Languages'}
            onChange={(title) => set({ title })}
          />
          {timeline && (
            <>
              <Field
                label="Organisation"
                value={item.organization}
                onChange={(organization) => set({ organization })}
              />
              <Field label="Location" value={item.location} onChange={(location) => set({ location })} />
              <div className="grid two">
                <Field
                  label="From"
                  value={item.startDate}
                  placeholder="2022"
                  onChange={(startDate) => set({ startDate })}
                />
                <Field
                  label="To"
                  value={item.endDate}
                  placeholder="Present"
                  onChange={(endDate) => set({ endDate })}
                />
              </div>
            </>
          )}
        </div>
      )}

      <div className="bullets">
        {item.bullets.map((bullet, index) => (
          <BulletRow
            key={bullet.uid}
            item={item}
            bullet={bullet}
            actions={actions}
            rows={section.kind === 'FreeForm' ? 4 : 1}
            placeholder={
              section.kind === 'FreeForm'
                ? 'Write freely — this prints as a paragraph.'
                : 'What you did, and what came of it.'
            }
            canUp={index > 0}
            canDown={index < item.bullets.length - 1}
          />
        ))}
        <button className="add" onClick={() => actions.addBullet(item.uid)}>
          + {CHILD_LABELS[section.kind]}
        </button>
      </div>
    </div>
  )
}

interface BulletRowProps {
  item: Item
  bullet: Bullet
  actions: CvActions
  rows: number
  placeholder: string
  canUp: boolean
  canDown: boolean
}

function BulletRow({ item, bullet, actions, rows, placeholder, canUp, canDown }: BulletRowProps) {
  return (
    <div className={`bullet${bullet.included ? '' : ' excluded'}`}>
      <input
        type="checkbox"
        checked={bullet.included}
        title="Include in the exported PDF"
        onChange={(e) => actions.updateBullet(bullet.uid, { included: e.target.checked })}
      />
      <textarea
        rows={rows}
        value={bullet.text}
        placeholder={placeholder}
        onChange={(e) => actions.updateBullet(bullet.uid, { text: e.target.value })}
      />
      <MoveButtons
        canUp={canUp}
        canDown={canDown}
        onUp={() => actions.moveBullet(item.uid, bullet.uid, -1)}
        onDown={() => actions.moveBullet(item.uid, bullet.uid, 1)}
      />
      <button className="icon danger" title="Delete bullet" onClick={() => actions.removeBullet(bullet.uid)}>
        ✕
      </button>
    </div>
  )
}
