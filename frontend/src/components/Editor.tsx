import { SECTION_KINDS, type Bullet, type Cv, type Item, type Section } from '../types'
import type { CvActions } from '../useCvEditor'
import { Field, IncludeToggle, MoveButtons } from './Field'

export function Editor({ cv, actions }: { cv: Cv; actions: CvActions }) {
  return (
    <div className="editor">
      <HeaderCard cv={cv} actions={actions} />

      {cv.sections.map((section, index) => (
        <SectionCard
          key={section.id}
          section={section}
          actions={actions}
          canUp={index > 0}
          canDown={index < cv.sections.length - 1}
        />
      ))}

      <div className="add-section">
        <span>Add section:</span>
        {SECTION_KINDS.map((kind) => (
          <button key={kind.value} onClick={() => void actions.addSection(kind.value)} title={kind.hint}>
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
          onChange={(e) => actions.updateSection(section.id, { title: e.target.value })}
        />
        <select
          value={section.kind}
          title="How this section is laid out in the PDF"
          onChange={(e) =>
            actions.updateSection(section.id, { kind: e.target.value as Section['kind'] }, true)
          }
        >
          {SECTION_KINDS.map((kind) => (
            <option key={kind.value} value={kind.value}>
              {kind.label}
            </option>
          ))}
        </select>
        <IncludeToggle
          checked={section.included}
          onChange={(included) => actions.updateSection(section.id, { included }, true)}
        />
        <MoveButtons
          canUp={canUp}
          canDown={canDown}
          onUp={() => actions.moveSection(section.id, -1)}
          onDown={() => actions.moveSection(section.id, 1)}
        />
        <button className="icon danger" title="Delete section" onClick={() => actions.removeSection(section.id)}>
          ✕
        </button>
      </div>

      {section.items.map((item, index) => (
        <ItemRow
          key={item.id}
          section={section}
          item={item}
          actions={actions}
          canUp={index > 0}
          canDown={index < section.items.length - 1}
        />
      ))}

      <button className="add" onClick={() => void actions.addItem(section.id)}>
        + {section.kind === 'Grouped' ? 'Category' : section.kind === 'Bullets' ? 'Group' : 'Entry'}
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
  const set = (patch: Parameters<CvActions['updateItem']>[1]) => actions.updateItem(item.id, patch)
  const timeline = section.kind === 'Timeline'

  return (
    <div className={`item${item.included ? '' : ' excluded'}`}>
      <div className="item-head">
        <IncludeToggle
          checked={item.included}
          onChange={(included) => actions.updateItem(item.id, { included }, true)}
        />
        <MoveButtons
          canUp={canUp}
          canDown={canDown}
          onUp={() => actions.moveItem(section.id, item.id, -1)}
          onDown={() => actions.moveItem(section.id, item.id, 1)}
        />
        <button className="icon danger" title="Delete entry" onClick={() => actions.removeItem(item.id)}>
          ✕
        </button>
      </div>

      {section.kind !== 'Bullets' && (
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
            key={bullet.id}
            item={item}
            bullet={bullet}
            actions={actions}
            canUp={index > 0}
            canDown={index < item.bullets.length - 1}
          />
        ))}
        <button className="add" onClick={() => void actions.addBullet(item.id)}>
          + {section.kind === 'Grouped' ? 'Skill' : 'Bullet'}
        </button>
      </div>
    </div>
  )
}

interface BulletRowProps {
  item: Item
  bullet: Bullet
  actions: CvActions
  canUp: boolean
  canDown: boolean
}

function BulletRow({ item, bullet, actions, canUp, canDown }: BulletRowProps) {
  return (
    <div className={`bullet${bullet.included ? '' : ' excluded'}`}>
      <input
        type="checkbox"
        checked={bullet.included}
        title="Include in the exported PDF"
        onChange={(e) => actions.updateBullet(bullet.id, { included: e.target.checked }, true)}
      />
      <textarea
        rows={1}
        value={bullet.text}
        placeholder="What you did, and what came of it."
        onChange={(e) => actions.updateBullet(bullet.id, { text: e.target.value })}
      />
      <MoveButtons
        canUp={canUp}
        canDown={canDown}
        onUp={() => actions.moveBullet(item.id, bullet.id, -1)}
        onDown={() => actions.moveBullet(item.id, bullet.id, 1)}
      />
      <button className="icon danger" title="Delete bullet" onClick={() => actions.removeBullet(bullet.id)}>
        ✕
      </button>
    </div>
  )
}
