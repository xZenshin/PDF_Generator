import type { Cv, Item, Section } from '../types'

/**
 * Mirrors CvPdfGenerator on the server: same inclusion rules, same ordering,
 * same "drop entries that would print blank". The PDF stays the source of truth,
 * this is the fast feedback loop.
 */
export function Preview({ cv }: { cv: Cv }) {
  const sections = cv.sections
    .filter((s) => s.included)
    .map((s) => ({ section: s, items: visibleItems(s) }))
    .filter((s) => s.items.length > 0)

  const contact = [cv.email, cv.phone, cv.location, cv.website].filter(Boolean).join(cv.style === 'Mono' ? ' | ' : ' · ')

  return (
    <div className={`paper paper-${cv.style.toLowerCase()}`}>
      <header className="paper-head">
        {cv.fullName && <h1>{cv.fullName}</h1>}
        {cv.headline && <p className="headline">{cv.headline}</p>}
        {contact && <p className="contact">{contact}</p>}
      </header>

      {cv.summary && <p className="summary">{cv.summary}</p>}

      {sections.map(({ section, items }) => (
        <section key={section.id} className="paper-section">
          <h2>{section.title}</h2>
          {items.map((item) => (
            <PreviewItem key={item.id} section={section} item={item} />
          ))}
        </section>
      ))}

      {sections.length === 0 && !cv.summary && (
        <p className="empty">Nothing is included yet — tick "In PDF" on the entries you want.</p>
      )}
    </div>
  )
}

function PreviewItem({ section, item }: { section: Section; item: Item }) {
  const bullets = visibleBullets(item)

  if (section.kind === 'Grouped') {
    return (
      <div className="grouped">
        <span className="grouped-label">{item.title}</span>
        <span>{bullets.map((b) => b.text).join(', ')}</span>
      </div>
    )
  }

  if (section.kind === 'FreeForm') {
    return (
      <div className="prose">
        {bullets.map((b) => (
          <p key={b.id}>{b.text}</p>
        ))}
      </div>
    )
  }

  if (section.kind === 'Bullets') {
    return (
      <ul className="paper-bullets">
        {bullets.map((b) => (
          <li key={b.id}>{b.text}</li>
        ))}
      </ul>
    )
  }

  const dates = [item.startDate, item.endDate].filter(Boolean).join(' – ')
  return (
    <div className="entry">
      <div className="entry-head">
        <div>
          {item.title && <strong>{item.title}</strong>}
          {item.organization && <div className="muted">{item.organization}</div>}
        </div>
        <div className="entry-meta">
          {dates && <div>{dates}</div>}
          {item.location && <div>{item.location}</div>}
        </div>
      </div>
      {bullets.length > 0 && (
        <ul className="paper-bullets">
          {bullets.map((b) => (
            <li key={b.id}>{b.text}</li>
          ))}
        </ul>
      )}
    </div>
  )
}

const visibleBullets = (item: Item) => item.bullets.filter((b) => b.included && b.text.trim())

const visibleItems = (section: Section) =>
  section.items.filter((item) => {
    if (!item.included) return false
    if (section.kind === 'Timeline') {
      return Boolean(item.title.trim() || item.organization.trim() || visibleBullets(item).length)
    }
    return visibleBullets(item).length > 0
  })
